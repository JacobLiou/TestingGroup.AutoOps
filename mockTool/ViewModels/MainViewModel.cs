using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockDiagTool.Models;
using MockDiagTool.Services;

namespace MockDiagTool.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private ObservableCollection<DiagnosticItem> _diagnosticItems = [];

    [ObservableProperty]
    private int _healthScore;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _scanComplete;

    [ObservableProperty]
    private string _currentScanItem = "就绪";

    [ObservableProperty]
    private string _statusText = "点击「开始体检」全面扫描您的电脑";

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
    private string _themeIcon = "🌗";

    [ObservableProperty]
    private string _themeModeText = "主题: 自动";

    public MainViewModel()
    {
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
        UpdateThemeProperties(ThemeService.Instance.CurrentMode, ThemeService.Instance.IsDarkTheme);
        ResetState();
    }

    private void ResetState()
    {
        var items = DiagnosticEngine.BuildCheckList();
        DiagnosticItems = new ObservableCollection<DiagnosticItem>(items);
        TotalItems = items.Count;
        ScannedItems = 0;
        HealthScore = 0;
        DisplayScore = 0;
        PassCount = 0;
        WarningCount = 0;
        FailCount = 0;
        IsScanning = false;
        ScanComplete = false;
        CurrentScanItem = "就绪";
        StatusText = "点击「开始体检」全面扫描您的电脑";
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (IsScanning) return;

        ResetState();
        IsScanning = true;
        ScanComplete = false;
        _cts = new CancellationTokenSource();
        StatusText = "正在扫描...";

        try
        {
            foreach (var item in DiagnosticItems)
            {
                if (_cts.Token.IsCancellationRequested) break;

                CurrentScanItem = $"{item.CategoryIcon} {item.Name}";
                await DiagnosticEngine.RunCheckAsync(item, _cts.Token);

                ScannedItems++;

                // live score update
                CalculateScore();
                await AnimateScoreAsync(DisplayScore, HealthScore);
            }

            ScanComplete = true;
            CurrentScanItem = "扫描完成";
            StatusText = $"体检完成！通过 {PassCount} 项 | 风险 {WarningCount} 项 | 异常 {FailCount} 项";
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
            CurrentScanItem = "已取消";
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
        StatusText = $"修复完成！健康评分: {HealthScore}";
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
        StatusText = $"已修复: {item.Name}";
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

    private void OnThemeChanged(AppTheme mode, bool isDark)
    {
        UpdateThemeProperties(mode, isDark);
    }

    private void UpdateThemeProperties(AppTheme mode, bool isDark)
    {
        ThemeIcon = isDark ? "🌙" : "☀️";
        
        string modeStr = mode switch
        {
            AppTheme.Auto => "自动",
            AppTheme.Dark => "深色",
            AppTheme.Light => "浅色",
            _ => "未知"
        };
        ThemeModeText = $"主题: {modeStr}";
    }
}
