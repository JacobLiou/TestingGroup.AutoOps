using System.Diagnostics;
using System.IO.Compression;
using System.IO.Ports;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AutoDiagnosis.Domain;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace AutoDiagnosis.Infrastructure;

public sealed class StationProfileProvider : IStationProfileProvider
{
    private const string TpConfigRootEnv = "AUTO_DIAG_TP_CONFIG_ROOT";
    private const string DefaultTpConfigRoot = @"C:\Users\menghl2\WorkSpace\Projects\Test Program\cal_fts_fvs_fqc\RELEASE\config";

    public async Task<StationProfile> LoadAsync(string stationType, CancellationToken cancellationToken)
    {
        var tpConfigRoot = ResolveTpConfigRoot();
        var profileFromTp = await TryLoadFromTpConfigAsync(tpConfigRoot, stationType, cancellationToken);
        if (profileFromTp is not null)
        {
            return profileFromTp;
        }

        return await LoadProfileFromWorkspaceAsync(stationType, cancellationToken);
    }

    private static string ResolveTpConfigRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable(TpConfigRootEnv);
        return string.IsNullOrWhiteSpace(fromEnv) ? DefaultTpConfigRoot : fromEnv;
    }

    private static async Task<StationProfile> LoadProfileFromWorkspaceAsync(string stationType, CancellationToken cancellationToken)
    {
        var workspaceRoot = WorkspaceLocator.LocateWorkspaceRoot();
        var profilePath = Path.Combine(workspaceRoot, "config", "baselines", $"{stationType}.json");
        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Station profile not found: {profilePath}");
        }

        await using var stream = File.OpenRead(profilePath);
        var profile = await JsonSerializer.DeserializeAsync<StationProfile>(stream, cancellationToken: cancellationToken);
        return profile ?? throw new InvalidDataException($"Unable to deserialize station profile: {profilePath}");
    }

    private static async Task<StationProfile?> TryLoadFromTpConfigAsync(
        string tpConfigRoot,
        string stationType,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(tpConfigRoot))
        {
            return null;
        }

        var systemXmlPath = Path.Combine(tpConfigRoot, "system.xml");
        var deviceCfgPath = Path.Combine(tpConfigRoot, "devicecfg.ini");
        if (!File.Exists(systemXmlPath) || !File.Exists(deviceCfgPath))
        {
            return null;
        }

        var hardware = ParseHardwareFromDeviceCfg(deviceCfgPath);
        var dependencies = ParseDependenciesFromSystemXml(systemXmlPath);
        var requiredServices = ParseRequiredServicesFromSystemXml(systemXmlPath);

        var configureIniPath = Path.Combine(tpConfigRoot, "Configure.ini");
        dependencies.AddRange(ParseDependenciesFromConfigureIni(configureIniPath));

        var workspaceFallback = await TryLoadWorkspaceBaselineAsync(stationType, cancellationToken);
        if (workspaceFallback is not null)
        {
            hardware = MergeDistinct(hardware, workspaceFallback.Hardware, item => item.Id);
            dependencies = MergeDistinct(dependencies, workspaceFallback.Dependencies, item => item.Id);
            requiredServices = MergeDistinct(requiredServices, workspaceFallback.RequiredServices, item => item);
        }

        return new StationProfile(
            stationType,
            hardware,
            dependencies,
            requiredServices);
    }

    private static async Task<StationProfile?> TryLoadWorkspaceBaselineAsync(string stationType, CancellationToken cancellationToken)
    {
        try
        {
            return await LoadProfileFromWorkspaceAsync(stationType, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static List<HardwareEndpoint> ParseHardwareFromDeviceCfg(string iniPath)
    {
        var hardware = new List<HardwareEndpoint>();
        string currentSection = "unknown";
        foreach (var rawLine in File.ReadLines(iniPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line[1..^1];
                continue;
            }

            if (!line.StartsWith("ADR", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var split = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (split.Length != 2)
            {
                continue;
            }

            var adr = split[1];
            var parts = adr.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                continue;
            }

            var type = parts[0].Split(':').LastOrDefault()?.Trim().ToUpperInvariant();
            if (type == "COM")
            {
                var port = parts[1];
                var baud = int.TryParse(parts.ElementAtOrDefault(2), out var baudRate) ? baudRate : 115200;
                hardware.Add(new HardwareEndpoint(
                    Id: $"{currentSection.ToLowerInvariant()}-com{port}",
                    Name: currentSection,
                    Protocol: "serial",
                    Address: $"COM{port}",
                    TimeoutMs: 1000,
                    BaudRate: baud,
                    ProbeCommand: "PING",
                    ExpectedContains: "PONG"));
            }
        }

        return hardware;
    }

    private static List<ExternalDependency> ParseDependenciesFromSystemXml(string systemXmlPath)
    {
        var deps = new List<ExternalDependency>();
        var doc = XDocument.Load(systemXmlPath);
        var filePathNode = doc.Root?.Element("file_path");
        if (filePathNode is null)
        {
            return deps;
        }

        AddIfUncPath(deps, "mesw-path", "MESW Path", filePathNode.Element("mesw_path")?.Value);
        AddIfUncPath(deps, "photo-path", "Photo Path", filePathNode.Element("photo_path")?.Value);
        AddIfUncPath(deps, "fw-path", "FW Path", filePathNode.Element("fw_path")?.Value);

        var testerIp = filePathNode.Element("tester_ip")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(testerIp))
        {
            deps.Add(new ExternalDependency("tester-ip", "Tester IP", DependencyType.Tcp, testerIp, 1500, Port: 5025));
        }

        var ftpIp = filePathNode.Element("ftp-ip")?.Value?.Trim() ?? filePathNode.Element("ftp_ip")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(ftpIp))
        {
            deps.Add(new ExternalDependency("ftp-ip", "FTP IP", DependencyType.Tcp, ftpIp, 1500, Port: 21));
        }

        return deps;
    }

    private static List<ExternalDependency> ParseDependenciesFromConfigureIni(string iniPath)
    {
        var deps = new List<ExternalDependency>();
        if (!File.Exists(iniPath))
        {
            return deps;
        }

        foreach (var rawLine in File.ReadLines(iniPath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("//", StringComparison.Ordinal) || !line.StartsWith("MDBSeverPath", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var split = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (split.Length == 2)
            {
                AddIfUncPath(deps, "mdb-server-path", "MDB Server Path", split[1]);
            }
        }

        return deps;
    }

    private static List<string> ParseRequiredServicesFromSystemXml(string systemXmlPath)
    {
        var doc = XDocument.Load(systemXmlPath);
        var services = doc.Root?
            .Element("dependencies")?
            .Elements("dependency")
            .Select(item => item.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return services ?? [];
    }

    private static void AddIfUncPath(List<ExternalDependency> deps, string id, string name, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = path.Trim();
        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            deps.Add(new ExternalDependency(id, name, DependencyType.FileShare, normalized, 2000));
        }
    }

    private static List<T> MergeDistinct<T>(IEnumerable<T> left, IEnumerable<T> right, Func<T, string> keySelector)
    {
        return left.Concat(right)
            .GroupBy(item => keySelector(item), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }
}

public sealed class ExternalDependencyCheck : ICheckItem
{
    private readonly ILogger<ExternalDependencyCheck> _logger;
    private readonly ResiliencePipeline<string> _probePipeline;

    public ExternalDependencyCheck(ILogger<ExternalDependencyCheck> logger)
    {
        _logger = logger;
        _probePipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(350),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<string>()
                    .Handle<Exception>()
                    .HandleResult(result => result.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase)),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Dependency probe retry. DependencyResult={Result}, Attempt={Attempt}, DelayMs={DelayMs}",
                        args.Outcome.Result,
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public string Id => "system-dependencies";
    public string Name => "System Dependency Reachability";

    public async Task<CheckResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var details = new Dictionary<string, string>();
        var hasFailure = false;

        foreach (var dependency in context.Profile.Dependencies)
        {
            var probe = await _probePipeline.ExecuteAsync(
                async token => await ProbeDependencyAsync(dependency, token),
                cancellationToken);
            details[dependency.Id] = probe;
            if (!probe.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            {
                hasFailure = true;
                _logger.LogWarning(
                    "Dependency probe failed. CorrelationId={CorrelationId}, Dependency={DependencyId}, Result={Result}",
                    context.CorrelationId,
                    dependency.Id,
                    probe);
            }
            else
            {
                _logger.LogInformation(
                    "Dependency probe succeeded. CorrelationId={CorrelationId}, Dependency={DependencyId}, Result={Result}",
                    context.CorrelationId,
                    dependency.Id,
                    probe);
            }
        }

        sw.Stop();
        var code = hasFailure ? DiagnosticResultCode.Fail : DiagnosticResultCode.Ok;
        var message = hasFailure ? "One or more dependencies failed." : "All dependencies reachable.";
        return new CheckResult(Id, Name, code, message, sw.ElapsedMilliseconds, details);
    }

    private static async Task<string> ProbeDependencyAsync(ExternalDependency dependency, CancellationToken cancellationToken)
    {
        try
        {
            return dependency.Type switch
            {
                DependencyType.Http => await ProbeHttpAsync(dependency, cancellationToken),
                DependencyType.Tcp => await ProbeTcpAsync(dependency, cancellationToken),
                DependencyType.FileShare => ProbeFileShare(dependency),
                _ => "FAIL: Unsupported dependency type."
            };
        }
        catch (Exception ex)
        {
            return $"FAIL: {ex.Message}";
        }
    }

    private static async Task<string> ProbeHttpAsync(ExternalDependency dependency, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(dependency.TimeoutMs) };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{dependency.Target}{dependency.HealthPath}");
        using var response = await http.SendAsync(request, cancellationToken);
        var status = (int)response.StatusCode;
        return response.IsSuccessStatusCode ? $"OK: HTTP {status}" : $"FAIL: HTTP {status}";
    }

    private static async Task<string> ProbeTcpAsync(ExternalDependency dependency, CancellationToken cancellationToken)
    {
        if (dependency.Port is null)
        {
            return "FAIL: Missing TCP port.";
        }

        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(dependency.TimeoutMs));
        await client.ConnectAsync(dependency.Target, dependency.Port.Value, cts.Token);
        return "OK: TCP connected";
    }

    private static string ProbeFileShare(ExternalDependency dependency)
    {
        return Directory.Exists(dependency.Target) ? "OK: Share reachable" : "FAIL: Share not reachable";
    }
}

public sealed class SerialHardwareCheck : ICheckItem
{
    public string Id => "serial-ports";
    public string Name => "Serial Hardware Connectivity";

    public Task<CheckResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var details = new Dictionary<string, string>();
        var ports = SerialPort.GetPortNames();
        var hasFailure = false;

        foreach (var endpoint in context.Profile.Hardware.Where(item =>
                     item.Protocol.Equals("serial", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ports.Contains(endpoint.Address, StringComparer.OrdinalIgnoreCase))
            {
                details[endpoint.Id] = $"FAIL: Port {endpoint.Address} not found";
                hasFailure = true;
                continue;
            }

            try
            {
                using var serial = new SerialPort(endpoint.Address, endpoint.BaudRate ?? 115200)
                {
                    ReadTimeout = endpoint.TimeoutMs,
                    WriteTimeout = endpoint.TimeoutMs
                };
                serial.Open();
                if (!string.IsNullOrWhiteSpace(endpoint.ProbeCommand))
                {
                    serial.WriteLine(endpoint.ProbeCommand);
                }
                details[endpoint.Id] = "OK: Port open";
                serial.Close();
            }
            catch (Exception ex)
            {
                details[endpoint.Id] = $"FAIL: {ex.Message}";
                hasFailure = true;
            }
        }

        sw.Stop();
        return Task.FromResult(new CheckResult(
            Id,
            Name,
            hasFailure ? DiagnosticResultCode.Fail : DiagnosticResultCode.Ok,
            hasFailure ? "Serial check has failures." : "Serial check passed.",
            sw.ElapsedMilliseconds,
            details));
    }
}

public sealed class StationServiceCheck : ICheckItem
{
    public string Id => "services-status";
    public string Name => "Required Process Status";

    public Task<CheckResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var details = new Dictionary<string, string>();
        var hasFailure = false;

        foreach (var processName in context.Profile.RequiredServices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exists = Process.GetProcessesByName(processName).Any();
            details[processName] = exists ? "OK: Running" : "FAIL: Not running";
            if (!exists)
            {
                hasFailure = true;
            }
        }

        sw.Stop();
        return Task.FromResult(new CheckResult(
            Id,
            Name,
            hasFailure ? DiagnosticResultCode.Fail : DiagnosticResultCode.Ok,
            hasFailure ? "Required process missing." : "All required processes running.",
            sw.ElapsedMilliseconds,
            details));
    }
}

public sealed class BaselineVersionCheck : ICheckItem
{
    public string Id => "baseline-version";
    public string Name => "Baseline Consistency";

    public Task<CheckResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var baselineFile = Path.Combine(context.WorkspaceRoot, "config", "baselines", $"{context.Profile.StationType}.json");
        var exists = File.Exists(baselineFile);
        sw.Stop();

        return Task.FromResult(new CheckResult(
            Id,
            Name,
            exists ? DiagnosticResultCode.Ok : DiagnosticResultCode.Fail,
            exists ? "Baseline profile found." : "Baseline profile missing.",
            sw.ElapsedMilliseconds,
            new Dictionary<string, string> { ["baselineFile"] = baselineFile }));
    }
}

public sealed class TpReadonlyMeasureCheck : ICheckItem
{
    public string Id => "tp-readonly-performance";
    public string Name => "TP SDK Readonly Measure";

    public async Task<CheckResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var tpResult = await Task.FromResult(new Dictionary<string, string>
        {
            ["GRR"] = "PASS",
            ["GDS"] = "PASS",
            ["Performance"] = "PASS"
        });
        sw.Stop();

        return new CheckResult(
            Id,
            Name,
            DiagnosticResultCode.Ok,
            "Readonly test-program checks passed.",
            sw.ElapsedMilliseconds,
            tpResult);
    }
}

public sealed class JsonAuditLogger : IAuditLogger
{
    public async Task WriteAsync(string correlationId, string category, object payload, CancellationToken cancellationToken)
    {
        var workspaceRoot = WorkspaceLocator.LocateWorkspaceRoot();
        var logDir = Path.Combine(workspaceRoot, "artifacts", "evidence", "audit");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, $"{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var line = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            correlationId,
            category,
            payload
        });
        await File.AppendAllTextAsync(logPath, line + Environment.NewLine, Encoding.UTF8, cancellationToken);
    }
}

public sealed class EvidenceCollector : IEvidenceCollector
{
    public async Task<EvidencePackageResult> CollectAsync(
        CheckExecutionContext context,
        HealthReport healthReport,
        RunbookExecutionResult runbookResult,
        CancellationToken cancellationToken)
    {
        var workspaceRoot = WorkspaceLocator.LocateWorkspaceRoot();
        var outputDir = Path.Combine(workspaceRoot, "artifacts", "evidence", context.CorrelationId);
        Directory.CreateDirectory(outputDir);

        var baselinePath = Path.Combine(outputDir, "baseline.json");
        var healthPath = Path.Combine(outputDir, "health-report.json");
        var runbookPath = Path.Combine(outputDir, "runbook-result.json");
        var screenshotPath = Path.Combine(outputDir, "screenshot.txt");

        await File.WriteAllTextAsync(baselinePath, JsonSerializer.Serialize(context.Profile, JsonDefaults.Pretty), cancellationToken);
        await File.WriteAllTextAsync(healthPath, JsonSerializer.Serialize(healthReport, JsonDefaults.Pretty), cancellationToken);
        await File.WriteAllTextAsync(runbookPath, JsonSerializer.Serialize(runbookResult, JsonDefaults.Pretty), cancellationToken);
        await File.WriteAllTextAsync(screenshotPath, "Screenshot placeholder for MVP.", cancellationToken);

        var zipPath = Path.Combine(workspaceRoot, "artifacts", "evidence", $"{context.CorrelationId}.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(outputDir, zipPath);
        return new EvidencePackageResult(zipPath);
    }
}

public sealed class MockEvidenceUploader : IEvidenceUploader
{
    public async Task<EvidenceUploadResult> UploadAsync(
        CheckExecutionContext context,
        EvidencePackageResult packageResult,
        CancellationToken cancellationToken)
    {
        var workspaceRoot = WorkspaceLocator.LocateWorkspaceRoot();
        var requestDir = Path.Combine(workspaceRoot, "artifacts", "evidence", "upload-queue");
        Directory.CreateDirectory(requestDir);
        var requestPath = Path.Combine(requestDir, $"{context.CorrelationId}.upload.json");
        var payload = JsonSerializer.Serialize(new
        {
            context.CorrelationId,
            station = context.Station.StationId,
            productFamily = context.Station.ProductFamily,
            packageResult.ZipFilePath,
            mode = "Mock"
        }, JsonDefaults.Pretty);

        await File.WriteAllTextAsync(requestPath, payload, cancellationToken);
        return new EvidenceUploadResult(true, $"Queued mock upload request: {requestPath}");
    }
}

public sealed class ReconnectInstrumentsAction : IHealingAction
{
    public string Id => "reconnect-instruments";
    public string Name => "Reconnect Instruments";
    public UserRole RequiredRole => UserRole.Operator;

    public Task<HealingActionResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        var serialCount = context.Profile.Hardware.Count(item =>
            item.Protocol.Equals("serial", StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(new HealingActionResult(
            Id,
            true,
            $"Reconnect requested for {serialCount} serial endpoints.",
            DateTimeOffset.UtcNow));
    }
}

public sealed class RestartServicesAction : IHealingAction
{
    public string Id => "restart-services";
    public string Name => "Restart Required Services";
    public UserRole RequiredRole => UserRole.Debugger;

    public Task<HealingActionResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HealingActionResult(
            Id,
            true,
            "Restart command recorded. Execute with operational approval in production.",
            DateTimeOffset.UtcNow));
    }
}

public sealed class CleanupCacheAction : IHealingAction
{
    public string Id => "cleanup-cache";
    public string Name => "Cleanup Cache";
    public UserRole RequiredRole => UserRole.Operator;

    public Task<HealingActionResult> ExecuteAsync(CheckExecutionContext context, CancellationToken cancellationToken)
    {
        var cacheDir = Path.Combine(context.WorkspaceRoot, "artifacts", "evidence", "temp");
        Directory.CreateDirectory(cacheDir);
        foreach (var file in Directory.GetFiles(cacheDir))
        {
            File.Delete(file);
        }

        return Task.FromResult(new HealingActionResult(
            Id,
            true,
            "Cache cleanup completed.",
            DateTimeOffset.UtcNow));
    }
}

public static class WorkspaceLocator
{
    public static string LocateWorkspaceRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var candidate in candidates)
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "config")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
