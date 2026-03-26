using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// TP（测试程序）相关检查项 ID 常量
    /// </summary>
    public static class TpCheckIds
    {
        public const string PathAndConfig = "TP_01";
        public const string SerialPorts = "TP_02";
        public const string NetworkEndpoints = "TP_03";
        public const string VersionCompliance = "TP_04";
        public const string StationCapabilityCompliance = "TP_05";
        public const string PowerSupplyQuality = "TP_06";
        public const string DefaultInfoAndLut = "TP_07";
        public const string HwSwFwConfigIntegrity = "TP_08";
        public const string HwStatusOpticalGroup = "TP_09";
        public const string HwStatusControlStorageGroup = "TP_10";
        public const string HwStatusInterfaceCommGroup = "TP_11";
        public const string OpticalResidualRisk = "TP_12";
        public const string OpticalCustomGrrRule = "TP_13";
        public const string OpticalCustomSnrRule = "TP_14";
    }

    /// <summary>
    /// TP 连通性检测快照 — 包含路径、配置文件、串口、网络端点等信息
    /// </summary>
    public sealed class TpConnectivitySnapshot
    {
        /// <summary>TP 程序根目录路径</summary>
        public string TpRootPath { get; set; } = string.Empty;
        /// <summary>TP 路径是否存在</summary>
        public bool TpPathExists { get; set; }
        /// <summary>检测到的配置文件列表</summary>
        public List<string> ConfigFiles { get; set; } = new List<string>();
        /// <summary>期望的串口列表</summary>
        public List<string> ExpectedSerialPorts { get; set; } = new List<string>();
        /// <summary>系统中实际可用的串口列表</summary>
        public List<string> AvailableSerialPorts { get; set; } = new List<string>();
        /// <summary>缺失的串口列表</summary>
        public List<string> MissingSerialPorts { get; set; } = new List<string>();
        /// <summary>网络端点可达性检测结果</summary>
        public List<TpNetworkEndpointStatus> NetworkEndpoints { get; set; } = new List<TpNetworkEndpointStatus>();
        /// <summary>检测过程中的错误信息</summary>
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// 单个网络端点的可达性探测结果
    /// </summary>
    public sealed class TpNetworkEndpointStatus
    {
        /// <summary>端点地址（host:port 或 URL）</summary>
        public string Endpoint { get; set; } = string.Empty;
        /// <summary>是否可达</summary>
        public bool Reachable { get; set; }
        /// <summary>探测耗时（毫秒）</summary>
        public long ElapsedMs { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// 设备版本要求条目（来自 MIMS 或本地配置）
    /// </summary>
    public sealed class DeviceVersionRequirement
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string RequiredVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// 设备实际版本条目
    /// </summary>
    public sealed class DeviceVersionActual
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string ActualVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// 设备版本不匹配项
    /// </summary>
    public sealed class DeviceVersionMismatch
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string RequiredVersion { get; set; } = string.Empty;
        public string ActualVersion { get; set; } = string.Empty;
        /// <summary>是否缺少实际版本信息</summary>
        public bool MissingActual { get; set; }
    }

    /// <summary>
    /// 设备版本合规性检查的完整结果
    /// </summary>
    public sealed class DeviceVersionComplianceResult
    {
        public bool ApiSuccess { get; set; }
        public string ApiMessage { get; set; } = string.Empty;
        public string RequirementUrl { get; set; } = string.Empty;
        public string RequirementSource { get; set; } = string.Empty;
        public List<DeviceVersionRequirement> Requirements { get; set; } = new List<DeviceVersionRequirement>();
        public List<DeviceVersionActual> Actuals { get; set; } = new List<DeviceVersionActual>();
        /// <summary>不匹配的设备列表</summary>
        public List<DeviceVersionMismatch> Mismatches { get; set; } = new List<DeviceVersionMismatch>();
    }

    /// <summary>
    /// 工站能力基线要求（光学指标阈值等）
    /// </summary>
    public sealed class StationCapabilityRequirements
    {
        /// <summary>GRR 最大百分比</summary>
        public double GrrMaxPercent { get; set; } = 10.0;
        /// <summary>GDS 最小百分比</summary>
        public double GdsMinPercent { get; set; } = 90.0;
        /// <summary>最大输出光功率下限 (dBm)</summary>
        public double MaxOutputPowerMinDbm { get; set; } = 6.0;
        /// <summary>信噪比下限 (dB)</summary>
        public double SnrMinDb { get; set; } = 30.0;
        /// <summary>开关重复性上限 (dB)</summary>
        public double SwitchRepeatabilityMaxDb { get; set; } = 0.5;
        /// <summary>功率稳定性上限 (dB)</summary>
        public double PowerStabilityMaxDb { get; set; } = 0.3;
        /// <summary>要求的通道规划</summary>
        public string ChannelPlanRequired { get; set; } = "100G-4CH";
    }

    /// <summary>
    /// 工站能力实际测量值
    /// </summary>
    public sealed class StationCapabilityActual
    {
        public double GrrPercent { get; set; }
        public double GdsPercent { get; set; }
        public double MaxOutputPowerDbm { get; set; }
        public double SnrDb { get; set; }
        public double SwitchRepeatabilityDb { get; set; }
        public double PowerStabilityDb { get; set; }
        public string ChannelPlanActual { get; set; } = string.Empty;
    }

    /// <summary>
    /// 工站能力单项指标的对比结果
    /// </summary>
    public sealed class StationCapabilityMetricResult
    {
        /// <summary>指标名称</summary>
        public string Metric { get; set; } = string.Empty;
        /// <summary>基线要求值</summary>
        public string Required { get; set; } = string.Empty;
        /// <summary>实际值</summary>
        public string Actual { get; set; } = string.Empty;
        /// <summary>是否达标</summary>
        public bool Pass { get; set; }
    }

    /// <summary>
    /// 工站能力合规性检查的完整结果
    /// </summary>
    public sealed class StationCapabilityComplianceResult
    {
        public bool Success { get; set; }
        /// <summary>实际值数据来源</summary>
        public string ActualSource { get; set; } = string.Empty;
        /// <summary>各指标对比结果</summary>
        public List<StationCapabilityMetricResult> Metrics { get; set; } = new List<StationCapabilityMetricResult>();
        /// <summary>失败原因列表</summary>
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// 电源质量基线要求
    /// </summary>
    public sealed class PowerSupplyRequirements
    {
        /// <summary>目标电压 (V)</summary>
        public double TargetVoltageV { get; set; } = 12.0;
        /// <summary>最小允许电压 (V)</summary>
        public double MinVoltageV { get; set; } = 11.4;
        /// <summary>最大允许电压 (V)</summary>
        public double MaxVoltageV { get; set; } = 12.6;
        /// <summary>标准差上限 (V)</summary>
        public double MaxStdDevV { get; set; } = 0.06;
        /// <summary>纹波上限 (V)</summary>
        public double MaxRippleV { get; set; } = 0.25;
        /// <summary>采样间隔 (ms)</summary>
        public int SampleIntervalMs { get; set; } = 500;
        /// <summary>采样次数</summary>
        public int SampleCount { get; set; } = 12;
        /// <summary>TP 电压 API 地址</summary>
        public string TpVoltageApiUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// 电源电压单次采样
    /// </summary>
    public sealed class PowerVoltageSample
    {
        public System.DateTime Timestamp { get; set; }
        /// <summary>电压值 (V)</summary>
        public double VoltageV { get; set; }
    }

    /// <summary>
    /// 电源质量检查的完整结果（含采样数据及统计值）
    /// </summary>
    public sealed class PowerSupplyQualityResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        /// <summary>采样数据序列</summary>
        public List<PowerVoltageSample> Samples { get; set; } = new List<PowerVoltageSample>();
        /// <summary>导出的曲线 JSON 文件路径</summary>
        public string CurveJsonPath { get; set; } = string.Empty;
        /// <summary>导出的曲线 CSV 文件路径</summary>
        public string CurveCsvPath { get; set; } = string.Empty;
        /// <summary>平均电压 (V)</summary>
        public double MeanV { get; set; }
        /// <summary>标准差 (V)</summary>
        public double StdDevV { get; set; }
        /// <summary>最小电压 (V)</summary>
        public double MinV { get; set; }
        /// <summary>最大电压 (V)</summary>
        public double MaxV { get; set; }
        /// <summary>纹波 (V) = Max - Min</summary>
        public double RippleV { get; set; }
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// Default Info 与 LUT 文件检查结果
    /// </summary>
    public sealed class DefaultInfoAndLutResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        public string DefaultInfoUrl { get; set; } = string.Empty;
        public string LutDownloadUrl { get; set; } = string.Empty;
        public string DefaultInfoSummary { get; set; } = string.Empty;
        public string LutSummary { get; set; } = string.Empty;
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// 分组检查中的单项指标（期望值 vs 实际值）
    /// </summary>
    public sealed class GroupedCheckMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public bool Pass { get; set; }
    }

    /// <summary>
    /// 硬件/软件/固件配置完整性检查结果
    /// </summary>
    public sealed class HwSwFwConfigIntegrityResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<GroupedCheckMetric> Metrics { get; set; } = new List<GroupedCheckMetric>();
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// 硬件状态分组检查结果（按功能组划分：光学 / 控制存储 / 接口通信）
    /// </summary>
    public sealed class HwStatusGroupedResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        /// <summary>分组键（如 Optical / ControlStorage / InterfaceComm）</summary>
        public string GroupKey { get; set; } = string.Empty;
        public List<GroupedCheckMetric> Metrics { get; set; } = new List<GroupedCheckMetric>();
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// 光学残余风险评估结果
    /// </summary>
    public sealed class OpticalResidualRiskResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<GroupedCheckMetric> Metrics { get; set; } = new List<GroupedCheckMetric>();
        public List<string> FailReasons { get; set; } = new List<string>();
    }
}
