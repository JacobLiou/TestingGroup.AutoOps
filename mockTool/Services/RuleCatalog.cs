using MockDiagTool.Models;

namespace MockDiagTool.Services;

public static class RuleCatalog
{
    private static readonly Dictionary<string, RuleMetadata> Entries = BuildEntries();

    public static RuleMetadata Resolve(string checkId)
    {
        if (Entries.TryGetValue(checkId, out var metadata))
        {
            return metadata;
        }

        return new RuleMetadata
        {
            CheckId = checkId,
            RuleCode = $"GEN-{checkId}",
            Domain = "GEN",
            Category = "GEN",
            Severity = RuleSeverity.S1,
            Threshold = "N/A",
            DefaultFailReason = "规则未维护标准失败原因",
            DefaultAction = "请补充规则元数据后重新评估",
            EscalationPath = "联系诊断规则维护人"
        };
    }

    private static Dictionary<string, RuleMetadata> BuildEntries()
    {
        return new Dictionary<string, RuleMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["NET_01"] = Make("NET_01", "SYS-NET-010", "SYS", "NET", RuleSeverity.S0, "网络可达率=100%", "网络不可达或适配器异常", "检查网线/Wi-Fi/交换机并重连", "10分钟未恢复升级IT网络"),
            ["NET_02"] = Make("NET_02", "SYS-NET-011", "SYS", "NET", RuleSeverity.S1, "DNS解析成功", "DNS解析失败", "检查DNS服务器与hosts配置", "升级IT网络与域管理员"),
            ["NET_03"] = Make("NET_03", "SYS-NET-012", "SYS", "NET", RuleSeverity.S1, "Ping延迟<=100ms", "外网连通不稳定", "检查防火墙/代理策略", "升级网络与安全管理员"),
            ["EXT_MES"] = Make("EXT_MES", "SYS-API-010", "SYS", "API", RuleSeverity.S0, "HTTP 200且响应<1s", "MES接口不可用", "核对接口地址、token与服务状态", "升级MES接口值班"),
            ["EXT_TMS"] = Make("EXT_TMS", "SYS-API-011", "SYS", "API", RuleSeverity.S0, "HTTP 200且响应<1s", "TMS接口不可用", "检查TMS网关与鉴权", "升级TMS服务值班"),
            ["EXT_TAS"] = Make("EXT_TAS", "SYS-API-010", "SYS", "API", RuleSeverity.S1, "HTTP 200", "TAS接口不可用", "检查TAS服务和网络策略", "升级TAS维护人"),
            ["EXT_FILE_SERVER"] = Make("EXT_FILE_SERVER", "SYS-NET-001", "SYS", "NET", RuleSeverity.S0, "共享路径可读写", "文件服务不可达或权限异常", "确认路径权限与账号权限", "升级文件服务管理员"),
            ["EXT_LAN"] = Make("EXT_LAN", "SYS-NET-005", "SYS", "NET", RuleSeverity.S1, "网关可达", "局域网网关不可达", "检查VLAN和网关配置", "升级网络值班"),
            ["TP_01"] = Make("TP_01", "SYS-CFG-001", "SYS", "CFG", RuleSeverity.S0, "TP路径与配置文件存在", "TP路径或配置不可读", "检查TP部署路径与目录权限", "升级测试平台管理员"),
            ["TP_02"] = Make("TP_02", "STA-HW-001", "STA", "HW", RuleSeverity.S0, "串口映射完整", "串口缺失或映射错误", "检查串口设备连接和驱动", "升级设备工程师"),
            ["TP_03"] = Make("TP_03", "STA-NET-001", "STA", "NET", RuleSeverity.S0, "目标端口可达率=100%", "网口目标不可达", "检查目标服务监听与防火墙", "升级网络与设备工程师"),
            ["TP_04"] = Make("TP_04", "SYS-CFG-002", "SYS", "CFG", RuleSeverity.S0, "版本匹配率=100%", "设备版本与要求不一致", "同步固件版本与目标版本配置", "升级工艺与设备双签核"),
            ["TP_05"] = Make("TP_05", "STA-OPT-004", "STA", "OPT", RuleSeverity.S1, "GRR/GDS/SNR满足要求", "工位能力指标未达标", "复测并校准光学参数", "升级视觉/光学工程师"),
            ["TP_06"] = Make("TP_06", "STA-OPT-002", "STA", "OPT", RuleSeverity.S0, "StdDev<=0.06V, Ripple<=0.25V", "电源稳定性不达标", "检查电源模块与采样链路", "升级设备维护"),
            ["TP_07"] = Make("TP_07", "SYS-CFG-010", "SYS", "CFG", RuleSeverity.S1, "默认信息和LUT完整", "默认信息或LUT缺失/损坏", "重新下发默认信息并校验LUT", "升级系统配置管理员"),
            ["TP_08"] = Make("TP_08", "SYS-CFG-011", "SYS", "CFG", RuleSeverity.S1, "配置完整性通过", "配置或损坏数据异常", "修复损坏数据并重跑自检", "升级配置管理员"),
            ["TP_09"] = Make("TP_09", "STA-HW-010", "STA", "HW", RuleSeverity.S1, "光学链路设备组全通过", "光学链路设备组异常", "排查PD/VOA/Pump/DFB/TEC/Heater", "升级光学设备工程师"),
            ["TP_10"] = Make("TP_10", "STA-HW-011", "STA", "HW", RuleSeverity.S1, "控制与存储组全通过", "控制或存储设备异常", "检查MCU/EEPROM/Flash/Sensor", "升级硬件工程师"),
            ["TP_11"] = Make("TP_11", "STA-HW-012", "STA", "HW", RuleSeverity.S1, "接口通信组全通过", "接口通信异常", "排查I/O、DAC/ADC、SPI/I2C链路", "升级电控工程师"),
            ["TP_12"] = Make("TP_12", "STA-OPT-006", "STA", "OPT", RuleSeverity.S1, "光路残留风险=通过", "光路残留风险未解除", "复查纤芯/熔接点/盘盒", "升级光学与工艺团队"),
            ["SYS_01"] = Make("SYS_01", "SYS-ENV-001", "SYS", "ENV", RuleSeverity.S2, "OS>=Windows10", "操作系统版本偏低", "升级系统版本", "升级IT桌面支持"),
            ["SYS_02"] = Make("SYS_02", "SYS-ENV-002", "SYS", "ENV", RuleSeverity.S2, "连续运行<=7天", "系统连续运行时间过长", "安排维护窗口重启", "升级IT桌面支持"),
            ["SYS_03"] = Make("SYS_03", "SYS-ENV-003", "SYS", "ENV", RuleSeverity.S2, "最近30天已更新", "Windows更新滞后", "安装最新补丁", "升级IT桌面支持"),
            ["SYS_04"] = Make("SYS_04", "SYS-NET-006", "SYS", "NET", RuleSeverity.S1, "时间偏差<=2s", "系统时间不同步", "同步NTP/域时间", "升级域管理员")
        };
    }

    private static RuleMetadata Make(
        string checkId,
        string ruleCode,
        string domain,
        string category,
        RuleSeverity severity,
        string threshold,
        string failReason,
        string action,
        string escalation)
    {
        return new RuleMetadata
        {
            CheckId = checkId,
            RuleCode = ruleCode,
            Domain = domain,
            Category = category,
            Severity = severity,
            Threshold = threshold,
            DefaultFailReason = failReason,
            DefaultAction = action,
            EscalationPath = escalation
        };
    }
}
