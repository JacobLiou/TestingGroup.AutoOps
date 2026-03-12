using AutoDiagnosis.Domain;
using Microsoft.Extensions.Logging;

namespace AutoDiagnosis.Application;

public sealed class HealthCheckEngine(IEnumerable<ICheckItem> checkItems) : IHealthCheckEngine
{
    private readonly IReadOnlyList<ICheckItem> _checkItems = checkItems.ToList();

    public async Task<HealthReport> ExecuteAsync(
        CheckExecutionContext context,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null,
        Func<CancellationToken, Task>? pausePoint = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<CheckResult>(_checkItems.Count);

        foreach (var item in _checkItems)
        {
            if (pausePoint is not null)
            {
                await pausePoint(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Health Check - {item.Name}");
            results.Add(await item.ExecuteAsync(context, cancellationToken));
        }

        var finishedAt = DateTimeOffset.UtcNow;
        return new HealthReport(
            context.CorrelationId,
            startedAt,
            finishedAt,
            CalculateScore(results),
            results);
    }

    private static int CalculateScore(IEnumerable<CheckResult> results)
    {
        var items = results.ToList();
        if (items.Count == 0)
        {
            return 0;
        }

        var points = items.Sum(result => result.Code switch
        {
            DiagnosticResultCode.Ok => 100,
            DiagnosticResultCode.Warn => 60,
            DiagnosticResultCode.Fail => 0,
            DiagnosticResultCode.Blocked => 20,
            _ => 0
        });

        return (int)Math.Round(points / (double)items.Count, MidpointRounding.AwayFromZero);
    }
}

public sealed class RunbookExecutor(IEnumerable<ICheckItem> checkItems) : IRunbookExecutor
{
    private readonly IReadOnlyDictionary<string, ICheckItem> _checkItems =
        checkItems.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

    public async Task<RunbookExecutionResult> ExecuteAsync(
        RunbookDefinition runbook,
        CheckExecutionContext context,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null,
        Func<CancellationToken, Task>? pausePoint = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stepResults = new List<RunbookStepResult>();
        var stepMap = runbook.Steps.ToDictionary(step => step.Id, StringComparer.OrdinalIgnoreCase);
        var currentStep = runbook.Steps.FirstOrDefault();

        while (currentStep is not null)
        {
            if (pausePoint is not null)
            {
                await pausePoint(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (!_checkItems.TryGetValue(currentStep.CheckId, out var check))
            {
                progress?.Report($"Runbook - {currentStep.Name} (missing handler)");
                stepResults.Add(new RunbookStepResult(
                    currentStep.Id,
                    currentStep.Name,
                    DiagnosticResultCode.Blocked,
                    $"Missing check handler: {currentStep.CheckId}",
                    0));
                break;
            }

            progress?.Report($"Runbook - {currentStep.Name}");
            var checkResult = await check.ExecuteAsync(context, cancellationToken);
            stepResults.Add(new RunbookStepResult(
                currentStep.Id,
                currentStep.Name,
                checkResult.Code,
                checkResult.Message,
                checkResult.DurationMs));

            var nextStepId = checkResult.Code is DiagnosticResultCode.Ok or DiagnosticResultCode.Warn
                ? currentStep.NextOnSuccess
                : currentStep.NextOnFailure;

            if (string.IsNullOrWhiteSpace(nextStepId))
            {
                break;
            }

            if (!stepMap.TryGetValue(nextStepId, out var nextStep))
            {
                stepResults.Add(new RunbookStepResult(
                    currentStep.Id,
                    currentStep.Name,
                    DiagnosticResultCode.Blocked,
                    $"Branch target not found: {nextStepId}",
                    0));
                break;
            }

            currentStep = nextStep;
        }

        return new RunbookExecutionResult(
            context.CorrelationId,
            startedAt,
            DateTimeOffset.UtcNow,
            stepResults);
    }
}

public sealed class SelfHealingService(
    IEnumerable<IHealingAction> actions,
    IAuditLogger auditLogger) : ISelfHealingService
{
    private readonly IReadOnlyDictionary<string, IHealingAction> _actions =
        actions.ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);
    private readonly IAuditLogger _auditLogger = auditLogger;

    public async Task<IReadOnlyList<HealingActionResult>> ExecuteAllowedActionsAsync(
        CheckExecutionContext context,
        IEnumerable<string> actionIds,
        CancellationToken cancellationToken)
    {
        var results = new List<HealingActionResult>();
        foreach (var actionId in actionIds)
        {
            if (!_actions.TryGetValue(actionId, out var action))
            {
                results.Add(new HealingActionResult(actionId, false, "Action not found.", DateTimeOffset.UtcNow));
                continue;
            }

            if (context.UserRole < action.RequiredRole)
            {
                results.Add(new HealingActionResult(action.Id, false, "Insufficient role.", DateTimeOffset.UtcNow));
                continue;
            }

            var actionResult = await action.ExecuteAsync(context, cancellationToken);
            await _auditLogger.WriteAsync(
                context.CorrelationId,
                "self-healing",
                new { action.Id, action.Name, actionResult.Succeeded, actionResult.Message },
                cancellationToken);
            results.Add(actionResult);
        }

        return results;
    }
}

public sealed class CorrelationIdGenerator : ICorrelationIdGenerator
{
    public string Create(StationContext stationContext)
    {
        var sn = string.IsNullOrWhiteSpace(stationContext.SerialNumber) ? "NOSN" : stationContext.SerialNumber;
        return $"{stationContext.StationId}-{sn}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }
}

public sealed class DiagnosticOrchestrator(
    IStationProfileProvider stationProfileProvider,
    IRunbookProvider runbookProvider,
    IHealthCheckEngine healthCheckEngine,
    IRunbookExecutor runbookExecutor,
    IEvidenceCollector evidenceCollector,
    IEvidenceUploader evidenceUploader,
    ISelfHealingService selfHealingService,
    IAuditLogger auditLogger,
    ILogger<DiagnosticOrchestrator> logger)
{
    private readonly IStationProfileProvider _stationProfileProvider = stationProfileProvider;
    private readonly IRunbookProvider _runbookProvider = runbookProvider;
    private readonly IHealthCheckEngine _healthCheckEngine = healthCheckEngine;
    private readonly IRunbookExecutor _runbookExecutor = runbookExecutor;
    private readonly IEvidenceCollector _evidenceCollector = evidenceCollector;
    private readonly IEvidenceUploader _evidenceUploader = evidenceUploader;
    private readonly ISelfHealingService _selfHealingService = selfHealingService;
    private readonly IAuditLogger _auditLogger = auditLogger;
    private readonly ILogger<DiagnosticOrchestrator> _logger = logger;

    public async Task<DiagnosticSessionResult> ExecuteAsync(
        StationContext stationContext,
        string stationType,
        UserRole userRole,
        string workspaceRoot,
        ICorrelationIdGenerator correlationIdGenerator,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null,
        Func<CancellationToken, Task>? pausePoint = null)
    {
        var correlationId = correlationIdGenerator.Create(stationContext);
        _logger.LogInformation(
            "Diagnostic session started. CorrelationId={CorrelationId}, Station={StationId}, ProductFamily={ProductFamily}, Role={Role}",
            correlationId,
            stationContext.StationId,
            stationContext.ProductFamily,
            userRole);
        var profile = await _stationProfileProvider.LoadAsync(stationType, cancellationToken);
        var context = new CheckExecutionContext(
            stationContext,
            profile,
            userRole,
            correlationId,
            workspaceRoot);

        await _auditLogger.WriteAsync(correlationId, "session-start", new { stationContext, stationType, userRole }, cancellationToken);
        progress?.Report("Preparing health checks");
        if (pausePoint is not null)
        {
            await pausePoint(cancellationToken);
        }

        var healthReport = await _healthCheckEngine.ExecuteAsync(context, cancellationToken, progress, pausePoint);
        progress?.Report("Loading runbook");
        if (pausePoint is not null)
        {
            await pausePoint(cancellationToken);
        }

        var runbook = await _runbookProvider.LoadAsync(stationContext.ProductFamily, cancellationToken);
        var runbookResult = await _runbookExecutor.ExecuteAsync(runbook, context, cancellationToken, progress, pausePoint);

        IReadOnlyList<HealingActionResult> healingResults = [];
        var hasFailure = runbookResult.Steps.Any(step => step.Code is DiagnosticResultCode.Fail or DiagnosticResultCode.Blocked);
        if (hasFailure)
        {
            progress?.Report("Self-healing actions");
            if (pausePoint is not null)
            {
                await pausePoint(cancellationToken);
            }

            healingResults = await _selfHealingService.ExecuteAllowedActionsAsync(
                context,
                ["reconnect-instruments", "restart-services", "cleanup-cache"],
                cancellationToken);
        }

        progress?.Report("Collecting evidence");
        if (pausePoint is not null)
        {
            await pausePoint(cancellationToken);
        }

        var evidence = await _evidenceCollector.CollectAsync(context, healthReport, runbookResult, cancellationToken);
        progress?.Report("Uploading evidence");
        if (pausePoint is not null)
        {
            await pausePoint(cancellationToken);
        }

        var upload = await _evidenceUploader.UploadAsync(context, evidence, cancellationToken);
        await _auditLogger.WriteAsync(correlationId, "session-finish", new { evidence.ZipFilePath, upload.Succeeded, upload.Message }, cancellationToken);
        progress?.Report("Completed");
        _logger.LogInformation(
            "Diagnostic session finished. CorrelationId={CorrelationId}, HealthScore={Score}, Evidence={EvidencePath}, UploadSucceeded={UploadSucceeded}",
            correlationId,
            healthReport.Score,
            evidence.ZipFilePath,
            upload.Succeeded);

        return new DiagnosticSessionResult(context, healthReport, runbookResult, healingResults, evidence, upload);
    }
}

public sealed record DiagnosticSessionResult(
    CheckExecutionContext Context,
    HealthReport HealthReport,
    RunbookExecutionResult RunbookResult,
    IReadOnlyList<HealingActionResult> HealingResults,
    EvidencePackageResult Evidence,
    EvidenceUploadResult UploadResult);
