using System.Text.Json;

namespace AutoDiagnosis.Domain;

public enum DiagnosticResultCode
{
    Ok,
    Warn,
    Fail,
    Blocked
}

public enum UserRole
{
    Viewer,
    Operator,
    Debugger,
    Admin
}

public enum DependencyType
{
    Http,
    Tcp,
    FileShare
}

public sealed record StationContext(string StationId, string ProductFamily, string? SerialNumber);

public sealed record HardwareEndpoint(
    string Id,
    string Name,
    string Protocol,
    string Address,
    int TimeoutMs = 1000,
    int? BaudRate = null,
    string? ProbeCommand = null,
    string? ExpectedContains = null);

public sealed record ExternalDependency(
    string Id,
    string Name,
    DependencyType Type,
    string Target,
    int TimeoutMs = 2000,
    string? HealthPath = null,
    int? Port = null);

public sealed record StationProfile(
    string StationType,
    List<HardwareEndpoint> Hardware,
    List<ExternalDependency> Dependencies,
    List<string> RequiredServices);

public sealed record CheckExecutionContext(
    StationContext Station,
    StationProfile Profile,
    UserRole UserRole,
    string CorrelationId,
    string WorkspaceRoot,
    string? BaselineVersion = null);

public sealed record CheckResult(
    string Id,
    string Name,
    DiagnosticResultCode Code,
    string Message,
    long DurationMs,
    Dictionary<string, string>? Metadata = null);

public sealed record HealthReport(
    string CorrelationId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int Score,
    IReadOnlyList<CheckResult> Results);

public sealed record RunbookStepDefinition(
    string Id,
    string Name,
    string CheckId,
    string? NextOnSuccess = null,
    string? NextOnFailure = null);

public sealed record RunbookDefinition(
    string Id,
    string Name,
    string ProductFamily,
    List<RunbookStepDefinition> Steps);

public sealed record RunbookStepResult(
    string StepId,
    string StepName,
    DiagnosticResultCode Code,
    string Message,
    long DurationMs);

public sealed record RunbookExecutionResult(
    string CorrelationId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<RunbookStepResult> Steps);

public sealed record HealingActionResult(
    string ActionId,
    bool Succeeded,
    string Message,
    DateTimeOffset ExecutedAt);

public sealed record EvidencePackageResult(string ZipFilePath);
public sealed record EvidenceUploadResult(bool Succeeded, string Message);

public interface ICheckItem
{
    string Id { get; }
    string Name { get; }
    Task<CheckResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken);
}

public interface IHealthCheckEngine
{
    Task<HealthReport> ExecuteAsync(
        CheckExecutionContext context,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null,
        Func<CancellationToken, Task>? pausePoint = null);
}

public interface IRunbookProvider
{
    Task<RunbookDefinition> LoadAsync(string productFamily, CancellationToken cancellationToken);
}

public interface IRunbookExecutor
{
    Task<RunbookExecutionResult> ExecuteAsync(
        RunbookDefinition runbook,
        CheckExecutionContext context,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null,
        Func<CancellationToken, Task>? pausePoint = null);
}

public interface IHealingAction
{
    string Id { get; }
    string Name { get; }
    UserRole RequiredRole { get; }
    Task<HealingActionResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken);
}

public interface ISelfHealingService
{
    Task<IReadOnlyList<HealingActionResult>> ExecuteAllowedActionsAsync(
        CheckExecutionContext context,
        IEnumerable<string> actionIds,
        CancellationToken cancellationToken);
}

public interface IEvidenceCollector
{
    Task<EvidencePackageResult> CollectAsync(
        CheckExecutionContext context,
        HealthReport healthReport,
        RunbookExecutionResult runbookResult,
        CancellationToken cancellationToken);
}

public interface IEvidenceUploader
{
    Task<EvidenceUploadResult> UploadAsync(
        CheckExecutionContext context,
        EvidencePackageResult packageResult,
        CancellationToken cancellationToken);
}

public interface IAuditLogger
{
    Task WriteAsync(string correlationId, string category, object payload, CancellationToken cancellationToken);
}

public interface IStationProfileProvider
{
    Task<StationProfile> LoadAsync(string stationType, CancellationToken cancellationToken);
}

public interface ICorrelationIdGenerator
{
    string Create(StationContext stationContext);
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true
    };
}
