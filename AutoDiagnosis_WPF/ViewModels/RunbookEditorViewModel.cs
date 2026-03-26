using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockDiagTool.Models;
using MockDiagTool.Services;

namespace MockDiagTool.ViewModels;

public partial class RunbookEditorViewModel : ObservableObject
{
    private readonly RunbookFileService _runbookFileService;

    [ObservableProperty]
    private string _runbookId = "default";

    [ObservableProperty]
    private string _runbookTitle = string.Empty;

    [ObservableProperty]
    private string _runbookVersion = "1.0.0";

    [ObservableProperty]
    private string _saveAsRunbookId = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "已加载 default";

    [ObservableProperty]
    private RunbookStepEditorItem? _selectedStep;

    public ObservableCollection<RunbookStepEditorItem> Steps { get; } = [];

    public IReadOnlyList<string> CategoryOptions { get; } =
    [
        nameof(CheckCategory.SystemCheck),
        nameof(CheckCategory.StationCheck),
        nameof(CheckCategory.HwSwFwCheck),
        nameof(CheckCategory.HwStatusCheck),
        nameof(CheckCategory.OpticalPerformanceCheck)
    ];

    public IReadOnlyList<string> CheckIdOptions { get; } = DiagnosticEngine.GetRegisteredCheckIds();

    public bool SavedChanges { get; private set; }

    public RunbookEditorViewModel(RunbookFileService? runbookFileService = null, string runbookId = "default")
    {
        _runbookFileService = runbookFileService ?? new RunbookFileService();
        LoadRunbook(runbookId);
    }

    [RelayCommand]
    private void LoadDefault()
    {
        LoadRunbook("default");
    }

    [RelayCommand]
    private void AddStep()
    {
        var item = new RunbookStepEditorItem
        {
            StepId = BuildNextStepId(),
            CheckId = "NEW_CHECK_ID",
            DisplayName = "新测试项",
            Category = nameof(CheckCategory.SystemCheck),
            TimeoutMs = 5000,
            Enabled = true,
            NextOnSuccess = string.Empty,
            NextOnFailure = string.Empty,
            ParamsJson = "{}"
        };

        Steps.Add(item);
        SelectedStep = item;
        StatusMessage = $"已新增步骤 {item.StepId}";
    }

    [RelayCommand]
    private void RemoveStep(RunbookStepEditorItem? item)
    {
        if (item is null)
        {
            return;
        }

        var removedId = item.StepId;
        Steps.Remove(item);
        if (ReferenceEquals(SelectedStep, item))
        {
            SelectedStep = Steps.FirstOrDefault();
        }

        StatusMessage = $"已删除步骤 {removedId}";
    }

    [RelayCommand]
    private void MoveUp(RunbookStepEditorItem? item)
    {
        if (item is null)
        {
            return;
        }

        var index = Steps.IndexOf(item);
        if (index <= 0)
        {
            return;
        }

        Steps.Move(index, index - 1);
        StatusMessage = $"已上移步骤 {item.StepId}";
    }

    [RelayCommand]
    private void MoveDown(RunbookStepEditorItem? item)
    {
        if (item is null)
        {
            return;
        }

        var index = Steps.IndexOf(item);
        if (index < 0 || index >= Steps.Count - 1)
        {
            return;
        }

        Steps.Move(index, index + 1);
        StatusMessage = $"已下移步骤 {item.StepId}";
    }

    [RelayCommand]
    private void AutoRelink()
    {
        try
        {
            var definitions = Steps.Select(s => s.ToDefinition()).ToList();
            _runbookFileService.AutoRelinkByEnabledOrder(definitions);
            ReplaceStepsFromDefinitions(definitions);
            StatusMessage = "已按启用顺序自动重连 nextOnSuccess/nextOnFailure";
        }
        catch (Exception ex)
        {
            StatusMessage = $"自动重连失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveDefault()
    {
        SaveAsCore("default");
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (string.IsNullOrWhiteSpace(SaveAsRunbookId))
        {
            StatusMessage = "请输入另存 RunBook Id";
            return;
        }

        SaveAsCore(SaveAsRunbookId.Trim());
    }

    private void SaveAsCore(string runbookId)
    {
        try
        {
            var invalid = Steps.FirstOrDefault(s => !s.IsParamsJsonValid);
            if (invalid is not null)
            {
                SelectedStep = invalid;
                StatusMessage = $"保存失败: 步骤 {invalid.StepId} 的 ParamsJson 不是有效 JSON";
                return;
            }

            var runbook = BuildRunbook(runbookId);
            _runbookFileService.Save(runbook, runbookId);
            RunbookId = runbookId;
            SavedChanges = true;
            StatusMessage = $"保存成功: {runbookId}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
        }
    }

    private RunbookDefinition BuildRunbook(string runbookId)
    {
        return new RunbookDefinition
        {
            Id = runbookId,
            Title = string.IsNullOrWhiteSpace(RunbookTitle) ? "RunBook" : RunbookTitle.Trim(),
            Version = string.IsNullOrWhiteSpace(RunbookVersion) ? "1.0.0" : RunbookVersion.Trim(),
            Steps = Steps.Select(s => s.ToDefinition()).ToList()
        };
    }

    private void LoadRunbook(string runbookId)
    {
        try
        {
            var runbook = _runbookFileService.Load(runbookId);
            RunbookId = runbook.Id;
            RunbookTitle = runbook.Title;
            RunbookVersion = runbook.Version;
            ReplaceStepsFromDefinitions(runbook.Steps);
            StatusMessage = $"已加载 RunBook: {runbook.Id}";
            SavedChanges = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    private void ReplaceStepsFromDefinitions(IEnumerable<RunbookStepDefinition> definitions)
    {
        Steps.Clear();
        foreach (var def in definitions)
        {
            Steps.Add(RunbookStepEditorItem.FromDefinition(def));
        }

        SelectedStep = Steps.FirstOrDefault();
    }

    private string BuildNextStepId()
    {
        const string prefix = "S";
        var used = Steps
            .Select(s => s.StepId)
            .Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id.AsSpan(1), out _))
            .Select(id => int.Parse(id.AsSpan(1)))
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{used + 1:000}";
    }
}

public partial class RunbookStepEditorItem : ObservableObject
{
    [ObservableProperty]
    private string _stepId = string.Empty;

    [ObservableProperty]
    private string _checkId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _category = nameof(CheckCategory.SystemCheck);

    [ObservableProperty]
    private int _timeoutMs = 5000;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private string _nextOnSuccess = string.Empty;

    [ObservableProperty]
    private string _nextOnFailure = string.Empty;

    [ObservableProperty]
    private string _paramsJson = "{}";

    [ObservableProperty]
    private bool _isParamsJsonValid = true;

    [ObservableProperty]
    private string _paramsJsonError = string.Empty;

    public static RunbookStepEditorItem FromDefinition(RunbookStepDefinition def)
    {
        return new RunbookStepEditorItem
        {
            StepId = def.StepId,
            CheckId = def.CheckId,
            DisplayName = def.DisplayName,
            Category = string.IsNullOrWhiteSpace(def.Category) ? nameof(CheckCategory.SystemCheck) : def.Category,
            TimeoutMs = def.TimeoutMs,
            Enabled = def.Enabled,
            NextOnSuccess = def.NextOnSuccess,
            NextOnFailure = def.NextOnFailure,
            ParamsJson = JsonSerializer.Serialize(def.Params, new JsonSerializerOptions { WriteIndented = true })
        };
    }

    public RunbookStepDefinition ToDefinition()
    {
        if (!IsParamsJsonValid)
        {
            throw new InvalidOperationException($"步骤 {StepId} 的 ParamsJson 非法: {ParamsJsonError}");
        }

        Dictionary<string, string> parsed;
        if (string.IsNullOrWhiteSpace(ParamsJson))
        {
            parsed = [];
        }
        else
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(ParamsJson)
                ?? [];
        }

        return new RunbookStepDefinition
        {
            StepId = StepId.Trim(),
            CheckId = CheckId.Trim(),
            DisplayName = DisplayName.Trim(),
            Category = string.IsNullOrWhiteSpace(Category) ? nameof(CheckCategory.SystemCheck) : Category.Trim(),
            TimeoutMs = TimeoutMs <= 0 ? 5000 : TimeoutMs,
            Enabled = Enabled,
            NextOnSuccess = NextOnSuccess.Trim(),
            NextOnFailure = NextOnFailure.Trim(),
            Params = parsed
        };
    }

    partial void OnParamsJsonChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IsParamsJsonValid = true;
            ParamsJsonError = string.Empty;
            return;
        }

        try
        {
            JsonSerializer.Deserialize<Dictionary<string, string>>(value);
            IsParamsJsonValid = true;
            ParamsJsonError = string.Empty;
        }
        catch (JsonException ex)
        {
            IsParamsJsonValid = false;
            ParamsJsonError = $"JSON 格式错误: {ex.Message}";
        }
        catch (Exception ex)
        {
            IsParamsJsonValid = false;
            ParamsJsonError = $"JSON 校验失败: {ex.Message}";
        }
    }
}
