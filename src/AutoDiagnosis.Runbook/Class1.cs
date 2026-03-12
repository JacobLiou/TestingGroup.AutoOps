using System.Text.Json;
using AutoDiagnosis.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoDiagnosis.Runbook;

public sealed class FileRunbookProvider : IRunbookProvider
{
    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<RunbookDefinition> LoadAsync(string productFamily, CancellationToken cancellationToken)
    {
        var workspaceRoot = WorkspaceLocator.LocateWorkspaceRoot();
        var runbookDir = Path.Combine(workspaceRoot, "config", "runbooks");
        var yamlPath = Path.Combine(runbookDir, $"{productFamily}.yaml");
        var jsonPath = Path.Combine(runbookDir, $"{productFamily}.json");

        RunbookDefinition runbook;
        if (File.Exists(yamlPath))
        {
            var content = await File.ReadAllTextAsync(yamlPath, cancellationToken);
            var dto = _yamlDeserializer.Deserialize<RunbookDto>(content);
            runbook = dto.ToDomain(productFamily);
        }
        else if (File.Exists(jsonPath))
        {
            var content = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            var dto = JsonSerializer.Deserialize<RunbookDto>(content)
                      ?? throw new InvalidDataException("Runbook JSON is invalid.");
            runbook = dto.ToDomain(productFamily);
        }
        else
        {
            throw new FileNotFoundException($"Runbook file not found for product family: {productFamily}");
        }

        Validate(runbook);
        return runbook;
    }

    private static void Validate(RunbookDefinition runbook)
    {
        if (runbook.Steps.Count == 0)
        {
            throw new InvalidDataException("Runbook must contain at least one step.");
        }

        var stepIds = runbook.Steps.Select(step => step.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var step in runbook.Steps)
        {
            if (!string.IsNullOrWhiteSpace(step.NextOnSuccess) && !stepIds.Contains(step.NextOnSuccess))
            {
                throw new InvalidDataException($"NextOnSuccess target not found: {step.NextOnSuccess}");
            }

            if (!string.IsNullOrWhiteSpace(step.NextOnFailure) && !stepIds.Contains(step.NextOnFailure))
            {
                throw new InvalidDataException($"NextOnFailure target not found: {step.NextOnFailure}");
            }
        }
    }
}

public static class WorkspaceLocator
{
    public static string LocateWorkspaceRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var candidate in candidates)
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "config")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}

public sealed class RunbookDto
{
    public string Id { get; set; } = "default-runbook";
    public string Name { get; set; } = "Default Runbook";
    public List<RunbookStepDto> Steps { get; set; } = [];

    public RunbookDefinition ToDomain(string productFamily)
    {
        return new RunbookDefinition(
            Id,
            Name,
            productFamily,
            Steps.Select(step => new RunbookStepDefinition(
                step.Id,
                step.Name,
                step.CheckId,
                step.NextOnSuccess,
                step.NextOnFailure)).ToList());
    }
}

public sealed class RunbookStepDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CheckId { get; set; } = string.Empty;
    public string? NextOnSuccess { get; set; }
    public string? NextOnFailure { get; set; }
}
