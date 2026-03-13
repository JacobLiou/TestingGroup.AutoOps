namespace MockDiagTool.Models;

public static class TpCheckIds
{
    public const string PathAndConfig = "TP_01";
    public const string SerialPorts = "TP_02";
    public const string NetworkEndpoints = "TP_03";
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
