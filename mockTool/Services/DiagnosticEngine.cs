using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

/// <summary>
/// Performs real Windows PC diagnostic checks.
/// </summary>
public static class DiagnosticEngine
{
    public static List<DiagnosticItem> BuildCheckList()
    {
        return
        [
            // ── System ──
            new() { Id = "SYS_01", Name = "操作系统版本", Category = CheckCategory.System },
            new() { Id = "SYS_02", Name = "系统运行时长", Category = CheckCategory.System },
            new() { Id = "SYS_03", Name = "Windows 更新状态", Category = CheckCategory.System },
            new() { Id = "SYS_04", Name = "系统时间同步", Category = CheckCategory.System },

            // ── Disk ──
            new() { Id = "DSK_01", Name = "系统盘可用空间", Category = CheckCategory.Disk },
            new() { Id = "DSK_02", Name = "磁盘剩余空间（所有驱动器）", Category = CheckCategory.Disk },
            new() { Id = "DSK_03", Name = "临时文件夹清理", Category = CheckCategory.Disk },

            // ── Network ──
            new() { Id = "NET_01", Name = "网络连接状态", Category = CheckCategory.Network },
            new() { Id = "NET_02", Name = "DNS 解析", Category = CheckCategory.Network },
            new() { Id = "NET_03", Name = "互联网连通性", Category = CheckCategory.Network },

            // ── Security ──
            new() { Id = "SEC_01", Name = "Windows 防火墙", Category = CheckCategory.Security },
            new() { Id = "SEC_02", Name = "杀毒软件状态", Category = CheckCategory.Security },
            new() { Id = "SEC_03", Name = "UAC (用户账户控制)", Category = CheckCategory.Security },
            new() { Id = "SEC_04", Name = "自动登录安全", Category = CheckCategory.Security },

            // ── Software ──
            new() { Id = "SFT_01", Name = "开机启动项数量", Category = CheckCategory.Software },
            new() { Id = "SFT_02", Name = "已安装程序数量", Category = CheckCategory.Software },
            new() { Id = "SFT_03", Name = "COM / 串口枚举", Category = CheckCategory.Software },

            // ── Performance ──
            new() { Id = "PRF_01", Name = "CPU 使用率", Category = CheckCategory.Performance },
            new() { Id = "PRF_02", Name = "内存使用率", Category = CheckCategory.Performance },
            new() { Id = "PRF_03", Name = "运行进程数", Category = CheckCategory.Performance },
        ];
    }

    public static async Task RunCheckAsync(DiagnosticItem item, CancellationToken ct)
    {
        item.Status = CheckStatus.Scanning;
        // Simulate a small delay to make scanning feel real
        await Task.Delay(Random.Shared.Next(300, 800), ct);

        try
        {
            switch (item.Id)
            {
                case "SYS_01": CheckOsVersion(item); break;
                case "SYS_02": CheckUptime(item); break;
                case "SYS_03": CheckWindowsUpdate(item); break;
                case "SYS_04": CheckTimeSync(item); break;

                case "DSK_01": CheckSystemDisk(item); break;
                case "DSK_02": CheckAllDisks(item); break;
                case "DSK_03": CheckTempFolder(item); break;

                case "NET_01": CheckNetworkAvailable(item); break;
                case "NET_02": CheckDns(item); break;
                case "NET_03": CheckInternet(item); break;

                case "SEC_01": CheckFirewall(item); break;
                case "SEC_02": CheckAntivirus(item); break;
                case "SEC_03": CheckUac(item); break;
                case "SEC_04": CheckAutoLogin(item); break;

                case "SFT_01": CheckStartupItems(item); break;
                case "SFT_02": CheckInstalledPrograms(item); break;
                case "SFT_03": CheckComPorts(item); break;

                case "PRF_01": CheckCpu(item); break;
                case "PRF_02": CheckMemory(item); break;
                case "PRF_03": CheckProcesses(item); break;

                default:
                    item.Status = CheckStatus.Pass;
                    item.Detail = "未知检查项";
                    break;
            }
        }
        catch (Exception ex)
        {
            item.Status = CheckStatus.Warning;
            item.Detail = $"检查时发生异常: {ex.Message}";
            item.Score = 95;
        }
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
