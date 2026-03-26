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

        RunbookFileService.Validate(runbook);
        return runbook;
    }
}
