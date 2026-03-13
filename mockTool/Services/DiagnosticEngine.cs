using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using MockDiagTool.Models;
using MockDiagTool.Services.Abstractions;

namespace MockDiagTool.Services;

public static class DiagnosticEngine
{
    private static readonly RunbookProvider RunbookProvider = new();
    private static readonly ExternalDependencyHttpChecker ExternalChecker = new();
    private static readonly DeviceVersionComplianceChecker VersionComplianceChecker = new();
    private static readonly StationCapabilityComplianceChecker StationCapabilityComplianceChecker = new();
    private static readonly PowerSupplyQualityChecker PowerSupplyQualityChecker = new();
    private static readonly CheckExecutorRegistry ExecutorRegistry = BuildExecutorRegistry();

    public static RunbookDefinition LoadRunbook()
    {
        return RunbookProvider.Load();
    }

    public static List<DiagnosticItem> BuildCheckList(RunbookDefinition? runbook = null)
    {
        runbook ??= LoadRunbook();
        return runbook.Steps
            .Where(s => s.Enabled)
            .Select(s => new DiagnosticItem
            {
                Id = s.CheckId,
                Name = s.DisplayName,
                Category = ParseCategory(s.Category)
            })
            .ToList();
    }

    public static async Task<CheckExecutionOutcome> RunCheckAsync(
        DiagnosticItem item,
        RunbookStepDefinition step,
        DiagnosticRunContext? runContext,
        CancellationToken ct)
    {
        item.Status = CheckStatus.Scanning;
        await Task.Delay(Random.Shared.Next(200, 450), ct);

        var executor = ExecutorRegistry.Resolve(step.CheckId);
        if (executor is null)
        {
            item.Status = CheckStatus.Warning;
            item.Detail = $"未注册的检查项: {step.CheckId}";
            item.Score = 95;
            return new CheckExecutionOutcome { Success = false };
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (step.TimeoutMs > 0)
        {
            linkedCts.CancelAfter(step.TimeoutMs);
        }

        try
        {
            return await executor.ExecuteAsync(item, step, runContext, linkedCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            item.Status = CheckStatus.Fail;
            item.Detail = $"检查超时（>{step.TimeoutMs}ms）";
            item.Score = 60;
            return new CheckExecutionOutcome { Success = false };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            item.Status = CheckStatus.Warning;
            item.Detail = $"检查时发生异常: {ex.Message}";
            item.Score = 95;
            return new CheckExecutionOutcome { Success = false };
        }
    }

    private static CheckExecutorRegistry BuildExecutorRegistry()
    {
        var registry = new CheckExecutorRegistry();

        RegisterSync(registry, "SYS_01", CheckOsVersion);
        RegisterSync(registry, "SYS_02", CheckUptime);
        RegisterSync(registry, "SYS_03", CheckWindowsUpdate);
        RegisterSync(registry, "SYS_04", CheckTimeSync);
        RegisterSync(registry, "DSK_01", CheckSystemDisk);
        RegisterSync(registry, "DSK_02", CheckAllDisks);
        RegisterSync(registry, "DSK_03", CheckTempFolder);
        RegisterSync(registry, "NET_01", CheckNetworkAvailable);
        RegisterSync(registry, "NET_02", CheckDns);
        RegisterSync(registry, "NET_03", CheckInternet);
        RegisterSync(registry, "SEC_01", CheckFirewall);
        RegisterSync(registry, "SEC_02", CheckAntivirus);
        RegisterSync(registry, "SEC_03", CheckUac);
        RegisterSync(registry, "SEC_04", CheckAutoLogin);
        RegisterSync(registry, "SFT_01", CheckStartupItems);
        RegisterSync(registry, "SFT_02", CheckInstalledPrograms);
        RegisterSync(registry, "SFT_03", CheckComPorts);
        RegisterSync(registry, "PRF_01", CheckCpu);
        RegisterSync(registry, "PRF_02", CheckMemory);
        RegisterSync(registry, "PRF_03", CheckProcesses);

        RegisterExternal(registry, ExternalDependencyIds.Mes);
        RegisterExternal(registry, ExternalDependencyIds.Tms);
        RegisterExternal(registry, ExternalDependencyIds.Tas);
        RegisterExternal(registry, ExternalDependencyIds.FileServer);
        RegisterExternal(registry, ExternalDependencyIds.Lan);
        RegisterTp(registry, TpCheckIds.PathAndConfig);
        RegisterTp(registry, TpCheckIds.SerialPorts);
        RegisterTp(registry, TpCheckIds.NetworkEndpoints);
        RegisterTp(registry, TpCheckIds.VersionCompliance);
        RegisterTp(registry, TpCheckIds.StationCapabilityCompliance);
        RegisterTp(registry, TpCheckIds.PowerSupplyQuality);

        return registry;
    }

    private static void RegisterSync(CheckExecutorRegistry registry, string checkId, Action<DiagnosticItem> action)
    {
        registry.Register(new DelegateCheckExecutor(checkId, (item, _, _, _) =>
        {
            action(item);
            return Task.FromResult(new CheckExecutionOutcome { Success = IsSuccessful(item.Status) });
        }));
    }

    private static void RegisterExternal(CheckExecutorRegistry registry, string dependencyId)
    {
        registry.Register(new DelegateCheckExecutor(dependencyId, async (item, step, runContext, ct) =>
        {
            var resolvedDependencyId = step.Params.TryGetValue("dependencyId", out var id) && !string.IsNullOrWhiteSpace(id)
                ? id
                : dependencyId;
            await CheckExternalApiAsync(item, resolvedDependencyId, runContext, ct);
            return new CheckExecutionOutcome { Success = IsSuccessful(item.Status) };
        }));
    }

    private static void RegisterTp(CheckExecutorRegistry registry, string checkId)
    {
        registry.Register(new DelegateCheckExecutor(checkId, async (item, step, runContext, ct) =>
        {
            var snapshot = runContext?.TpConnectivity;
            if (snapshot is null && checkId != TpCheckIds.VersionCompliance && checkId != TpCheckIds.StationCapabilityCompliance && checkId != TpCheckIds.PowerSupplyQuality)
            {
                item.Status = CheckStatus.Warning;
                item.Detail = "未获取 TP 连接检查快照";
                item.Score = 90;
                return new CheckExecutionOutcome { Success = false };
            }
            var tpSnapshot = snapshot!;

            if (checkId == TpCheckIds.PathAndConfig)
            {
                if (!tpSnapshot.TpPathExists)
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"TP 路径不可用: {tpSnapshot.TpRootPath}";
                    item.FixSuggestion = "确认 TP 路径和目录权限";
                    item.Score = 60;
                }
                else
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"TP 路径有效，发现配置文件 {tpSnapshot.ConfigFiles.Count} 个";
                    item.Score = 100;
                }
            }
            else if (checkId == TpCheckIds.SerialPorts)
            {
                if (!tpSnapshot.TpPathExists)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "TP 路径不可用，跳过串口检查";
                    item.Score = 90;
                }
                else if (tpSnapshot.ExpectedSerialPorts.Count == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "未在 TP 配置中识别到串口映射";
                    item.Score = 90;
                }
                else if (tpSnapshot.MissingSerialPorts.Count == 0)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"串口映射正常: {string.Join(", ", tpSnapshot.ExpectedSerialPorts)}";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"缺失串口: {string.Join(", ", tpSnapshot.MissingSerialPorts)}";
                    item.FixSuggestion = "检查串口设备连接、驱动与 COM 口映射";
                    item.Score = 65;
                }
            }
            else if (checkId == TpCheckIds.NetworkEndpoints)
            {
                if (!tpSnapshot.TpPathExists)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "TP 路径不可用，跳过网口检查";
                    item.Score = 90;
                }
                else if (tpSnapshot.NetworkEndpoints.Count == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "未在 TP 配置中识别到网口目标";
                    item.Score = 90;
                }
                else
                {
                    var failed = tpSnapshot.NetworkEndpoints.Where(e => !e.Reachable).ToList();
                    if (failed.Count == 0)
                    {
                        item.Status = CheckStatus.Pass;
                        item.Detail = $"网口目标全部可达，共 {tpSnapshot.NetworkEndpoints.Count} 个";
                        item.Score = 100;
                    }
                    else
                    {
                        item.Status = CheckStatus.Fail;
                        item.Detail = $"不可达目标 {failed.Count} 个: {string.Join("; ", failed.Select(f => $"{f.Endpoint}({f.Error})"))}";
                        item.FixSuggestion = "检查网络连通、防火墙、交换机/VLAN 与目标服务状态";
                        item.Score = 65;
                    }
                }
            }
            else if (checkId == TpCheckIds.VersionCompliance)
            {
                var result = await VersionComplianceChecker.CheckAsync(step, runContext, ct);
                if (!result.ApiSuccess)
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"TMS 版本要求获取失败: {result.ApiMessage}";
                    item.FixSuggestion = "检查 TMS API 地址、权限与接口可用性";
                    item.Score = 60;
                }
                else if (result.Requirements.Count == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = $"TMS 未返回版本要求: {result.RequirementUrl}";
                    item.FixSuggestion = "确认 TMS 版本配置是否已维护";
                    item.Score = 90;
                }
                else if (result.Mismatches.Count == 0)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"版本符合要求: {result.Requirements.Count} 项匹配（TMS: {result.RequirementUrl}）";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"版本不匹配 {result.Mismatches.Count} 项: {string.Join("; ", result.Mismatches.Select(m => m.MissingActual ? $"{m.DeviceKey}:缺少实际版本(要求{m.RequiredVersion})" : $"{m.DeviceKey}:实际{m.ActualVersion}/要求{m.RequiredVersion}"))}";
                    item.FixSuggestion = "更新设备固件/版本或同步 TMS 目标版本配置";
                    item.Score = 65;
                }
            }
            else if (checkId == TpCheckIds.StationCapabilityCompliance)
            {
                var result = StationCapabilityComplianceChecker.Check(runContext);
                var failed = result.Metrics.Where(m => !m.Pass).ToList();
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"工位能力指标全部满足要求，共 {result.Metrics.Count} 项（数据源: {result.ActualSource}）";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = failed.Count == 0
                        ? $"工位能力要求不可用或数据缺失（数据源: {result.ActualSource}）"
                        : $"不满足 {failed.Count} 项（数据源: {result.ActualSource}）: {string.Join("; ", failed.Select(f => $"{f.Metric} 实际{f.Actual} / 要求{f.Required}"))}";
                    item.FixSuggestion = "检查工位实测数据、治具状态与 MIMS 下发要求";
                    item.Score = 65;
                }
            }
            else if (checkId == TpCheckIds.PowerSupplyQuality)
            {
                var result = await PowerSupplyQualityChecker.CheckAsync(runContext, ct);
                var curve = string.Join(", ", result.Samples.Select(s => s.VoltageV.ToString("F3")));
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"电源电压质量合格（{result.Source}）| 均值{result.MeanV:F3}V 标准差{result.StdDevV:F4}V 纹波{result.RippleV:F4}V | 曲线: [{curve}]";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"电源电压质量不满足要求（{result.Source}）| 均值{result.MeanV:F3}V 标准差{result.StdDevV:F4}V 纹波{result.RippleV:F4}V | {string.Join(" | ", result.FailReasons)} | 曲线: [{curve}]";
                    item.FixSuggestion = "检查电源模块、负载波动、采样链路和 TP 采集接口";
                    item.Score = 60;
                }
            }

            return new CheckExecutionOutcome { Success = IsSuccessful(item.Status) };
        }));
    }

    private static bool IsSuccessful(CheckStatus status)
    {
        return status is CheckStatus.Pass or CheckStatus.Fixed;
    }

    private static CheckCategory ParseCategory(string category)
    {
        return Enum.TryParse<CheckCategory>(category, ignoreCase: true, out var parsed)
            ? parsed
            : CheckCategory.SystemCheck;
    }

    // ────────── System Checks ──────────

    private static void CheckOsVersion(DiagnosticItem item)
    {
        var os = Environment.OSVersion;
        var ver = os.VersionString;
        item.Detail = $"{ver}";
        if (os.Version.Major >= 10)
        {
            item.Status = CheckStatus.Pass;
            item.Score = 100;
        }
        else
        {
            item.Status = CheckStatus.Warning;
            item.Detail += " — 建议升级到 Windows 10 及以上";
            item.FixSuggestion = "升级操作系统到 Windows 10/11";
            item.Score = 90;
        }
    }

    private static void CheckUptime(DiagnosticItem item)
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        item.Detail = $"已运行 {uptime.Days} 天 {uptime.Hours} 小时 {uptime.Minutes} 分钟";
        if (uptime.TotalDays > 7)
        {
            item.Status = CheckStatus.Warning;
            item.FixSuggestion = "建议定期重启电脑以释放资源";
            item.Score = 92;
        }
        else
        {
            item.Status = CheckStatus.Pass;
            item.Score = 100;
        }
    }

    private static void CheckWindowsUpdate(DiagnosticItem item)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
            if (key != null)
            {
                var lastStr = key.GetValue("LastSuccessTime") as string;
                if (DateTime.TryParse(lastStr, out var lastUpdate))
                {
                    var days = (DateTime.Now - lastUpdate).TotalDays;
                    item.Detail = $"上次更新: {lastUpdate:yyyy-MM-dd} ({days:F0} 天前)";
                    if (days > 30)
                    {
                        item.Status = CheckStatus.Warning;
                        item.FixSuggestion = "建议检查并安装最新 Windows 更新";
                        item.Score = 90;
                    }
                    else
                    {
                        item.Status = CheckStatus.Pass;
                        item.Score = 100;
                    }
                    return;
                }
            }
            item.Status = CheckStatus.Pass;
            item.Detail = "Windows 更新服务正常";
            item.Score = 100;
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "无法读取更新状态，跳过";
            item.Score = 98;
        }
    }

    private static void CheckTimeSync(DiagnosticItem item)
    {
        var utcNow = DateTime.UtcNow;
        var localNow = DateTime.Now;
        var offset = TimeZoneInfo.Local.GetUtcOffset(localNow);
        item.Detail = $"本地时间: {localNow:yyyy-MM-dd HH:mm:ss}  UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:D2}:{offset.Minutes:D2}";
        item.Status = CheckStatus.Pass;
        item.Score = 100;
    }

    // ────────── Disk Checks ──────────

    private static void CheckSystemDisk(DiagnosticItem item)
    {
        var sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var di = new DriveInfo(sysDrive);
        var freeGB = di.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        var totalGB = di.TotalSize / (1024.0 * 1024 * 1024);
        var usedPct = (1 - freeGB / totalGB) * 100;
        item.Detail = $"{sysDrive} 可用 {freeGB:F1} GB / 共 {totalGB:F1} GB (已用 {usedPct:F0}%)";

        if (freeGB < 5)
        {
            item.Status = CheckStatus.Fail;
            item.FixSuggestion = "系统盘空间不足 5GB，请清理磁盘或扩容";
            item.Score = 70;
        }
        else if (freeGB < 20)
        {
            item.Status = CheckStatus.Warning;
            item.FixSuggestion = "系统盘空间低于 20GB，建议清理不需要的文件";
            item.Score = 88;
        }
        else
        {
            item.Status = CheckStatus.Pass;
            item.Score = 100;
        }
    }

    private static void CheckAllDisks(DiagnosticItem item)
    {
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
        var warnings = new List<string>();
        foreach (var d in drives)
        {
            var freeGB = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            if (freeGB < 10) warnings.Add($"{d.Name} 仅剩 {freeGB:F1}GB");
        }
        item.Detail = $"检测到 {drives.Count} 个固定磁盘";
        if (warnings.Count > 0)
        {
            item.Status = CheckStatus.Warning;
            item.Detail += $" | 低空间: {string.Join(", ", warnings)}";
            item.FixSuggestion = "清理磁盘空间或移动数据到其他驱动器";
            item.Score = 85;
        }
        else
        {
            item.Status = CheckStatus.Pass;
            item.Detail += "，空间充足";
            item.Score = 100;
        }
    }

    private static void CheckTempFolder(DiagnosticItem item)
    {
        var tempPath = Path.GetTempPath();
        try
        {
            var tempDir = new DirectoryInfo(tempPath);
            var files = tempDir.GetFiles("*", SearchOption.TopDirectoryOnly);
            var sizeMB = files.Sum(f => f.Length) / (1024.0 * 1024);
            item.Detail = $"临时文件夹: {files.Length} 个文件，共 {sizeMB:F1} MB";
            if (sizeMB > 500)
            {
                item.Status = CheckStatus.Warning;
                item.FixSuggestion = "临时文件超过 500MB，建议清理";
                item.Score = 90;
            }
            else
            {
                item.Status = CheckStatus.Pass;
                item.Score = 100;
            }
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "临时文件夹正常";
            item.Score = 100;
        }
    }

    // ────────── Network Checks ──────────

    private static void CheckNetworkAvailable(DiagnosticItem item)
    {
        if (NetworkInterface.GetIsNetworkAvailable())
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();
            item.Status = CheckStatus.Pass;
            item.Detail = $"网络已连接，{nics.Count} 个活动适配器";
            item.Score = 100;
        }
        else
        {
            item.Status = CheckStatus.Fail;
            item.Detail = "无网络连接";
            item.FixSuggestion = "检查网线或 Wi-Fi 连接";
            item.Score = 60;
        }
    }

    private static void CheckDns(DiagnosticItem item)
    {
        try
        {
            var addresses = Dns.GetHostAddresses("www.baidu.com");
            item.Status = CheckStatus.Pass;
            item.Detail = $"DNS 解析正常 (www.baidu.com → {addresses.FirstOrDefault()})";
            item.Score = 100;
        }
        catch
        {
            item.Status = CheckStatus.Fail;
            item.Detail = "DNS 解析失败";
            item.FixSuggestion = "检查 DNS 设置，尝试使用 8.8.8.8";
            item.Score = 65;
        }
    }

    private static void CheckInternet(DiagnosticItem item)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("8.8.8.8", 3000);
            if (reply.Status == IPStatus.Success)
            {
                item.Status = CheckStatus.Pass;
                item.Detail = $"互联网连通，延迟 {reply.RoundtripTime}ms";
                item.Score = 100;
            }
            else
            {
                item.Status = CheckStatus.Warning;
                item.Detail = $"Ping 失败: {reply.Status}";
                item.FixSuggestion = "检查网络防火墙或代理设置";
                item.Score = 80;
            }
        }
        catch
        {
            item.Status = CheckStatus.Warning;
            item.Detail = "无法 Ping 外网（可能被防火墙拦截）";
            item.FixSuggestion = "检查防火墙是否拦截了 ICMP";
            item.Score = 85;
        }
    }

    // ────────── Security Checks ──────────

    // ────────── External System Checks ──────────

    private static async Task CheckExternalApiAsync(
        DiagnosticItem item,
        string dependencyId,
        DiagnosticRunContext? runContext,
        CancellationToken ct)
    {
        if (runContext is null || !runContext.ExternalChecksEnabled || runContext.ExternalConfig is null)
        {
            item.Status = CheckStatus.Warning;
            item.Detail = string.IsNullOrWhiteSpace(runContext?.ConfigError)
                ? "未获取外部系统配置，已跳过该项"
                : $"外部系统配置不可用: {runContext.ConfigError}";
            item.Score = 90;
            return;
        }

        try
        {
            var result = await ExternalChecker.CheckAsync(dependencyId, runContext.ExternalConfig, ct);
            var statusLabel = result.StatusCode.HasValue ? $"HTTP {result.StatusCode.Value}" : "NO_HTTP_STATUS";
            if (result.Success)
            {
                item.Status = CheckStatus.Pass;
                item.Detail = $"{result.EndpointName} POST {result.Url} -> {statusLabel} ({result.ElapsedMs}ms)";
                item.Score = 100;
            }
            else
            {
                item.Status = CheckStatus.Fail;
                item.Detail = string.IsNullOrWhiteSpace(result.Error)
                    ? $"{result.EndpointName} POST {result.Url} -> {statusLabel} ({result.ElapsedMs}ms)"
                    : $"{result.EndpointName} POST {result.Url} -> {statusLabel} ({result.ElapsedMs}ms) | {result.Error}";
                item.FixSuggestion = $"检查 {result.EndpointName} 服务状态、网关与路由配置";
                item.Score = 70;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            item.Status = CheckStatus.Fail;
            item.Detail = $"外部接口调用失败: {ex.Message}";
            item.FixSuggestion = "检查外部系统地址可达性与服务进程";
            item.Score = 60;
        }
    }

    private static void CheckFirewall(DiagnosticItem item)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            var enabled = key?.GetValue("EnableFirewall");
            if (enabled is int val && val == 1)
            {
                item.Status = CheckStatus.Pass;
                item.Detail = "Windows 防火墙已启用";
                item.Score = 100;
            }
            else
            {
                item.Status = CheckStatus.Fail;
                item.Detail = "Windows 防火墙未启用";
                item.FixSuggestion = "请启用 Windows 防火墙以保护系统";
                item.Score = 60;
            }
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "防火墙状态检测完成";
            item.Score = 100;
        }
    }

    private static void CheckAntivirus(DiagnosticItem item)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
            var products = searcher.Get();
            var names = new List<string>();
            foreach (ManagementObject obj in products)
            {
                names.Add(obj["displayName"]?.ToString() ?? "Unknown");
            }
            if (names.Count > 0)
            {
                item.Status = CheckStatus.Pass;
                item.Detail = $"检测到杀毒软件: {string.Join(", ", names)}";
                item.Score = 100;
            }
            else
            {
                item.Status = CheckStatus.Warning;
                item.Detail = "未检测到杀毒软件";
                item.FixSuggestion = "建议启用 Windows Defender 或安装杀毒软件";
                item.Score = 75;
            }
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "杀毒软件检测完成（服务器版本可能无 SecurityCenter）";
            item.Score = 98;
        }
    }

    private static void CheckUac(DiagnosticItem item)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var uac = key?.GetValue("EnableLUA");
            if (uac is int val && val == 1)
            {
                item.Status = CheckStatus.Pass;
                item.Detail = "UAC 已启用";
                item.Score = 100;
            }
            else
            {
                item.Status = CheckStatus.Warning;
                item.Detail = "UAC 已关闭，存在安全风险";
                item.FixSuggestion = "建议开启用户账户控制（UAC）";
                item.Score = 80;
            }
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "UAC 检测完成";
            item.Score = 100;
        }
    }

    private static void CheckAutoLogin(DiagnosticItem item)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon");
            var autoAdmin = key?.GetValue("AutoAdminLogon") as string;
            if (autoAdmin == "1")
            {
                item.Status = CheckStatus.Warning;
                item.Detail = "自动登录已启用，存在安全风险";
                item.FixSuggestion = "禁用自动登录以提升安全性";
                item.Score = 82;
            }
            else
            {
                item.Status = CheckStatus.Pass;
                item.Detail = "未启用自动登录";
                item.Score = 100;
            }
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "自动登录检测完成";
            item.Score = 100;
        }
    }

    // ────────── Software Checks ──────────

    private static void CheckStartupItems(DiagnosticItem item)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            var count = key?.GetValueNames().Length ?? 0;

            using var key2 = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            count += key2?.GetValueNames().Length ?? 0;

            item.Detail = $"检测到 {count} 个开机启动项";
            if (count > 10)
            {
                item.Status = CheckStatus.Warning;
                item.FixSuggestion = $"启动项过多（{count}个），建议禁用不必要的启动项";
                item.Score = 85;
            }
            else
            {
                item.Status = CheckStatus.Pass;
                item.Score = 100;
            }
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "启动项检测完成";
            item.Score = 100;
        }
    }

    private static void CheckInstalledPrograms(DiagnosticItem item)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            var count = key?.GetSubKeyNames().Length ?? 0;
            item.Detail = $"检测到约 {count} 个已安装程序";
            item.Status = CheckStatus.Pass;
            item.Score = 100;
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "程序检测完成";
            item.Score = 100;
        }
    }

    private static void CheckComPorts(DiagnosticItem item)
    {
        var ports = System.IO.Ports.SerialPort.GetPortNames();
        if (ports.Length > 0)
        {
            item.Detail = $"检测到 {ports.Length} 个串口: {string.Join(", ", ports)}";
        }
        else
        {
            item.Detail = "未检测到串口";
        }
        item.Status = CheckStatus.Pass;
        item.Score = 100;
    }

    // ────────── Performance Checks ──────────

    private static void CheckCpu(DiagnosticItem item)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LoadPercentage FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var load = Convert.ToInt32(obj["LoadPercentage"]);
                item.Detail = $"CPU 当前负载: {load}%";
                if (load > 90)
                {
                    item.Status = CheckStatus.Fail;
                    item.FixSuggestion = "CPU 负载过高，检查高占用进程";
                    item.Score = 65;
                }
                else if (load > 70)
                {
                    item.Status = CheckStatus.Warning;
                    item.FixSuggestion = "CPU 负载较高，建议关闭不必要的程序";
                    item.Score = 85;
                }
                else
                {
                    item.Status = CheckStatus.Pass;
                    item.Score = 100;
                }
                return;
            }
            item.Status = CheckStatus.Pass;
            item.Detail = "CPU 正常";
            item.Score = 100;
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "CPU 检测完成";
            item.Score = 100;
        }
    }

    private static void CheckMemory(DiagnosticItem item)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                var freeKB = Convert.ToDouble(obj["FreePhysicalMemory"]);
                var totalGB = totalKB / (1024 * 1024);
                var usedGB = (totalKB - freeKB) / (1024 * 1024);
                var usedPct = (totalKB - freeKB) / totalKB * 100;

                item.Detail = $"内存使用: {usedGB:F1} GB / {totalGB:F1} GB ({usedPct:F0}%)";
                if (usedPct > 90)
                {
                    item.Status = CheckStatus.Fail;
                    item.FixSuggestion = "内存使用率过高，建议关闭无用程序或增加内存";
                    item.Score = 65;
                }
                else if (usedPct > 75)
                {
                    item.Status = CheckStatus.Warning;
                    item.FixSuggestion = "内存使用率较高";
                    item.Score = 85;
                }
                else
                {
                    item.Status = CheckStatus.Pass;
                    item.Score = 100;
                }
                return;
            }
            item.Status = CheckStatus.Pass;
            item.Score = 100;
        }
        catch
        {
            item.Status = CheckStatus.Pass;
            item.Detail = "内存检测完成";
            item.Score = 100;
        }
    }

    private static void CheckProcesses(DiagnosticItem item)
    {
        var procs = Process.GetProcesses();
        item.Detail = $"当前运行 {procs.Length} 个进程";
        if (procs.Length > 200)
        {
            item.Status = CheckStatus.Warning;
            item.FixSuggestion = "进程数过多，建议检查是否有异常进程";
            item.Score = 88;
        }
        else
        {
            item.Status = CheckStatus.Pass;
            item.Score = 100;
        }
    }
}
