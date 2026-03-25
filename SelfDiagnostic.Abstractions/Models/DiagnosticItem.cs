using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SelfDiagnostic.Models
{
    public class DiagnosticItem : INotifyPropertyChanged
    {
        private CheckStatus _status = CheckStatus.Pending;
        private string _detail = string.Empty;
        private string _fixSuggestion = string.Empty;
        private int _score = 100;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CheckCategory Category { get; set; }

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

        public string Detail
        {
            get => _detail;
            set { if (_detail != value) { _detail = value; OnPropertyChanged(); } }
        }

        public string FixSuggestion
        {
            get => _fixSuggestion;
            set { if (_fixSuggestion != value) { _fixSuggestion = value; OnPropertyChanged(); } }
        }

        public int Score
        {
            get => _score;
            set { if (_score != value) { _score = value; OnPropertyChanged(); } }
        }

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
