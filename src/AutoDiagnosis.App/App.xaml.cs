using AutoDiagnosis.Application;
using AutoDiagnosis.Domain;
using AutoDiagnosis.Infrastructure;
using AutoDiagnosis.Runbook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;
using Serilog;
using WpfApplication = System.Windows.Application;

namespace AutoDiagnosis.App;

public partial class App : WpfApplication
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var workspaceRoot = AutoDiagnosis.Infrastructure.WorkspaceLocator.LocateWorkspaceRoot();
        var logDirectory = Path.Combine(workspaceRoot, "artifacts", "logs");
        Directory.CreateDirectory(logDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "autodiag-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<ICorrelationIdGenerator, CorrelationIdGenerator>();
                services.AddSingleton<IStationProfileProvider, StationProfileProvider>();
                services.AddSingleton<IRunbookProvider, FileRunbookProvider>();
                services.AddSingleton<IAuditLogger, JsonAuditLogger>();
                services.AddSingleton<IEvidenceCollector, EvidenceCollector>();
                services.AddSingleton<IEvidenceUploader, MockEvidenceUploader>();

                services.AddSingleton<ICheckItem, ExternalDependencyCheck>();
                services.AddSingleton<ICheckItem, SerialHardwareCheck>();
                services.AddSingleton<ICheckItem, StationServiceCheck>();
                services.AddSingleton<ICheckItem, BaselineVersionCheck>();
                services.AddSingleton<ICheckItem, TpReadonlyMeasureCheck>();

                services.AddSingleton<IHealingAction, ReconnectInstrumentsAction>();
                services.AddSingleton<IHealingAction, RestartServicesAction>();
                services.AddSingleton<IHealingAction, CleanupCacheAction>();

                services.AddSingleton<IHealthCheckEngine, HealthCheckEngine>();
                services.AddSingleton<IRunbookExecutor, RunbookExecutor>();
                services.AddSingleton<ISelfHealingService, SelfHealingService>();
                services.AddSingleton<DiagnosticOrchestrator>();

                services.AddSingleton<ViewModels.MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}

