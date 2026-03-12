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
    System,
    Disk,
    Network,
    External,
    Security,
    Software,
    Performance
}

public partial class DiagnosticItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CheckCategory Category { get; set; }
    public string CategoryIcon => Category switch
    {
        CheckCategory.System => "🖥",
        CheckCategory.Disk => "💾",
        CheckCategory.Network => "🌐",
        CheckCategory.External => "🏭",
        CheckCategory.Security => "🛡",
        CheckCategory.Software => "📦",
        CheckCategory.Performance => "⚡",
        _ => "❓"
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
