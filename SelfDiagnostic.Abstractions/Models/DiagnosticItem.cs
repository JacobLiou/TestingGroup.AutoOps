using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 单个诊断检查项的运行时数据模型，绑定到 UI Grid 的每一行。
    /// 实现 <see cref="INotifyPropertyChanged"/> 以支持实时 UI 刷新。
    /// </summary>
    public class DiagnosticItem : INotifyPropertyChanged
    {
        private CheckStatus _status = CheckStatus.Pending;
        private string _detail = string.Empty;
        private string _fixSuggestion = string.Empty;
        private int _score = 100;

        /// <summary>检查项唯一标识（对应 RunBook 中的 CheckId）</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>检查项显示名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>检查项所属分类</summary>
        public CheckCategory Category { get; set; }

        /// <summary>分类缩写图标（SYS / STA / CFG / HW / OPT）</summary>
        public string CategoryIcon
        {
            get
            {
                switch (Category)
                {
                    case CheckCategory.SystemCheck: return "SYS";
                    case CheckCategory.StationCheck: return "STA";
                    case CheckCategory.HwSwFwCheck: return "CFG";
                    case CheckCategory.HwStatusCheck: return "HW";
                    case CheckCategory.OpticalPerformanceCheck: return "OPT";
                    default: return "?";
                }
            }
        }

        /// <summary>分类完整显示名称</summary>
        public string CategoryName
        {
            get
            {
                switch (Category)
                {
                    case CheckCategory.SystemCheck: return "System Check";
                    case CheckCategory.StationCheck: return "Station Check";
                    case CheckCategory.HwSwFwCheck: return "HW/SW/FW Check";
                    case CheckCategory.HwStatusCheck: return "HW Status Check";
                    case CheckCategory.OpticalPerformanceCheck: return "Optical Performance Check";
                    default: return "Unknown";
                }
            }
        }

        /// <summary>当前执行状态（Pending → Scanning → Pass/Warning/Fail/Fixed）</summary>
        public CheckStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        /// <summary>检查结果详细描述</summary>
        public string Detail
        {
            get => _detail;
            set { if (_detail != value) { _detail = value; OnPropertyChanged(); } }
        }

        /// <summary>修复建议（当检查失败或警告时给出的操作指引）</summary>
        public string FixSuggestion
        {
            get => _fixSuggestion;
            set { if (_fixSuggestion != value) { _fixSuggestion = value; OnPropertyChanged(); } }
        }

        /// <summary>检查得分（0-100，100 为满分/通过）</summary>
        public int Score
        {
            get => _score;
            set { if (_score != value) { _score = value; OnPropertyChanged(); } }
        }

        /// <summary>状态的显示文本（供 Grid 列绑定）</summary>
        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case CheckStatus.Pending: return "Pending";
                    case CheckStatus.Scanning: return "Scanning...";
                    case CheckStatus.Pass: return "Pass";
                    case CheckStatus.Warning: return "Warning";
                    case CheckStatus.Fail: return "Fail";
                    case CheckStatus.Fixed: return "Fixed";
                    default: return string.Empty;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}