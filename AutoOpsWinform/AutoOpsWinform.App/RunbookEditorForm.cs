using System.ComponentModel;
using System.Text.Json;
using MockDiagTool.Models;
using MockDiagTool.Services;

namespace AutoOpsWinform.App;

public sealed class RunbookEditorForm : Form
{
    private readonly RunbookFileService _runbookFileService = new();
    private readonly BindingList<RunbookStepRow> _rows = [];
    private readonly DataGridView _grid = new();
    private readonly TextBox _runbookIdBox = new();
    private readonly TextBox _runbookTitleBox = new();
    private readonly TextBox _runbookVersionBox = new();
    private readonly TextBox _saveAsIdBox = new();
    private readonly Label _statusLabel = new();

    public event EventHandler? RunbookSaved;

    public RunbookEditorForm(string runbookId = "default")
    {
        Width = 1320;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;
        InitializeUi();
        LoadRunbook(runbookId);
    }

    private void InitializeUi()
    {
        Text = T("Loc.Editor.Title", "RunBook 快速编辑器");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
        top.Controls.Add(new Label { AutoSize = true, Text = "Id:", Padding = new Padding(0, 8, 0, 0) });
        _runbookIdBox.Width = 100;
        top.Controls.Add(_runbookIdBox);
        top.Controls.Add(new Label { AutoSize = true, Text = "Title:", Padding = new Padding(0, 8, 0, 0) });
        _runbookTitleBox.Width = 180;
        top.Controls.Add(_runbookTitleBox);
        top.Controls.Add(new Label { AutoSize = true, Text = "Version:", Padding = new Padding(0, 8, 0, 0) });
        _runbookVersionBox.Width = 80;
        top.Controls.Add(_runbookVersionBox);

        AddButton(top, T("Loc.Editor.LoadDefault", "加载 Default"), (_, _) => LoadRunbook("default"));
        AddButton(top, T("Loc.Editor.Add", "新增步骤"), (_, _) => AddStep());
        AddButton(top, T("Loc.Editor.Remove", "删除步骤"), (_, _) => RemoveSelectedStep());
        AddButton(top, T("Loc.Editor.Up", "上移"), (_, _) => MoveSelected(-1));
        AddButton(top, T("Loc.Editor.Down", "下移"), (_, _) => MoveSelected(1));
        AddButton(top, T("Loc.Editor.Relink", "自动重连"), (_, _) => AutoRelink());
        AddButton(top, T("Loc.Editor.SaveDefault", "保存 Default"), (_, _) => SaveAs("default"));

        top.Controls.Add(new Label { AutoSize = true, Text = "SaveAs Id:", Padding = new Padding(0, 8, 0, 0) });
        _saveAsIdBox.Width = 120;
        top.Controls.Add(_saveAsIdBox);
        AddButton(top, T("Loc.Editor.SaveAs", "另存模板"), (_, _) => SaveAs(_saveAsIdBox.Text.Trim()));
        root.Controls.Add(top, 0, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.DataError += (_, e) =>
        {
            e.Cancel = true;
            _statusLabel.Text = $"数据错误: {e.Exception?.Message}";
        };
        _grid.CellEndEdit += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _rows.Count)
            {
                _rows[e.RowIndex].ValidateJson();
                PaintRowValidation(e.RowIndex);
            }
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunbookStepRow.StepId), HeaderText = "StepId", Width = 82 });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(RunbookStepRow.CheckId),
            HeaderText = "CheckId",
            Width = 140,
            DataSource = DiagnosticEngine.GetRegisteredCheckIds().ToList()
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunbookStepRow.DisplayName), HeaderText = "DisplayName", Width = 180 });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(RunbookStepRow.Category),
            HeaderText = "Category",
            Width = 170,
            DataSource = Enum.GetNames<CheckCategory>()
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(RunbookStepRow.Enabled), HeaderText = "Enabled", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunbookStepRow.TimeoutMs), HeaderText = "TimeoutMs", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunbookStepRow.NextOnSuccess), HeaderText = "NextOnSuccess", Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunbookStepRow.NextOnFailure), HeaderText = "NextOnFailure", Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunbookStepRow.ParamsJson), HeaderText = "ParamsJson", Width = 400 });
        _grid.DataSource = _rows;
        root.Controls.Add(_grid, 0, 1);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_statusLabel, 0, 2);
    }

    private static void AddButton(FlowLayoutPanel panel, string text, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        btn.Click += onClick;
        panel.Controls.Add(btn);
    }

    private void LoadRunbook(string runbookId)
    {
        try
        {
            var runbook = _runbookFileService.Load(runbookId);
            _runbookIdBox.Text = runbook.Id;
            _runbookTitleBox.Text = runbook.Title;
            _runbookVersionBox.Text = runbook.Version;
            _rows.Clear();
            foreach (var step in runbook.Steps)
            {
                _rows.Add(RunbookStepRow.FromDefinition(step));
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                PaintRowValidation(i);
            }
            _statusLabel.Text = $"已加载: {runbook.Id}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"加载失败: {ex.Message}";
        }
    }

    private void AddStep()
    {
        var used = _rows
            .Select(x => x.StepId)
            .Where(x => x.StartsWith("S", StringComparison.OrdinalIgnoreCase) && int.TryParse(x[1..], out _))
            .Select(x => int.Parse(x[1..]))
            .DefaultIfEmpty(0)
            .Max();

        _rows.Add(new RunbookStepRow
        {
            StepId = $"S{used + 1:000}",
            CheckId = DiagnosticEngine.GetRegisteredCheckIds().FirstOrDefault() ?? "SYS_01",
            DisplayName = "新步骤",
            Category = nameof(CheckCategory.SystemCheck),
            TimeoutMs = 5000,
            Enabled = true,
            ParamsJson = "{}"
        });
    }

    private void RemoveSelectedStep()
    {
        if (_grid.CurrentRow?.DataBoundItem is RunbookStepRow row)
        {
            _rows.Remove(row);
        }
    }

    private void MoveSelected(int delta)
    {
        if (_grid.CurrentRow?.DataBoundItem is not RunbookStepRow row)
        {
            return;
        }

        var currentIndex = _rows.IndexOf(row);
        var targetIndex = currentIndex + delta;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _rows.Count)
        {
            return;
        }

        _rows.RemoveAt(currentIndex);
        _rows.Insert(targetIndex, row);
        _grid.ClearSelection();
        _grid.Rows[targetIndex].Selected = true;
    }

    private void AutoRelink()
    {
        var defs = _rows.Select(r => r.ToDefinition()).ToList();
        _runbookFileService.AutoRelinkByEnabledOrder(defs);
        _rows.Clear();
        foreach (var def in defs)
        {
            _rows.Add(RunbookStepRow.FromDefinition(def));
        }
        _statusLabel.Text = "已按启用顺序自动重连";
    }

    private void SaveAs(string runbookId)
    {
        if (string.IsNullOrWhiteSpace(runbookId))
        {
            _statusLabel.Text = "请输入有效 RunBook Id";
            return;
        }

        try
        {
            foreach (var row in _rows)
            {
                row.ValidateJson();
                if (!row.IsParamsJsonValid)
                {
                    _statusLabel.Text = $"ParamsJson 错误: {row.StepId} - {row.ParamsJsonError}";
                    return;
                }
            }

            var runbook = new RunbookDefinition
            {
                Id = runbookId,
                Title = string.IsNullOrWhiteSpace(_runbookTitleBox.Text) ? "RunBook" : _runbookTitleBox.Text.Trim(),
                Version = string.IsNullOrWhiteSpace(_runbookVersionBox.Text) ? "1.0.0" : _runbookVersionBox.Text.Trim(),
                Steps = _rows.Select(r => r.ToDefinition()).ToList()
            };
            _runbookFileService.Save(runbook, runbookId);
            _runbookIdBox.Text = runbookId;
            _statusLabel.Text = $"保存成功: {runbookId}";
            RunbookSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"保存失败: {ex.Message}";
        }
    }

    private void PaintRowValidation(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || rowIndex >= _grid.Rows.Count)
        {
            return;
        }

        var row = _rows[rowIndex];
        _grid.Rows[rowIndex].DefaultCellStyle.BackColor = row.IsParamsJsonValid
            ? Color.White
            : Color.FromArgb(255, 232, 232);
        _grid.Rows[rowIndex].DefaultCellStyle.SelectionBackColor = row.IsParamsJsonValid
            ? Color.FromArgb(220, 240, 255)
            : Color.FromArgb(255, 200, 200);
    }

    private static string T(string key, string fallback) => LanguageService.Instance.Get(key, fallback);
}

public sealed class RunbookStepRow
{
    public string StepId { get; set; } = string.Empty;
    public string CheckId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = nameof(CheckCategory.SystemCheck);
    public int TimeoutMs { get; set; } = 5000;
    public bool Enabled { get; set; } = true;
    public string NextOnSuccess { get; set; } = string.Empty;
    public string NextOnFailure { get; set; } = string.Empty;
    public string ParamsJson { get; set; } = "{}";
    public bool IsParamsJsonValid { get; private set; } = true;
    public string ParamsJsonError { get; private set; } = string.Empty;

    public static RunbookStepRow FromDefinition(RunbookStepDefinition def)
    {
        return new RunbookStepRow
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
        ValidateJson();
        if (!IsParamsJsonValid)
        {
            throw new InvalidOperationException($"步骤 {StepId} 的 ParamsJson 非法: {ParamsJsonError}");
        }

        var paramMap = string.IsNullOrWhiteSpace(ParamsJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(ParamsJson) ?? [];

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
            Params = paramMap
        };
    }

    public void ValidateJson()
    {
        if (string.IsNullOrWhiteSpace(ParamsJson))
        {
            IsParamsJsonValid = true;
            ParamsJsonError = string.Empty;
            return;
        }

        try
        {
            JsonSerializer.Deserialize<Dictionary<string, string>>(ParamsJson);
            IsParamsJsonValid = true;
            ParamsJsonError = string.Empty;
        }
        catch (Exception ex)
        {
            IsParamsJsonValid = false;
            ParamsJsonError = ex.Message;
        }
    }
}
