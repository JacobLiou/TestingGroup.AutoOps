using System.IO;
using System.Text.Json;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class RunbookProvider
{
    private const string DefaultRunbookRelativePath = @"config\runbook\default.runbook.json";

    public RunbookDefinition Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, DefaultRunbookRelativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"RunBook 配置文件不存在: {path}");
        }

        var json = File.ReadAllText(path);
        var runbook = JsonSerializer.Deserialize<RunbookDefinition>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (runbook is null)
        {
            throw new InvalidOperationException("RunBook 配置解析失败");
        }

        Validate(runbook);
        return runbook;
    }

    private static void Validate(RunbookDefinition runbook)
    {
        if (runbook.Steps.Count == 0)
        {
            throw new InvalidOperationException("RunBook 至少需要一个步骤");
        }

        var enabledSteps = runbook.Steps.Where(s => s.Enabled).ToList();
        if (enabledSteps.Count == 0)
        {
            throw new InvalidOperationException("RunBook 至少需要一个启用步骤");
        }

        var duplicated = enabledSteps
            .GroupBy(s => s.StepId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicated != null)
        {
            throw new InvalidOperationException($"RunBook 存在重复 StepId: {duplicated.Key}");
        }
    }
}
