using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using Newtonsoft.Json;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services;

namespace SelfDiagnostic.UI
{
    public sealed class RunbookEditorForm : XtraForm
    {
        private readonly RunbookFileService _runbookFileService = new RunbookFileService();
        private readonly BindingList<RunbookStepRow> _rows = new BindingList<RunbookStepRow>();

        private GridControl _gridControl;
        private GridView _gridView;
        private TextEdit _runbookIdBox;
        private TextEdit _runbookTitleBox;
        private TextEdit _runbookVersionBox;
        private TextEdit _saveAsIdBox;
        private LabelControl _statusLabel;

        public event EventHandler RunbookSaved;

        public RunbookEditorForm(string runbookId = "default")
        {
            Width = 1320;
            Height = 800;
            StartPosition = FormStartPosition.CenterParent;
            Text = T("Loc.Editor.Title", "RunBook 快速编辑器");
            InitializeUi();
            LoadRunbook(runbookId);
        }

        private void InitializeUi()
        {
            SuspendLayout();

            var rootPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            Controls.Add(rootPanel);

            // -- Top toolbar --
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };

            toolbar.Controls.Add(MakeLabel("Id:"));
            _runbookIdBox = new TextEdit { Width = 100 };
            toolbar.Controls.Add(_runbookIdBox);

            toolbar.Controls.Add(MakeLabel("Title:"));
            _runbookTitleBox = new TextEdit { Width = 180 };
            toolbar.Controls.Add(_runbookTitleBox);

            toolbar.Controls.Add(MakeLabel("Version:"));
            _runbookVersionBox = new TextEdit { Width = 80 };
            toolbar.Controls.Add(_runbookVersionBox);

            AddToolButton(toolbar, T("Loc.Editor.LoadDefault", "加载 Default"), (s, e) => LoadRunbook("default"));
            AddToolButton(toolbar, T("Loc.Editor.Add", "新增步骤"), (s, e) => AddStep());
            AddToolButton(toolbar, T("Loc.Editor.Remove", "删除步骤"), (s, e) => RemoveSelectedStep());
            AddToolButton(toolbar, T("Loc.Editor.Up", "上移"), (s, e) => MoveSelected(-1));
            AddToolButton(toolbar, T("Loc.Editor.Down", "下移"), (s, e) => MoveSelected(1));
            AddToolButton(toolbar, T("Loc.Editor.Relink", "自动重连"), (s, e) => AutoRelink());
            AddToolButton(toolbar, T("Loc.Editor.SaveDefault", "保存 Default"), (s, e) => SaveAs("default"));

            toolbar.Controls.Add(MakeLabel("SaveAs Id:"));
            _saveAsIdBox = new TextEdit { Width = 120 };
            toolbar.Controls.Add(_saveAsIdBox);
            AddToolButton(toolbar, T("Loc.Editor.SaveAs", "另存模板"), (s, e) => SaveAs(_saveAsIdBox.Text.Trim()));

            rootPanel.Controls.Add(toolbar, 0, 0);

            // -- Grid --
            _gridControl = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_gridControl);
            _gridControl.MainView = _gridView;
            _gridControl.DataSource = _rows;

            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ShowIndicator = false;
            _gridView.OptionsBehavior.Editable = true;

            _gridView.Columns.AddVisible("StepId", "StepId").Width = 82;
            _gridView.Columns.AddVisible("CheckId", "CheckId").Width = 140;
            _gridView.Columns.AddVisible("DisplayName", "DisplayName").Width = 180;
            _gridView.Columns.AddVisible("Category", "Category").Width = 160;
            _gridView.Columns.AddVisible("Enabled", "Enabled").Width = 70;
            _gridView.Columns.AddVisible("TimeoutMs", "TimeoutMs").Width = 90;
            _gridView.Columns.AddVisible("NextOnSuccess", "NextOnSuccess").Width = 100;
            _gridView.Columns.AddVisible("NextOnFailure", "NextOnFailure").Width = 100;
            _gridView.Columns.AddVisible("ParamsJson", "ParamsJson").Width = 400;

            rootPanel.Controls.Add(_gridControl, 0, 1);

            // -- Status bar --
            _statusLabel = new LabelControl { Dock = DockStyle.Fill };
            rootPanel.Controls.Add(_statusLabel, 0, 2);

            ResumeLayout();
        }

        private static LabelControl MakeLabel(string text)
        {
            return new LabelControl { Text = text, AutoSizeMode = LabelAutoSizeMode.Default, Padding = new Padding(0, 6, 4, 0) };
        }

        private static void AddToolButton(FlowLayoutPanel panel, string text, EventHandler onClick)
        {
            var btn = new SimpleButton { Text = text, AutoSize = true };
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
                _statusLabel.Text = "已加载: " + runbook.Id;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "加载失败: " + ex.Message;
            }
        }

        private void AddStep()
        {
            var maxNum = _rows
                .Select(x => x.StepId)
                .Where(x => x.StartsWith("S", StringComparison.OrdinalIgnoreCase) && x.Length > 1 && int.TryParse(x.Substring(1), out _))
                .Select(x => int.Parse(x.Substring(1)))
                .DefaultIfEmpty(0)
                .Max();

            _rows.Add(new RunbookStepRow
            {
                StepId = "S" + (maxNum + 1).ToString("000"),
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
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _rows.Count) return;
            _rows.RemoveAt(handle);
        }

        private void MoveSelected(int delta)
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0) return;
            var target = handle + delta;
            if (target < 0 || target >= _rows.Count) return;

            var row = _rows[handle];
            _rows.RemoveAt(handle);
            _rows.Insert(target, row);
            _gridView.FocusedRowHandle = target;
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
                        _statusLabel.Text = "ParamsJson 错误: " + row.StepId + " - " + row.ParamsJsonError;
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
                _statusLabel.Text = "保存成功: " + runbookId;
                RunbookSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "保存失败: " + ex.Message;
            }
        }

        private static string T(string key, string fallback)
        {
            return LanguageService.Instance.Get(key, fallback);
        }
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
                ParamsJson = JsonConvert.SerializeObject(def.Params, Formatting.Indented)
            };
        }

        public RunbookStepDefinition ToDefinition()
        {
            ValidateJson();
            if (!IsParamsJsonValid)
            {
                throw new InvalidOperationException("步骤 " + StepId + " 的 ParamsJson 非法: " + ParamsJsonError);
            }

            var paramMap = string.IsNullOrWhiteSpace(ParamsJson)
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(ParamsJson) ?? new Dictionary<string, string>();

            return new RunbookStepDefinition
            {
                StepId = (StepId ?? string.Empty).Trim(),
                CheckId = (CheckId ?? string.Empty).Trim(),
                DisplayName = (DisplayName ?? string.Empty).Trim(),
                Category = string.IsNullOrWhiteSpace(Category) ? nameof(CheckCategory.SystemCheck) : Category.Trim(),
                TimeoutMs = TimeoutMs <= 0 ? 5000 : TimeoutMs,
                Enabled = Enabled,
                NextOnSuccess = (NextOnSuccess ?? string.Empty).Trim(),
                NextOnFailure = (NextOnFailure ?? string.Empty).Trim(),
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
                JsonConvert.DeserializeObject<Dictionary<string, string>>(ParamsJson);
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
}
