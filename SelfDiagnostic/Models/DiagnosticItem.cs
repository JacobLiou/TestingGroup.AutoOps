using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SelfDiagnostic.Services;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 单个诊断检查项的运行时数据模型
    /// </summary>
    public class DiagnosticItem : INotifyPropertyChanged
    {
        private CheckStatus _status = CheckStatus.Pending;
        private string _detail = string.Empty;
        private string _fixSuggestion = string.Empty;
        private int _score = 100;

        /// <summary>
        /// 检查项唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 检查项显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 检查项所属分类
        /// </summary>
        public CheckCategory Category { get; set; }

        /// <summary>
        /// 分类对应的简短图标或标签文本
        /// </summary>
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

        /// <summary>
        /// 分类的本地化显示名称
        /// </summary>
        public string CategoryName
        {
            get
            {
                switch (Category)
                {
                    case CheckCategory.SystemCheck:
                        return LanguageService.Instance.Get("Loc.Category.SystemCheck", "System Check");
                    case CheckCategory.StationCheck:
                        return LanguageService.Instance.Get("Loc.Category.StationCheck", "Station Check");
                    case CheckCategory.HwSwFwCheck:
                        return LanguageService.Instance.Get("Loc.Category.HwSwFwCheck", "HW/SW/FW Check");
                    case CheckCategory.HwStatusCheck:
                        return LanguageService.Instance.Get("Loc.Category.HwStatusCheck", "HW Status Check");
                    case CheckCategory.OpticalPerformanceCheck:
                        return LanguageService.Instance.Get("Loc.Category.OpticalPerformanceCheck", "Optical Performance Check");
                    default:
                        return "Unknown";
                }
            }
        }

        /// <summary>
        /// 当前检查执行状态
        /// </summary>
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

        /// <summary>
        /// 检查结果详情说明
        /// </summary>
        public string Detail
        {
            get => _detail;
            set { if (_detail != value) { _detail = value; OnPropertyChanged(); }             }
        }

        /// <summary>
        /// 修复或处理建议
        /// </summary>
        public string FixSuggestion
        {
            get => _fixSuggestion;
            set { if (_fixSuggestion != value) { _fixSuggestion = value; OnPropertyChanged(); }             }
        }

        /// <summary>
        /// 检查项评分
        /// </summary>
        public int Score
        {
            get => _score;
            set { if (_score != value) { _score = value; OnPropertyChanged(); }             }
        }

        /// <summary>
        /// 状态的本地化显示文本
        /// </summary>
        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case CheckStatus.Pending: return LanguageService.Instance.Get("Loc.Status.Pending", "等待检测");
                    case CheckStatus.Scanning: return LanguageService.Instance.Get("Loc.Status.Scanning", "正在扫描...");
                    case CheckStatus.Pass: return LanguageService.Instance.Get("Loc.Status.Pass", "正常");
                    case CheckStatus.Warning: return LanguageService.Instance.Get("Loc.Status.Warning", "存在风险");
                    case CheckStatus.Fail: return LanguageService.Instance.Get("Loc.Status.Fail", "异常");
                    case CheckStatus.Fixed: return LanguageService.Instance.Get("Loc.Status.Fixed", "已修复");
                    default: return string.Empty;
                }
            }
        }

        public void RefreshLocalizedText()
        {
            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(StatusText));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
