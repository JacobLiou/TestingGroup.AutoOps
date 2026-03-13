using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockDiagTool.Models;
using MockDiagTool.Services;
using MockDiagTool.Services.Abstractions;

namespace MockDiagTool.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string DefaultMimsAuthor = "YUD";
    private const string DefaultMimsSpec = "GUI";
    private const string DefaultMimsPartNumber = "TEST-001";
    private const string DefaultStationId = "STATION-001";
    private const string DefaultLineId = "LINE-001";

    private CancellationTokenSource? _cts;
    private readonly IExternalSystemClient _externalSystemClient;
    private readonly MimsConfigXmlParser _mimsConfigXmlParser;
    private readonly MimsStationCapabilityParser _mimsStationCapabilityParser;
    private readonly MimsPowerSupplyParser _mimsPowerSupplyParser;
    private readonly TpConnectivityInspector _tpConnectivityInspector;
    private RunbookDefinition? _activeRunbook;
    private Dictionary<string, RunbookStepDefinition> _runbookStepsByStepId = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpdatingLanguageSelection;

    [ObservableProperty]
    private ObservableCollection<DiagnosticItem> _diagnosticItems = [];

    [ObservableProperty]
    private int _healthScore;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _scanComplete;

    [ObservableProperty]
    private string _currentScanItem = string.Empty;

    [ObservableProperty]
    private DiagnosticItem? _currentDiagnosticItem;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private int _scannedItems;

    [ObservableProperty]
    private int _passCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _failCount;

    [ObservableProperty]
    private int _displayScore;

    [ObservableProperty]
    private string _themeIcon = "☀️";

    [ObservableProperty]
    private string _themeModeText = string.Empty;

    [ObservableProperty]
    private bool _isReportingToMims;

    [ObservableProperty]
    private string _externalConfigStatus = string.Empty;

    [ObservableProperty]
    private string _currentRunbookName = "default";

    [ObservableProperty]
    private string _selectedLanguageCode = LanguageService.ZhCn;

    [ObservableProperty]
    private string _scannedProgressText = string.Empty;

    public event EventHandler? RunbookEditorRequested;

    public MainViewModel()
    {
        _externalSystemClient = new MimsGrpcClient(new MimsXmlBuilder());
        _mimsConfigXmlParser = new MimsConfigXmlParser();
        _mimsStationCapabilityParser = new MimsStationCapabilityParser();
        _mimsPowerSupplyParser = new MimsPowerSupplyParser();
        _tpConnectivityInspector = new TpConnectivityInspector();
        SelectedLanguageCode = LanguageService.Instance.CurrentLanguage;
        LanguageService.Instance.LanguageChanged += OnLanguageChanged;
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
        UpdateThemeProperties(ThemeService.Instance.CurrentMode, ThemeService.Instance.IsDarkTheme);
        ResetState();
    }

    private void ResetState()
    {
        _activeRunbook = DiagnosticEngine.LoadRunbook();
        _runbookStepsByStepId = _activeRunbook.Steps
            .Where(s => s.Enabled)
            .ToDictionary(s => s.StepId, s => s, StringComparer.OrdinalIgnoreCase);

        var items = DiagnosticEngine.BuildCheckList(_activeRunbook);
        DiagnosticItems = new ObservableCollection<DiagnosticItem>(items);
        CurrentRunbookName = string.IsNullOrWhiteSpace(_activeRunbook.Id) ? "default" : _activeRunbook.Id;
        TotalItems = items.Count;
        ScannedItems = 0;
        HealthScore = 0;
        DisplayScore = 0;
        PassCount = 0;
        WarningCount = 0;
        FailCount = 0;
        IsScanning = false;
        ScanComplete = false;
        CurrentScanItem = T("Loc.Runtime.Ready", "就绪");
        CurrentDiagnosticItem = null;
        IsReportingToMims = false;
        ExternalConfigStatus = TF("Loc.Runtime.ConfigUnavailable", "外部配置不可用: {0}", "未获取 MIMS 外部依赖配置");
        StatusText = TF("Loc.Runtime.ClickRunbook", "点击「开始体检」执行 RunBook：{0}", CurrentRunbookName);
        UpdateScannedProgressText();
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (IsScanning) return;

        ResetState();
        IsScanning = true;
        ScanComplete = false;
        _cts = new CancellationTokenSource();
        StatusText = T("Loc.Runtime.Scanning", "正在扫描...");

        try
        {
            StatusText = T("Loc.Runtime.FetchMimsConfig", "正在向 MIMS 获取外部系统配置...");
            var runContext = await BuildRunContextAsync(_cts.Token);
            if (runContext.ExternalChecksEnabled)
            {
                ExternalConfigStatus = TF("Loc.Runtime.ConfigSource", "外部配置来源: {0}", runContext.ConfigSource);
                StatusText = T("Loc.Runtime.ConfigReady", "已获取 MIMS 配置，开始执行外部接口与本机诊断...");
            }
            else
            {
                ExternalConfigStatus = TF("Loc.Runtime.ConfigUnavailable", "外部配置不可用: {0}", runContext.ConfigError);
                StatusText = T("Loc.Runtime.ConfigFailed", "MIMS 配置获取失败，外部依赖项将标记为跳过");
            }

            var currentStep = _activeRunbook?.Steps.FirstOrDefault(s => s.Enabled);
            var stepGuard = 0;
            var maxStepGuard = Math.Max(1, _runbookStepsByStepId.Count * 4);

            while (currentStep != null && stepGuard < maxStepGuard)
            {
                if (_cts.Token.IsCancellationRequested) break;

                var item = DiagnosticItems.FirstOrDefault(i => i.Id.Equals(currentStep.CheckId, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    break;
                }

                CurrentDiagnosticItem = item;
                CurrentScanItem = $"{item.CategoryIcon} {item.Name}";
                var outcome = await DiagnosticEngine.RunCheckAsync(item, currentStep, runContext, _cts.Token);

                ScannedItems++;

                // live score update
                CalculateScore();
                await AnimateScoreAsync(DisplayScore, HealthScore);

                var nextStepId = outcome.Success ? currentStep.NextOnSuccess : currentStep.NextOnFailure;
                if (string.IsNullOrWhiteSpace(nextStepId))
                {
                    currentStep = null;
                }
                else
                {
                    _runbookStepsByStepId.TryGetValue(nextStepId, out currentStep);
                }

                stepGuard++;
            }

            ScanComplete = true;
            CurrentScanItem = T("Loc.Runtime.ScanComplete", "扫描完成");
            CurrentDiagnosticItem = null;
            StatusText = TF("Loc.Runtime.Summary", "体检完成！通过 {0} 项 | 风险 {1} 项 | 异常 {2} 项", PassCount, WarningCount, FailCount);
            await SendToMimsCoreAsync(trigger: "auto", cancellationToken: CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            StatusText = T("Loc.Runtime.ScanCancelled", "扫描已取消");
            CurrentScanItem = T("Loc.Runtime.Cancelled", "已取消");
            CurrentDiagnosticItem = null;
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task FixAllAsync()
    {
        foreach (var item in DiagnosticItems)
        {
            if (item.Status is CheckStatus.Warning or CheckStatus.Fail)
            {
                item.Status = CheckStatus.Scanning;
                await Task.Delay(400);
                item.Status = CheckStatus.Fixed;
                item.Score = 100;
                item.Detail += " [已修复]";
            }
        }
        CalculateScore();
        await AnimateScoreAsync(DisplayScore, HealthScore);
        StatusText = TF("Loc.Runtime.FixDone", "修复完成！健康评分: {0}", HealthScore);
        WarningCount = 0;
        FailCount = 0;
        PassCount = DiagnosticItems.Count;
    }

    [RelayCommand]
    private async Task FixItemAsync(DiagnosticItem? item)
    {
        if (item == null || item.Status is not (CheckStatus.Warning or CheckStatus.Fail)) return;

        item.Status = CheckStatus.Scanning;
        await Task.Delay(600);
        item.Status = CheckStatus.Fixed;
        item.Score = 100;
        item.Detail += " [已修复]";

        CalculateScore();
        await AnimateScoreAsync(DisplayScore, HealthScore);
        UpdateCounts();
        StatusText = TF("Loc.Runtime.FixedItem", "已修复: {0}", item.Name);
    }

    private void CalculateScore()
    {
        if (DiagnosticItems.Count == 0) { HealthScore = 100; return; }

        var scored = DiagnosticItems.Where(i => i.Status is not (CheckStatus.Pending or CheckStatus.Scanning)).ToList();
        if (scored.Count == 0) { HealthScore = 100; return; }

        var total = scored.Sum(i => i.Score);
        HealthScore = total / scored.Count;

        UpdateCounts();
    }

    private void UpdateCounts()
    {
        PassCount = DiagnosticItems.Count(i => i.Status is CheckStatus.Pass or CheckStatus.Fixed);
        WarningCount = DiagnosticItems.Count(i => i.Status == CheckStatus.Warning);
        FailCount = DiagnosticItems.Count(i => i.Status == CheckStatus.Fail);
    }

    private async Task AnimateScoreAsync(int from, int to)
    {
        if (from == to) { DisplayScore = to; return; }
        var step = from < to ? 1 : -1;
        for (var i = from; i != to; i += step)
        {
            DisplayScore = i;
            await Task.Delay(15);
        }
        DisplayScore = to;
    }

    [RelayCommand]
    private void StopScan()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.Instance.CycleTheme();
    }

    [RelayCommand(CanExecute = nameof(CanOpenRunbookEditor))]
    private void OpenRunbookEditor()
    {
        RunbookEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanOpenRunbookEditor()
    {
        return !IsScanning;
    }

    [RelayCommand(CanExecute = nameof(CanSendToMims))]
    private async Task SendToMimsAsync()
    {
        await SendToMimsCoreAsync(trigger: "manual", cancellationToken: CancellationToken.None);
    }

    [RelayCommand]
    private void SwitchLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return;
        }

        SelectedLanguageCode = languageCode;
    }

    private bool CanSendToMims()
    {
        return !IsScanning && !IsReportingToMims && (ScanComplete || ScannedItems > 0);
    }

    private void OnThemeChanged(AppTheme mode, bool isDark)
    {
        UpdateThemeProperties(mode, isDark);
    }

    private void OnLanguageChanged(string language)
    {
        _isUpdatingLanguageSelection = true;
        SelectedLanguageCode = language;
        _isUpdatingLanguageSelection = false;
        ThemeModeText = T("Loc.Theme.Light", "主题: 浅色");
        UpdateScannedProgressText();
        foreach (var item in DiagnosticItems)
        {
            item.RefreshLocalizedText();
        }
        if (!IsScanning && !ScanComplete)
        {
            CurrentScanItem = T("Loc.Runtime.Ready", "就绪");
            StatusText = TF("Loc.Runtime.ClickRunbook", "点击「开始体检」执行 RunBook：{0}", CurrentRunbookName);
        }
    }

    private void UpdateThemeProperties(AppTheme mode, bool isDark)
    {
        ThemeIcon = "☀️";
        ThemeModeText = T("Loc.Theme.Light", "主题: 浅色");
    }

    partial void OnIsScanningChanged(bool value)
    {
        SendToMimsCommand.NotifyCanExecuteChanged();
        OpenRunbookEditorCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsReportingToMimsChanged(bool value)
    {
        SendToMimsCommand.NotifyCanExecuteChanged();
    }

    partial void OnScanCompleteChanged(bool value)
    {
        SendToMimsCommand.NotifyCanExecuteChanged();
    }

    partial void OnScannedItemsChanged(int value)
    {
        SendToMimsCommand.NotifyCanExecuteChanged();
        UpdateScannedProgressText();
    }

    partial void OnTotalItemsChanged(int value)
    {
        UpdateScannedProgressText();
    }

    partial void OnSelectedLanguageCodeChanged(string value)
    {
        if (_isUpdatingLanguageSelection)
        {
            return;
        }

        LanguageService.Instance.SetLanguage(value);
    }

    private async Task SendToMimsCoreAsync(string trigger, CancellationToken cancellationToken)
    {
        if (IsReportingToMims)
        {
            return;
        }

        IsReportingToMims = true;
        try
        {
            var request = BuildMimsRequest();
            var result = await _externalSystemClient.SendAskInfoAsync(request, cancellationToken);

            if (result.Success)
            {
                StatusText = trigger == "auto"
                    ? TF("Loc.Runtime.AutoReportSuccess", "{0} | 已自动上报 MIMS", StatusText)
                    : TF("Loc.Runtime.ManualReportSuccess", "手动上报成功: {0} ({1})", result.Code, result.Endpoint);
                return;
            }

            StatusText = trigger == "auto"
                ? TF("Loc.Runtime.AutoReportFailed", "{0} | MIMS 自动上报失败: {1}", StatusText, result.Code)
                : TF("Loc.Runtime.ManualReportFailed", "手动上报失败: {0} - {1}", result.Code, result.Message);
        }
        catch (Exception ex)
        {
            StatusText = trigger == "auto"
                ? TF("Loc.Runtime.AutoReportException", "{0} | MIMS 自动上报异常: {1}", StatusText, ex.Message)
                : TF("Loc.Runtime.ManualReportException", "手动上报异常: {0}", ex.Message);
        }
        finally
        {
            IsReportingToMims = false;
        }
    }

    private MimsAskInfoRequest BuildMimsRequest()
    {
        return new MimsAskInfoRequest
        {
            Author = DefaultMimsAuthor,
            Spec = DefaultMimsSpec,
            PartNumber = DefaultMimsPartNumber,
            Date = DateTime.Now,
            TotalItems = TotalItems,
            PassCount = PassCount,
            WarningCount = WarningCount,
            FailCount = FailCount
        };
    }

    private async Task<DiagnosticRunContext> BuildRunContextAsync(CancellationToken cancellationToken)
    {
        var configRequest = new MimsEnvironmentConfigRequest
        {
            StationId = DefaultStationId,
            LineId = DefaultLineId
        };
        var configResult = await _externalSystemClient.GetEnvironmentConfigAsync(configRequest, cancellationToken);
        var tpSnapshot = await _tpConnectivityInspector.InspectAsync(cancellationToken);
        if (!configResult.Success)
        {
            return new DiagnosticRunContext
            {
                ExternalChecksEnabled = false,
                ConfigError = $"{configResult.Code} - {configResult.Message}",
                TpConnectivity = tpSnapshot
            };
        }

        var parsed = _mimsConfigXmlParser.ParseOrDefault(configResult.ConfigXml);
        var capabilityRequirements = _mimsStationCapabilityParser.ParseOrDefault(configResult.ConfigXml);
        var powerRequirements = _mimsPowerSupplyParser.ParseOrDefault(configResult.ConfigXml);
        return new DiagnosticRunContext
        {
            ExternalChecksEnabled = true,
            ExternalConfig = parsed,
            ConfigSource = $"MIMS({configResult.Endpoint})",
            TpConnectivity = tpSnapshot,
            StationCapabilityRequirements = capabilityRequirements,
            PowerSupplyRequirements = powerRequirements,
            RawMimsConfigXml = configResult.ConfigXml
        };
    }

    public void ReloadRunbook()
    {
        if (IsScanning)
        {
            return;
        }

        ResetState();
    }

    private static string T(string key, string fallback)
    {
        return LanguageService.Instance.Get(key, fallback);
    }

    private static string TF(string key, string fallbackFormat, params object[] args)
    {
        return LanguageService.Instance.Format(key, fallbackFormat, args);
    }

    private void UpdateScannedProgressText()
    {
        ScannedProgressText = TF("Loc.Main.ScannedProgress", "已扫描 {0} / {1} 项", ScannedItems, TotalItems);
    }
}
