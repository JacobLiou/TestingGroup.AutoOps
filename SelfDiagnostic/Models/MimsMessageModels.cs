using System;

namespace SelfDiagnostic.Models
{
    public sealed class MimsAskInfoRequest
    {
        public string Author { get; set; } = "YUD";
        public string Spec { get; set; } = "GUI";
        public string PartNumber { get; set; } = "TEST-001";
        public DateTime Date { get; set; } = DateTime.Now;
        public int TotalItems { get; set; }
        public int PassCount { get; set; }
        public int WarningCount { get; set; }
        public int FailCount { get; set; }
    }

    public sealed class MimsEnvironmentConfigRequest
    {
        public string StationId { get; set; } = "STATION-001";
        public string LineId { get; set; } = "LINE-001";
    }

    public sealed class ExternalSendResult
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
    }

    public sealed class MimsEnvironmentConfigResult
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string ConfigXml { get; set; } = string.Empty;
    }
}
