using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
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
        public string TpRootPath { get; set; } = string.Empty;
        public bool TpPathExists { get; set; }
        public List<string> ConfigFiles { get; set; } = new List<string>();
        public List<string> ExpectedSerialPorts { get; set; } = new List<string>();
        public List<string> AvailableSerialPorts { get; set; } = new List<string>();
        public List<string> MissingSerialPorts { get; set; } = new List<string>();
        public List<TpNetworkEndpointStatus> NetworkEndpoints { get; set; } = new List<TpNetworkEndpointStatus>();
        public string Error { get; set; } = string.Empty;
    }

    public sealed class TpNetworkEndpointStatus
    {
        public string Endpoint { get; set; } = string.Empty;
        public bool Reachable { get; set; }
        public long ElapsedMs { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public sealed class DeviceVersionRequirement
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string RequiredVersion { get; set; } = string.Empty;
    }

    public sealed class DeviceVersionActual
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string ActualVersion { get; set; } = string.Empty;
    }

    public sealed class DeviceVersionMismatch
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string RequiredVersion { get; set; } = string.Empty;
        public string ActualVersion { get; set; } = string.Empty;
        public bool MissingActual { get; set; }
    }

    public sealed class DeviceVersionComplianceResult
    {
        public bool ApiSuccess { get; set; }
        public string ApiMessage { get; set; } = string.Empty;
        public string RequirementUrl { get; set; } = string.Empty;
        public string RequirementSource { get; set; } = string.Empty;
        public List<DeviceVersionRequirement> Requirements { get; set; } = new List<DeviceVersionRequirement>();
        public List<DeviceVersionActual> Actuals { get; set; } = new List<DeviceVersionActual>();
        public List<DeviceVersionMismatch> Mismatches { get; set; } = new List<DeviceVersionMismatch>();
    }

    public sealed class StationCapabilityRequirements
    {
        public double GrrMaxPercent { get; set; } = 10.0;
        public double GdsMinPercent { get; set; } = 90.0;
        public double MaxOutputPowerMinDbm { get; set; } = 6.0;
        public double SnrMinDb { get; set; } = 30.0;
        public double SwitchRepeatabilityMaxDb { get; set; } = 0.5;
        public double PowerStabilityMaxDb { get; set; } = 0.3;
        public string ChannelPlanRequired { get; set; } = "100G-4CH";
    }

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

    public sealed class StationCapabilityMetricResult
    {
        public string Metric { get; set; } = string.Empty;
        public string Required { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public bool Pass { get; set; }
    }

    public sealed class StationCapabilityComplianceResult
    {
        public bool Success { get; set; }
        public string ActualSource { get; set; } = string.Empty;
        public List<StationCapabilityMetricResult> Metrics { get; set; } = new List<StationCapabilityMetricResult>();
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    public sealed class PowerSupplyRequirements
    {
        public double TargetVoltageV { get; set; } = 12.0;
        public double MinVoltageV { get; set; } = 11.4;
        public double MaxVoltageV { get; set; } = 12.6;
        public double MaxStdDevV { get; set; } = 0.06;
        public double MaxRippleV { get; set; } = 0.25;
        public int SampleIntervalMs { get; set; } = 500;
        public int SampleCount { get; set; } = 12;
        public string TpVoltageApiUrl { get; set; } = string.Empty;
    }

    public sealed class PowerVoltageSample
    {
        public System.DateTime Timestamp { get; set; }
        public double VoltageV { get; set; }
    }

    public sealed class PowerSupplyQualityResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<PowerVoltageSample> Samples { get; set; } = new List<PowerVoltageSample>();
        public string CurveJsonPath { get; set; } = string.Empty;
        public string CurveCsvPath { get; set; } = string.Empty;
        public double MeanV { get; set; }
        public double StdDevV { get; set; }
        public double MinV { get; set; }
        public double MaxV { get; set; }
        public double RippleV { get; set; }
        public List<string> FailReasons { get; set; } = new List<string>();
    }

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

    public sealed class GroupedCheckMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public bool Pass { get; set; }
    }

    public sealed class HwSwFwConfigIntegrityResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<GroupedCheckMetric> Metrics { get; set; } = new List<GroupedCheckMetric>();
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    public sealed class HwStatusGroupedResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public List<GroupedCheckMetric> Metrics { get; set; } = new List<GroupedCheckMetric>();
        public List<string> FailReasons { get; set; } = new List<string>();
    }

    public sealed class OpticalResidualRiskResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<GroupedCheckMetric> Metrics { get; set; } = new List<GroupedCheckMetric>();
        public List<string> FailReasons { get; set; } = new List<string>();
    }
}
