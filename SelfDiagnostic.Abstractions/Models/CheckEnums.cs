namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 检查项执行状态枚举
    /// </summary>
    public enum CheckStatus
    {
        /// <summary>等待执行</summary>
        Pending,
        /// <summary>正在扫描中</summary>
        Scanning,
        /// <summary>检查通过</summary>
        Pass,
        /// <summary>存在警告</summary>
        Warning,
        /// <summary>检查失败</summary>
        Fail,
        /// <summary>已自动修复</summary>
        Fixed
    }

    /// <summary>
    /// 检查项分类枚举（对应 RunBook 中的 Category 字段）
    /// </summary>
    public enum CheckCategory
    {
        /// <summary>系统级检查</summary>
        SystemCheck,
        /// <summary>工站能力检查</summary>
        StationCheck,
        /// <summary>硬件/软件/固件配置检查</summary>
        HwSwFwCheck,
        /// <summary>硬件状态检查</summary>
        HwStatusCheck,
        /// <summary>光学性能检查</summary>
        OpticalPerformanceCheck
    }
}
