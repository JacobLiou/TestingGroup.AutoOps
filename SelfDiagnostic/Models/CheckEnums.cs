namespace SelfDiagnostic.Models
{
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
}
