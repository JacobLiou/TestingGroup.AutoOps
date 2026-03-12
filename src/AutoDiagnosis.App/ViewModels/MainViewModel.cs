using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using AutoDiagnosis.App.Services;
using AutoDiagnosis.Application;
using AutoDiagnosis.Domain;
using AutoDiagnosis.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoDiagnosis.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStationProfileProvider _stationProfileProvider;
    private readonly IHealthCheckEngine _healthCheckEngine;
    private readonly IRunbookProvider _runbookProvider;
    private readonly IRunbookExecutor _runbookExecutor;
    private readonly IEvidenceCollector _evidenceCollector;
    private readonly DiagnosticOrchestrator _diagnosticOrchestrator;
    private readonly ICorrelationIdGenerator _correlationIdGenerator;
    private readonly ScanControl _scanControl = new();
    private CancellationTokenSource? _scanCts;

    public MainViewModel(
        IStationProfileProvider stationProfileProvider,
        IHealthCheckEngine healthCheckEngine,
        IRunbookProvider runbookProvider,
        IRunbookExecutor runbookExecutor,
        IEvidenceCollector evidenceCollector,
        DiagnosticOrchestrator diagnosticOrchestrator,
        ICorrelationIdGenerator correlationIdGenerator)
    {
        _stationProfileProvider = stationProfileProvider;
        _healthCheckEngine = healthCheckEngine;
        _runbookProvider = runbookProvider;
        _runbookExecutor = runbookExecutor;
        _evidenceCollector = evidenceCollector;
        _diagnosticOrchestrator = diagnosticOrchestrator;
        _correlationIdGenerator = correlationIdGenerator;

        RoleOptions = Enum.GetValues<UserRole>();
        RunHealthCheckCommand = new AsyncRelayCommand(RunHealthCheckAsync);
        RunDiagnosticCommand = new AsyncRelayCommand(RunDiagnosticAsync);
        OpenEvidenceFolderCommand = new RelayCommand(OpenEvidenceFolder);
        PauseScanCommand = new RelayCommand(PauseScan);
        ResumeScanCommand = new RelayCommand(ResumeScan);
        StopScanCommand = new RelayCommand(StopScan);
    }

    [ObservableProperty]
    private string stationId = "ST-001";

    [ObservableProperty]
    private string stationType = "default-station";

    [ObservableProperty]
    private string productFamily = "default";

    [ObservableProperty]
    private UserRole selectedRole = UserRole.Operator;

    [ObservableProperty]
    private string statusText = "Ready.";

    [ObservableProperty]
    private int healthScore;

    [ObservableProperty]
    private string latestEvidencePath = "-";

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private string currentScanItem = "Idle";

    [ObservableProperty]
    private bool isPaused;

    public Array RoleOptions { get; }

    public ObservableCollection<CheckResult> HealthResults { get; } = [];
    public ObservableCollection<RunbookStepResult> RunbookResults { get; } = [];
    public ObservableCollection<HealingActionResult> HealingResults { get; } = [];

    public IAsyncRelayCommand RunHealthCheckCommand { get; }
    public IAsyncRelayCommand RunDiagnosticCommand { get; }
    public IRelayCommand OpenEvidenceFolderCommand { get; }
    public IRelayCommand PauseScanCommand { get; }
    public IRelayCommand ResumeScanCommand { get; }
    public IRelayCommand StopScanCommand { get; }

    private async Task RunHealthCheckAsync()
    {
        if (IsScanning)
        {
            StatusText = "A scan is already running.";
            return;
        }

        _scanCts = new CancellationTokenSource();
        _scanControl.Reset();
        IsPaused = false;

        try
        {
            StatusText = "Running health check...";
            IsScanning = true;
            CurrentScanItem = "Initializing health check";
            var stationContext = CreateStationContext();
            var profile = await _stationProfileProvider.LoadAsync(StationType, CancellationToken.None);
            var correlationId = _correlationIdGenerator.Create(stationContext);
            var executionContext = new CheckExecutionContext(
                stationContext,
                profile,
                SelectedRole,
                correlationId,
                WorkspaceLocator.LocateWorkspaceRoot());

            var progress = new Progress<string>(message => CurrentScanItem = message);
            var healthReport = await _healthCheckEngine.ExecuteAsync(
                executionContext,
                _scanCts.Token,
                progress,
                _scanControl.WaitIfPausedAsync);
            HealthResults.Clear();
            foreach (var result in healthReport.Results)
            {
                HealthResults.Add(result);
            }

            HealthScore = healthReport.Score;
            CurrentScanItem = "Health check completed";
            StatusText = "Health check completed.";
        }
        catch (OperationCanceledException)
        {
            CurrentScanItem = "Stopped by user";
            StatusText = "Scan stopped.";
        }
        catch (Exception ex)
        {
            CurrentScanItem = $"Stopped: {ex.Message}";
            StatusText = $"Health check failed: {ex.Message}";
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            _scanControl.Reset();
            IsPaused = false;
            IsScanning = false;
        }
    }

    private async Task RunDiagnosticAsync()
    {
        if (IsScanning)
        {
            StatusText = "A scan is already running.";
            return;
        }

        _scanCts = new CancellationTokenSource();
        _scanControl.Reset();
        IsPaused = false;

        try
        {
            StatusText = "Running full diagnosis...";
            IsScanning = true;
            CurrentScanItem = "Initializing diagnosis";
            var stationContext = CreateStationContext();
            var progress = new Progress<string>(message => CurrentScanItem = message);
            var runResult = await _diagnosticOrchestrator.ExecuteAsync(
                stationContext,
                StationType,
                SelectedRole,
                WorkspaceLocator.LocateWorkspaceRoot(),
                _correlationIdGenerator,
                _scanCts.Token,
                progress,
                _scanControl.WaitIfPausedAsync);

            HealthResults.Clear();
            foreach (var result in runResult.HealthReport.Results)
            {
                HealthResults.Add(result);
            }

            RunbookResults.Clear();
            foreach (var result in runResult.RunbookResult.Steps)
            {
                RunbookResults.Add(result);
            }

            HealingResults.Clear();
            foreach (var healing in runResult.HealingResults)
            {
                HealingResults.Add(healing);
            }

            HealthScore = runResult.HealthReport.Score;
            LatestEvidencePath = runResult.Evidence.ZipFilePath;
            CurrentScanItem = "Diagnosis completed";
            StatusText = $"Full diagnosis completed. Upload: {runResult.UploadResult.Message}";
        }
        catch (OperationCanceledException)
        {
            CurrentScanItem = "Stopped by user";
            StatusText = "Scan stopped.";
        }
        catch (Exception ex)
        {
            CurrentScanItem = $"Stopped: {ex.Message}";
            StatusText = $"Diagnosis failed: {ex.Message}";
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            _scanControl.Reset();
            IsPaused = false;
            IsScanning = false;
        }
    }

    private void PauseScan()
    {
        if (!IsScanning || IsPaused)
        {
            return;
        }

        _scanControl.Pause();
        IsPaused = true;
        StatusText = "Scan paused.";
        CurrentScanItem = "Paused";
    }

    private void ResumeScan()
    {
        if (!IsScanning || !IsPaused)
        {
            return;
        }

        _scanControl.Resume();
        IsPaused = false;
        StatusText = "Scan resumed.";
    }

    private void StopScan()
    {
        if (!IsScanning)
        {
            return;
        }

        _scanControl.Resume();
        _scanCts?.Cancel();
        IsPaused = false;
        StatusText = "Stopping scan...";
    }

    private void OpenEvidenceFolder()
    {
        var evidenceDir = Path.Combine(WorkspaceLocator.LocateWorkspaceRoot(), "artifacts", "evidence");
        Directory.CreateDirectory(evidenceDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = evidenceDir,
            UseShellExecute = true
        });
    }

    private StationContext CreateStationContext()
    {
        return new StationContext(StationId.Trim(), ProductFamily.Trim(), null);
    }
}
