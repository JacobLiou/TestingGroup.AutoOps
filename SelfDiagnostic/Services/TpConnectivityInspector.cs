using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// TP 连通性检测器 — 读取 TP 配置文件、检测串口与网络端点的可用性，生成连通性快照。
    /// </summary>
    public sealed class TpConnectivityInspector
    {
        private const string ConfigRelativePath = @"config\tpConnectivity.json";
        private const string DefaultTpRootPath = @"C:\Users\menghl2\WorkSpace\Projects\Test Program\cal_fts_fvs_fqc\RELEASE";
        private const int EndpointProbeTimeoutMs = 800;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private static readonly Regex ComRegex = new Regex(@"\bCOM\d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IpPortRegex = new Regex(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?::\d{1,5})?\b", RegexOptions.Compiled);
        private static readonly object CacheSync = new object();
        private static TpConnectivitySnapshot _cachedSnapshot;
        private static DateTimeOffset _cachedAtUtc;
        private static string _cachedTpRootPath;
        private static Task<TpConnectivitySnapshot> _inflightInspectionTask;
        private static string _inflightTpRootPath;

        private sealed class TpConnectivityConfig
        {
            public string TpRootPath { get; set; } = DefaultTpRootPath;
        }

        /// <summary>
        /// 执行 TP 连通性巡检（含短期缓存与并发去重），返回串口与网络端点状态快照。
        /// </summary>
        public async Task<TpConnectivitySnapshot> InspectAsync(CancellationToken cancellationToken)
        {
            var tpRootPath = LoadConfigPath();
            TpConnectivitySnapshot cachedSnapshot;
            if (TryGetCachedSnapshot(tpRootPath, out cachedSnapshot))
            {
                return cachedSnapshot;
            }

            Task<TpConnectivitySnapshot> inspectTask;
            lock (CacheSync)
            {
                if (TryGetCachedSnapshotUnderLock(tpRootPath, out cachedSnapshot))
                {
                    return cachedSnapshot;
                }

                if (_inflightInspectionTask != null &&
                    !_inflightInspectionTask.IsCompleted &&
                    string.Equals(_inflightTpRootPath, tpRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    inspectTask = _inflightInspectionTask;
                }
                else
                {
                    inspectTask = InspectCoreAsync(tpRootPath);
                    _inflightInspectionTask = inspectTask;
                    _inflightTpRootPath = tpRootPath;
                }
            }

            var snapshot = await inspectTask;

            lock (CacheSync)
            {
                if (inspectTask.Status == TaskStatus.RanToCompletion)
                {
                    _cachedSnapshot = snapshot;
                    _cachedTpRootPath = tpRootPath;
                    _cachedAtUtc = DateTimeOffset.UtcNow;
                }

                if (ReferenceEquals(_inflightInspectionTask, inspectTask))
                {
                    _inflightInspectionTask = null;
                    _inflightTpRootPath = null;
                }
            }

            return snapshot;
        }

        private static async Task<TpConnectivitySnapshot> InspectCoreAsync(string tpRootPath)
        {
            if (!Directory.Exists(tpRootPath))
            {
                return new TpConnectivitySnapshot
                {
                    TpRootPath = tpRootPath,
                    TpPathExists = false,
                    Error = "TP 路径不存在"
                };
            }

            try
            {
                var configFiles = DiscoverConfigFiles(tpRootPath);
                var contentTasks = configFiles
                    .Select(file => ReadConfigContentSafelyAsync(file, CancellationToken.None))
                    .ToList();
                var contents = (await Task.WhenAll(contentTasks))
                    .Where(c => c != null)
                    .ToList();

                var expectedSerial = contents
                    .SelectMany(c => ComRegex.Matches(c).Cast<Match>().Select(m => m.Value.ToUpperInvariant()))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                var expectedEndpoints = contents
                    .SelectMany(c => IpPortRegex.Matches(c).Cast<Match>().Select(m => m.Value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(15)
                    .ToList();

                var availablePorts = SerialPort.GetPortNames()
                    .Select(p => p.ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();
                var availablePortSet = new HashSet<string>(availablePorts, StringComparer.OrdinalIgnoreCase);

                var missingPorts = expectedSerial
                    .Where(p => !availablePortSet.Contains(p))
                    .ToList();

                var endpointStatuses = await Task.WhenAll(
                    expectedEndpoints.Select(endpoint => ProbeEndpointAsync(endpoint, CancellationToken.None)));

                return new TpConnectivitySnapshot
                {
                    TpRootPath = tpRootPath,
                    TpPathExists = true,
                    ConfigFiles = configFiles,
                    ExpectedSerialPorts = expectedSerial,
                    AvailableSerialPorts = availablePorts,
                    MissingSerialPorts = missingPorts,
                    NetworkEndpoints = endpointStatuses.ToList()
                };
            }
            catch (Exception ex)
            {
                return new TpConnectivitySnapshot
                {
                    TpRootPath = tpRootPath,
                    TpPathExists = true,
                    Error = ex.Message
                };
            }
        }

        private static bool TryGetCachedSnapshot(string tpRootPath, out TpConnectivitySnapshot snapshot)
        {
            lock (CacheSync)
            {
                return TryGetCachedSnapshotUnderLock(tpRootPath, out snapshot);
            }
        }

        private static bool TryGetCachedSnapshotUnderLock(string tpRootPath, out TpConnectivitySnapshot snapshot)
        {
            if (_cachedSnapshot != null &&
                string.Equals(_cachedTpRootPath, tpRootPath, StringComparison.OrdinalIgnoreCase) &&
                DateTimeOffset.UtcNow - _cachedAtUtc <= CacheTtl)
            {
                snapshot = _cachedSnapshot;
                return true;
            }

            snapshot = null;
            return false;
        }

        private static List<string> DiscoverConfigFiles(string rootPath)
        {
            var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var ext = Path.GetExtension(path);
                    if (!ext.Equals(".ini", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".xml", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".cfg", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var name = Path.GetFileName(path);
                    return name.IndexOf("config", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("configure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("device", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("system", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .Take(30)
                .ToList();

            return files;
        }

        private static string LoadConfigPath()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigRelativePath);
                if (!File.Exists(path))
                {
                    return DefaultTpRootPath;
                }

                var json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<TpConnectivityConfig>(json);
                return string.IsNullOrWhiteSpace(config != null ? config.TpRootPath : null) ? DefaultTpRootPath : config.TpRootPath;
            }
            catch
            {
                return DefaultTpRootPath;
            }
        }

        private static async Task<TpNetworkEndpointStatus> ProbeEndpointAsync(string endpoint, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var parts = endpoint.Split(new[] { ':' }, 2);
                var ip = parts[0];
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(EndpointProbeTimeoutMs);
                    if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                    {
                        using (var client = new TcpClient())
                        {
                            await client.ConnectAsync(ip, port);
                        }
                    }
                    else
                    {
                        using (var ping = new Ping())
                        {
                            var reply = await ping.SendPingAsync(ip, EndpointProbeTimeoutMs);
                            if (reply.Status != IPStatus.Success)
                            {
                                throw new InvalidOperationException($"Ping 状态: {reply.Status}");
                            }
                        }
                    }
                }

                sw.Stop();
                return new TpNetworkEndpointStatus
                {
                    Endpoint = endpoint,
                    Reachable = true,
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new TpNetworkEndpointStatus
                {
                    Endpoint = endpoint,
                    Reachable = false,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Error = ex.Message
                };
            }
        }

        private static async Task<string> ReadConfigContentSafelyAsync(string file, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await Task.Run(() => File.ReadAllText(file));
            }
            catch
            {
                return null;
            }
        }
    }
}
