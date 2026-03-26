using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;

namespace SelfDiagnostic.Services.Executors.Network
{
    /// <summary>
    /// 网络检查执行器集合（主项目副本）— 检测网络连通性、DNS、互联网可达性。
    /// </summary>
    internal static class NetworkCheckExecutors
    {
        [CheckExecutor("NET_01")]
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

        [CheckExecutor("NET_02")]
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

        [CheckExecutor("NET_03")]
        private static void CheckInternet(DiagnosticItem item)
        {
            try
            {
                using (var ping = new Ping())
                {
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
            }
            catch
            {
                item.Status = CheckStatus.Warning;
                item.Detail = "无法 Ping 外网（可能被防火墙拦截）";
                item.FixSuggestion = "检查防火墙是否拦截了 ICMP";
                item.Score = 85;
            }
        }
    }
}
