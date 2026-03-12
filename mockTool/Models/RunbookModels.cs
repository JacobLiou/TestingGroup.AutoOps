namespace MockDiagTool.Models;

public sealed class RunbookDefinition
{
    public string Id { get; init; } = "default";
    public string Title { get; init; } = "默认诊断 RunBook";
    public string Version { get; init; } = "1.0.0";
    public List<RunbookStepDefinition> Steps { get; init; } = [];
}

public sealed class RunbookStepDefinition
{
    public string StepId { get; init; } = string.Empty;
    public string CheckId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = nameof(CheckCategory.SystemCheck);
    public int TimeoutMs { get; init; } = 5000;
    public bool Enabled { get; init; } = true;
    public string NextOnSuccess { get; init; } = string.Empty;
    public string NextOnFailure { get; init; } = string.Empty;
    public Dictionary<string, string> Params { get; init; } = [];
}

public sealed class CheckExecutionOutcome
{
    public bool Success { get; init; }
}
