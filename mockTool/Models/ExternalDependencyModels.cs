namespace MockDiagTool.Models;

public static class ExternalDependencyIds
{
    public const string Mes = "EXT_MES";
    public const string Tms = "EXT_TMS";
    public const string Tas = "EXT_TAS";
    public const string FileServer = "EXT_FILE_SERVER";
    public const string Lan = "EXT_LAN";
}

public sealed class ExternalDependencyEndpoint
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class ExternalDependencyConfig
{
    public Dictionary<string, ExternalDependencyEndpoint> Endpoints { get; init; } = [];
}

public sealed class ExternalDependencyCheckResult
{
    public bool Success { get; init; }
    public int? StatusCode { get; init; }
    public long ElapsedMs { get; init; }
    public string Error { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string EndpointName { get; init; } = string.Empty;
}

public sealed class DiagnosticRunContext
{
    public bool ExternalChecksEnabled { get; init; }
    public ExternalDependencyConfig? ExternalConfig { get; init; }
    public string ConfigSource { get; init; } = string.Empty;
    public string ConfigError { get; init; } = string.Empty;
}
