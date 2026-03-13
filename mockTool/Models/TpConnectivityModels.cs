namespace MockDiagTool.Models;

public static class TpCheckIds
{
    public const string PathAndConfig = "TP_01";
    public const string SerialPorts = "TP_02";
    public const string NetworkEndpoints = "TP_03";
    public const string VersionCompliance = "TP_04";
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
    public List<DeviceVersionRequirement> Requirements { get; init; } = [];
    public List<DeviceVersionActual> Actuals { get; init; } = [];
    public List<DeviceVersionMismatch> Mismatches { get; init; } = [];
}
