using CommunityToolkit.Mvvm.ComponentModel;
using MockDiagTool.Services;

namespace MockDiagTool.Models;

public enum CheckStatus
{
    Pending,
    Scanning,
    Pass,
    Warning,
    Fail,
    Fixed
}

public enum CheckCategory
{
    SystemCheck,
    StationCheck,
    HwSwFwCheck,
    HwStatusCheck,
    OpticalPerformanceCheck
}

public partial class DiagnosticItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CheckCategory Category { get; set; }
    public string CategoryIcon => Category switch
    {
        CheckCategory.SystemCheck => "🖥",
        CheckCategory.StationCheck => "🏭",
        CheckCategory.HwSwFwCheck => "🧰",
        CheckCategory.HwStatusCheck => "🔧",
        CheckCategory.OpticalPerformanceCheck => "📈",
        _ => "❓"
    };

    public string CategoryName => Category switch
    {
        CheckCategory.SystemCheck => LanguageService.Instance.Get("Loc.Category.SystemCheck", "System Check"),
        CheckCategory.StationCheck => LanguageService.Instance.Get("Loc.Category.StationCheck", "Station Check"),
        CheckCategory.HwSwFwCheck => LanguageService.Instance.Get("Loc.Category.HwSwFwCheck", "HW/SW/FW Check"),
        CheckCategory.HwStatusCheck => LanguageService.Instance.Get("Loc.Category.HwStatusCheck", "HW Status Check"),
        CheckCategory.OpticalPerformanceCheck => LanguageService.Instance.Get("Loc.Category.OpticalPerformanceCheck", "Optical Performance Check"),
        _ => "Unknown"
    };

    [ObservableProperty]
    private CheckStatus _status = CheckStatus.Pending;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private string _fixSuggestion = string.Empty;

    [ObservableProperty]
    private int _score = 100; // points contribution (0-100 scale, deducted if fail/warning)

    public string StatusText => Status switch
    {
        CheckStatus.Pending => LanguageService.Instance.Get("Loc.Status.Pending", "⏳ 等待检测"),
        CheckStatus.Scanning => LanguageService.Instance.Get("Loc.Status.Scanning", "🔍 正在扫描..."),
        CheckStatus.Pass => LanguageService.Instance.Get("Loc.Status.Pass", "✅ 正常"),
        CheckStatus.Warning => LanguageService.Instance.Get("Loc.Status.Warning", "⚠️ 存在风险"),
        CheckStatus.Fail => LanguageService.Instance.Get("Loc.Status.Fail", "❌ 异常"),
        CheckStatus.Fixed => LanguageService.Instance.Get("Loc.Status.Fixed", "🔧 已修复"),
        _ => ""
    };

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnStatusChanged(CheckStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
    }
}
