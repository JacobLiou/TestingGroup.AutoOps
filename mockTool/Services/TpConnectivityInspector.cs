using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class TpConnectivityInspector
{
    private const string ConfigRelativePath = @"config\tpConnectivity.json";
    private const string DefaultTpRootPath = @"C:\Users\menghl2\WorkSpace\Projects\Test Program\cal_fts_fvs_fqc\RELEASE";
    private static readonly Regex ComRegex = new(@"\bCOM\d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IpPortRegex = new(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?::\d{1,5})?\b", RegexOptions.Compiled);

    private sealed class TpConnectivityConfig
    {
        public string TpRootPath { get; set; } = DefaultTpRootPath;
    }

    public async Task<TpConnectivitySnapshot> InspectAsync(CancellationToken cancellationToken)
    {
        var tpRootPath = LoadConfigPath();
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
            var contents = new List<string>();
            foreach (var file in configFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    contents.Add(await File.ReadAllTextAsync(file, cancellationToken));
                }
                catch
                {
                    // ignore unreadable file
                }
            }

            var expectedSerial = contents
                .SelectMany(c => ComRegex.Matches(c).Select(m => m.Value.ToUpperInvariant()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var expectedEndpoints = contents
                .SelectMany(c => IpPortRegex.Matches(c).Select(m => m.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(15)
                .ToList();

            var availablePorts = System.IO.Ports.SerialPort.GetPortNames()
                .Select(p => p.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var missingPorts = expectedSerial
                .Where(p => !availablePorts.Contains(p, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var endpointStatuses = new List<TpNetworkEndpointStatus>();
            foreach (var endpoint in expectedEndpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                endpointStatuses.Add(await ProbeEndpointAsync(endpoint, cancellationToken));
            }

            return new TpConnectivitySnapshot
            {
                TpRootPath = tpRootPath,
                TpPathExists = true,
                ConfigFiles = configFiles,
                ExpectedSerialPorts = expectedSerial,
                AvailableSerialPorts = availablePorts,
                MissingSerialPorts = missingPorts,
                NetworkEndpoints = endpointStatuses
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
                return name.Contains("config", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("configure", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("device", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("system", StringComparison.OrdinalIgnoreCase);
            })
            .Take(30)
            .ToList();

        return files;
    }

    private static string LoadConfigPath()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, ConfigRelativePath);
            if (!File.Exists(path))
            {
                return DefaultTpRootPath;
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<TpConnectivityConfig>(json);
            return string.IsNullOrWhiteSpace(config?.TpRootPath) ? DefaultTpRootPath : config.TpRootPath;
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
            var parts = endpoint.Split(':', 2);
            var ip = parts[0];
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                using var client = new TcpClient();
                await client.ConnectAsync(ip, port, cancellationToken);
            }
            else
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 1500);
                if (reply.Status != IPStatus.Success)
                {
                    throw new InvalidOperationException($"Ping 状态: {reply.Status}");
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
}
