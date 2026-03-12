namespace MockDiagTool.Models;

public sealed class MimsAskInfoRequest
{
    public string Author { get; init; } = "YUD";
    public string Spec { get; init; } = "GUI";
    public string PartNumber { get; init; } = "TEST-001";
    public DateTime Date { get; init; } = DateTime.Now;
    public int TotalItems { get; init; }
    public int PassCount { get; init; }
    public int WarningCount { get; init; }
    public int FailCount { get; init; }
}

public sealed class ExternalSendResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
}
