namespace MockDiagTool.Models;

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

public sealed class TpConnectivitySnapshot
{
    public string TpRootPath { get; init; } = string.Empty;
    public bool TpPathExists { get; init; }
    public List<string> ConfigFiles { get; init; } = [];
    public List<string> ExpectedSerialPorts { get; init; } = [];
    public List<string> AvailableSerialPorts { get; init; } = [];
    public List<string> MissingSerialPorts { get; init; } = [];
    public List<TpNetworkEndpointStatus> NetworkEndpoints { get; init; } = [];
    public string Error { get; init; } = string.Empty;
}

public sealed class TpNetworkEndpointStatus
{
    public string Endpoint { get; init; } = string.Empty;
    public bool Reachable { get; init; }
    public long ElapsedMs { get; init; }
    public string Error { get; init; } = string.Empty;
}

public sealed class DeviceVersionRequirement
{
    public string DeviceKey { get; init; } = string.Empty;
    public string RequiredVersion { get; init; } = string.Empty;
}

public sealed class DeviceVersionActual
{
    public string DeviceKey { get; init; } = string.Empty;
    public string ActualVersion { get; init; } = string.Empty;
}

public sealed class DeviceVersionMismatch
{
    public string DeviceKey { get; init; } = string.Empty;
    public string RequiredVersion { get; init; } = string.Empty;
    public string ActualVersion { get; init; } = string.Empty;
    public bool MissingActual { get; init; }
}

public sealed class DeviceVersionComplianceResult
{
    public bool ApiSuccess { get; init; }
    public string ApiMessage { get; init; } = string.Empty;
    public string RequirementUrl { get; init; } = string.Empty;
    public string RequirementSource { get; init; } = string.Empty;
    public List<DeviceVersionRequirement> Requirements { get; init; } = [];
    public List<DeviceVersionActual> Actuals { get; init; } = [];
    public List<DeviceVersionMismatch> Mismatches { get; init; } = [];
}

public sealed class StationCapabilityRequirements
{
    public double GrrMaxPercent { get; init; } = 10.0;
    public double GdsMinPercent { get; init; } = 90.0;
    public double MaxOutputPowerMinDbm { get; init; } = 6.0;
    public double SnrMinDb { get; init; } = 30.0;
    public double SwitchRepeatabilityMaxDb { get; init; } = 0.5;
    public double PowerStabilityMaxDb { get; init; } = 0.3;
    public string ChannelPlanRequired { get; init; } = "100G-4CH";
}

public sealed class StationCapabilityActual
{
    public double GrrPercent { get; init; }
    public double GdsPercent { get; init; }
    public double MaxOutputPowerDbm { get; init; }
    public double SnrDb { get; init; }
    public double SwitchRepeatabilityDb { get; init; }
    public double PowerStabilityDb { get; init; }
    public string ChannelPlanActual { get; init; } = string.Empty;
}

public sealed class StationCapabilityMetricResult
{
    public string Metric { get; init; } = string.Empty;
    public string Required { get; init; } = string.Empty;
    public string Actual { get; init; } = string.Empty;
    public bool Pass { get; init; }
}

public sealed class StationCapabilityComplianceResult
{
    public bool Success { get; init; }
    public string ActualSource { get; init; } = string.Empty;
    public List<StationCapabilityMetricResult> Metrics { get; init; } = [];
    public List<string> FailReasons { get; init; } = [];
}

public sealed class PowerSupplyRequirements
{
    public double TargetVoltageV { get; init; } = 12.0;
    public double MinVoltageV { get; init; } = 11.4;
    public double MaxVoltageV { get; init; } = 12.6;
    public double MaxStdDevV { get; init; } = 0.06;
    public double MaxRippleV { get; init; } = 0.25;
    public int SampleIntervalMs { get; init; } = 500;
    public int SampleCount { get; init; } = 12;
    public string TpVoltageApiUrl { get; init; } = string.Empty;
}

public sealed class PowerVoltageSample
{
    public DateTime Timestamp { get; init; }
    public double VoltageV { get; init; }
}

public sealed class PowerSupplyQualityResult
{
    public bool Success { get; init; }
    public string Source { get; init; } = string.Empty;
    public List<PowerVoltageSample> Samples { get; init; } = [];
    public string CurveJsonPath { get; init; } = string.Empty;
    public string CurveCsvPath { get; init; } = string.Empty;
    public double MeanV { get; init; }
    public double StdDevV { get; init; }
    public double MinV { get; init; }
    public double MaxV { get; init; }
    public double RippleV { get; init; }
    public List<string> FailReasons { get; init; } = [];
}

public sealed class DefaultInfoAndLutResult
{
    public bool Success { get; init; }
    public string Source { get; init; } = string.Empty;
    public string DefaultInfoUrl { get; init; } = string.Empty;
    public string LutDownloadUrl { get; init; } = string.Empty;
    public string DefaultInfoSummary { get; init; } = string.Empty;
    public string LutSummary { get; init; } = string.Empty;
    public List<string> FailReasons { get; init; } = [];
}

public sealed class GroupedCheckMetric
{
    public string Name { get; init; } = string.Empty;
    public string Expected { get; init; } = string.Empty;
    public string Actual { get; init; } = string.Empty;
    public bool Pass { get; init; }
}

public sealed class HwSwFwConfigIntegrityResult
{
    public bool Success { get; init; }
    public string Source { get; init; } = string.Empty;
    public List<GroupedCheckMetric> Metrics { get; init; } = [];
    public List<string> FailReasons { get; init; } = [];
}

public sealed class HwStatusGroupedResult
{
    public bool Success { get; init; }
    public string Source { get; init; } = string.Empty;
    public string GroupKey { get; init; } = string.Empty;
    public List<GroupedCheckMetric> Metrics { get; init; } = [];
    public List<string> FailReasons { get; init; } = [];
}

public sealed class OpticalResidualRiskResult
{
    public bool Success { get; init; }
    public string Source { get; init; } = string.Empty;
    public List<GroupedCheckMetric> Metrics { get; init; } = [];
    public List<string> FailReasons { get; init; } = [];
}
