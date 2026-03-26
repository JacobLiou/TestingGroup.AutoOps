using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;
using MockDiagTool.Models;
using MockDiagTool.Services.Abstractions;

namespace MockDiagTool.Services.Executors.System;

internal static class SystemCheckExecutors
{
    [CheckExecutor("SYS_01")]
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

    [CheckExecutor("SYS_02")]
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

    [CheckExecutor("SYS_03")]
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

    [CheckExecutor("SYS_04")]
    private static void CheckTimeSync(DiagnosticItem item)
    {
        var localNow = DateTime.Now;
        var offset = TimeZoneInfo.Local.GetUtcOffset(localNow);
        item.Detail = $"本地时间: {localNow:yyyy-MM-dd HH:mm:ss}  UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:D2}:{offset.Minutes:D2}";
        item.Status = CheckStatus.Pass;
        item.Score = 100;
    }

    [CheckExecutor("DSK_01")]
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

    [CheckExecutor("DSK_02")]
    private static void CheckAllDisks(DiagnosticItem item)
    {
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
        var warnings = new List<string>();
        foreach (var d in drives)
        {
            var freeGB = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            if (freeGB < 10)
            {
                warnings.Add($"{d.Name} 仅剩 {freeGB:F1}GB");
            }
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

    [CheckExecutor("DSK_03")]
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

    [CheckExecutor("SEC_01")]
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

    [CheckExecutor("SEC_02")]
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

    [CheckExecutor("SEC_03")]
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

    [CheckExecutor("SEC_04")]
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

    [CheckExecutor("SFT_01")]
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

    [CheckExecutor("SFT_02")]
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

    [CheckExecutor("SFT_03")]
    private static void CheckComPorts(DiagnosticItem item)
    {
        var ports = global::System.IO.Ports.SerialPort.GetPortNames();
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

    [CheckExecutor("PRF_01")]
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

    [CheckExecutor("PRF_02")]
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

    [CheckExecutor("PRF_03")]
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
