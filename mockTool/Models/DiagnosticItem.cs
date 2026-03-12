using CommunityToolkit.Mvvm.ComponentModel;

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
        CheckCategory.SystemCheck => "System Check",
        CheckCategory.StationCheck => "Station Check",
        CheckCategory.HwSwFwCheck => "HW/SW/FW Check",
        CheckCategory.HwStatusCheck => "HW Status Check",
        CheckCategory.OpticalPerformanceCheck => "Optical Performance Check",
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
        CheckStatus.Pending => "⏳ 等待检测",
        CheckStatus.Scanning => "🔍 正在扫描...",
        CheckStatus.Pass => "✅ 正常",
        CheckStatus.Warning => "⚠️ 存在风险",
        CheckStatus.Fail => "❌ 异常",
        CheckStatus.Fixed => "🔧 已修复",
        _ => ""
    };

    partial void OnStatusChanged(CheckStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
    }
}
