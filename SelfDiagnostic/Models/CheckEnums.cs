namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 检查项执行状态枚举
    /// </summary>
    public enum CheckStatus
    {
        Pending,
        Scanning,
        Pass,
        Warning,
        Fail,
        Fixed
    }

    /// <summary>
    /// 检查项分类枚举
    /// </summary>
    public enum CheckCategory
    {
        SystemCheck,
        StationCheck,
        HwSwFwCheck,
        HwStatusCheck,
        OpticalPerformanceCheck
    }
}
