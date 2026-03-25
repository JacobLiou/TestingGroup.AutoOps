using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
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
        private LabelControl _filePathLabel;

        public event EventHandler RunbookSaved;

        public RunbookEditorForm(string runbookId = "default")
        {
            Width = 1400;
            Height = 860;
            MinimumSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterParent;
            Text = "RunBook Quick Editor";
            InitializeUi();
            LoadRunbook(runbookId);
        }

        private void InitializeUi()
        {
            SuspendLayout();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            Controls.Add(root);

            // =====================================================
            //  Row 0 - Metadata bar
            // =====================================================
            var metaBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 9,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4)
            };
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            metaBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            metaBar.Controls.Add(ML("Id:"), 0, 0);
            _runbookIdBox = new TextEdit { Dock = DockStyle.Fill };
            _runbookIdBox.Properties.ReadOnly = true;
            _runbookIdBox.Properties.Appearance.BackColor = Color.FromArgb(245, 245, 245);
            metaBar.Controls.Add(_runbookIdBox, 1, 0);
            metaBar.Controls.Add(ML("Title:"), 2, 0);
            _runbookTitleBox = new TextEdit { Dock = DockStyle.Fill };
            metaBar.Controls.Add(_runbookTitleBox, 3, 0);
            metaBar.Controls.Add(ML("Version:"), 4, 0);
            _runbookVersionBox = new TextEdit { Dock = DockStyle.Fill };
            metaBar.Controls.Add(_runbookVersionBox, 5, 0);
            metaBar.Controls.Add(ML("File:"), 7, 0);
            _filePathLabel = new LabelControl { Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None, AutoEllipsis = true };
            _filePathLabel.Appearance.ForeColor = Color.Gray;
            metaBar.Controls.Add(_filePathLabel, 8, 0);
            root.Controls.Add(metaBar, 0, 0);

            // =====================================================
            //  Row 1 - Action toolbar
            // =====================================================
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 2)
            };

            AddBtn(toolbar, "Load Default", Color.FromArgb(41, 128, 185), (s, e) => LoadRunbook("default"));
            AddSep(toolbar);
            AddBtn(toolbar, "Append", Color.FromArgb(46, 204, 113), (s, e) => AppendStep());
            AddBtn(toolbar, "Insert After", Color.FromArgb(22, 160, 133), (s, e) => InsertStepAfterCurrent());
            AddBtn(toolbar, "Remove", Color.FromArgb(231, 76, 60), (s, e) => RemoveSelectedStep());
            AddBtn(toolbar, "Move Up", Color.FromArgb(52, 73, 94), (s, e) => MoveSelected(-1));
            AddBtn(toolbar, "Move Down", Color.FromArgb(52, 73, 94), (s, e) => MoveSelected(1));
            AddSep(toolbar);
            AddBtn(toolbar, "Save", Color.FromArgb(230, 126, 34), (s, e) => SaveAs(_runbookIdBox.Text.Trim()));

            var saveAsLabel = new LabelControl
            {
                Text = "Save As:",
                AutoSizeMode = LabelAutoSizeMode.None,
                Width = 60,
                Height = 30,
                Margin = new Padding(12, 0, 2, 0),
                Anchor = AnchorStyles.Left,
                Appearance = { TextOptions = { VAlignment = DevExpress.Utils.VertAlignment.Center } }
            };
            toolbar.Controls.Add(saveAsLabel);
            _saveAsIdBox = new TextEdit { Width = 120, Height = 28, Margin = new Padding(0, 3, 0, 0) };
            _saveAsIdBox.Properties.NullValuePrompt = "Enter RunBook Id";
            toolbar.Controls.Add(_saveAsIdBox);
            AddBtn(toolbar, "Save Template", Color.FromArgb(142, 68, 173), (s, e) => SaveAs(_saveAsIdBox.Text.Trim()));

            root.Controls.Add(toolbar, 0, 1);

            // =====================================================
            //  Row 2 - Grid
            // =====================================================
            _gridControl = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_gridControl);
            _gridControl.MainView = _gridView;
            _gridControl.DataSource = _rows;

            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ShowIndicator = false;
            _gridView.OptionsBehavior.Editable = true;
            _gridView.OptionsView.RowAutoHeight = true;
            _gridView.OptionsView.ColumnAutoWidth = true;
            _gridView.OptionsSelection.EnableAppearanceFocusedRow = true;

            var colStepId = _gridView.Columns.AddVisible("StepId", "StepId");
            colStepId.Width = 70;
            colStepId.MinWidth = 50;

            var colCheckId = _gridView.Columns.AddVisible("CheckId", "CheckId");
            colCheckId.Width = 110;
            colCheckId.MinWidth = 70;
            var checkIdCombo = new RepositoryItemComboBox();
            foreach (var id in DiagnosticEngine.GetRegisteredCheckIds())
            {
                checkIdCombo.Items.Add(id);
            }
            colCheckId.ColumnEdit = checkIdCombo;

            var colName = _gridView.Columns.AddVisible("DisplayName", "DisplayName");
            colName.Width = 160;
            colName.MinWidth = 80;

            var colCat = _gridView.Columns.AddVisible("Category", "Category");
            colCat.Width = 120;
            colCat.MinWidth = 70;
            var catCombo = new RepositoryItemComboBox();
            foreach (var name in Enum.GetNames(typeof(CheckCategory)))
            {
                catCombo.Items.Add(name);
            }
            colCat.ColumnEdit = catCombo;

            var colEnabled = _gridView.Columns.AddVisible("Enabled", "Enabled");
            colEnabled.Width = 60;
            colEnabled.MinWidth = 40;
            var enabledCheckEdit = new RepositoryItemCheckEdit();
            enabledCheckEdit.ValueChecked = true;
            enabledCheckEdit.ValueUnchecked = false;
            colEnabled.ColumnEdit = enabledCheckEdit;

            var colTimeout = _gridView.Columns.AddVisible("TimeoutMs", "TimeoutMs");
            colTimeout.Width = 70;
            colTimeout.MinWidth = 50;

            var colParams = _gridView.Columns.AddVisible("ParamsJson", "Params (JSON)");
            colParams.Width = 280;
            colParams.MinWidth = 100;
            var paramsBtnEdit = new RepositoryItemButtonEdit();
            paramsBtnEdit.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            paramsBtnEdit.ButtonClick += ParamsButtonEdit_ButtonClick;
            colParams.ColumnEdit = paramsBtnEdit;

            _gridView.RowStyle += (s, e) =>
            {
                if (e.RowHandle < 0) return;
                var row = _gridView.GetRow(e.RowHandle) as RunbookStepRow;
                if (row == null) return;
                if (!row.Enabled)
                {
                    e.Appearance.ForeColor = Color.Silver;
                }
            };

            _gridView.CellValueChanged += (s, e) =>
            {
                if (e.Column.FieldName == "Enabled")
                {
                    _gridView.RefreshData();
                }
            };

            root.Controls.Add(_gridControl, 0, 2);

            // =====================================================
            //  Row 3 - Status bar
            // =====================================================
            var statusBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 248, 248) };
            _statusLabel = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Padding = new Padding(4, 4, 0, 0)
            };
            statusBar.Controls.Add(_statusLabel);
            root.Controls.Add(statusBar, 0, 3);

            ResumeLayout(true);
        }

        private void ParamsButtonEdit_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _rows.Count) return;
            var row = _rows[handle];

            using (var dlg = new XtraForm())
            {
                dlg.Text = "Edit Params JSON - " + row.StepId;
                dlg.Width = 600;
                dlg.Height = 480;
                dlg.MinimumSize = new Size(400, 300);
                dlg.StartPosition = FormStartPosition.CenterParent;

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    Padding = new Padding(10)
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
                dlg.Controls.Add(root);

                var hint = new LabelControl
                {
                    Text = "Format: { \"key1\": \"value1\", \"key2\": \"value2\" }",
                    Dock = DockStyle.Fill,
                    Appearance = { ForeColor = Color.Gray }
                };
                root.Controls.Add(hint, 0, 0);

                var editor = new MemoEdit
                {
                    Dock = DockStyle.Fill,
                    EditValue = FormatJson(row.ParamsJson)
                };
                editor.Properties.ScrollBars = ScrollBars.Both;
                editor.Properties.WordWrap = false;
                root.Controls.Add(editor, 0, 1);

                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(0, 4, 0, 0)
                };
                var cancelBtn = new SimpleButton { Text = "Cancel", Width = 80, Height = 30, DialogResult = DialogResult.Cancel };
                var okBtn = new SimpleButton
                {
                    Text = "OK",
                    Width = 80,
                    Height = 30,
                    Appearance = { BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, Options = { UseBackColor = true, UseForeColor = true } }
                };

                var errorLabel = new LabelControl
                {
                    AutoSizeMode = LabelAutoSizeMode.None,
                    Width = 360,
                    Height = 30,
                    Padding = new Padding(0, 8, 0, 0),
                    Appearance = { ForeColor = Color.FromArgb(231, 76, 60) }
                };

                okBtn.Click += (s2, e2) =>
                {
                    var text = (editor.EditValue ?? "").ToString().Trim();
                    if (string.IsNullOrEmpty(text)) text = "{}";
                    try
                    {
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
                        row.ParamsJson = text;
                        _gridView.RefreshData();
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    }
                    catch (Exception ex)
                    {
                        errorLabel.Text = "Invalid JSON: " + ex.Message;
                    }
                };

                btnPanel.Controls.AddRange(new Control[] { cancelBtn, okBtn, errorLabel });
                root.Controls.Add(btnPanel, 0, 2);

                dlg.AcceptButton = okBtn;
                dlg.CancelButton = cancelBtn;
                dlg.ShowDialog(this);
            }
        }

        private static string FormatJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "{}";
            try
            {
                var obj = JsonConvert.DeserializeObject(json);
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        private static LabelControl ML(string text)
        {
            return new LabelControl
            {
                Text = text,
                AutoSizeMode = LabelAutoSizeMode.None,
                Dock = DockStyle.Fill,
                Appearance =
                {
                    TextOptions =
                    {
                        HAlignment = DevExpress.Utils.HorzAlignment.Far,
                        VAlignment = DevExpress.Utils.VertAlignment.Center
                    }
                }
            };
        }

        private static void AddBtn(FlowLayoutPanel panel, string text, Color backColor, EventHandler onClick)
        {
            var btn = new SimpleButton
            {
                Text = text,
                Width = 100,
                Height = 30,
                Margin = new Padding(2, 2, 2, 2),
                Appearance =
                {
                    BackColor = backColor,
                    ForeColor = Color.White,
                    Options = { UseBackColor = true, UseForeColor = true }
                }
            };
            btn.Click += onClick;
            panel.Controls.Add(btn);
        }

        private static void AddSep(FlowLayoutPanel panel)
        {
            panel.Controls.Add(new Panel { Width = 8, Height = 1, Margin = new Padding(0) });
        }

        private void LoadRunbook(string runbookId)
        {
            try
            {
                var path = _runbookFileService.BuildRunbookPath(runbookId);
                var runbook = _runbookFileService.Load(runbookId);
                _runbookIdBox.Text = runbookId;
                _runbookTitleBox.Text = runbook.Title;
                _runbookVersionBox.Text = runbook.Version;
                _filePathLabel.Text = path;
                _rows.Clear();
                foreach (var step in runbook.Steps)
                {
                    _rows.Add(RunbookStepRow.FromDefinition(step));
                }
                SetStatus("Loaded RunBook: " + runbookId + " (" + runbook.Steps.Count + " steps)", Color.FromArgb(39, 174, 96));
            }
            catch (Exception ex)
            {
                SetStatus("Load failed: " + ex.Message, Color.FromArgb(231, 76, 60));
            }
        }

        private RunbookStepRow CreateNewStepRow()
        {
            var maxNum = _rows
                .Select(x => x.StepId)
                .Where(x => x.StartsWith("S", StringComparison.OrdinalIgnoreCase) && x.Length > 1 && int.TryParse(x.Substring(1), out _))
                .Select(x => int.Parse(x.Substring(1)))
                .DefaultIfEmpty(0)
                .Max();

            return new RunbookStepRow
            {
                StepId = "S" + (maxNum + 1).ToString("000"),
                CheckId = DiagnosticEngine.GetRegisteredCheckIds().FirstOrDefault() ?? "SYS_01",
                DisplayName = "New Step",
                Category = nameof(CheckCategory.SystemCheck),
                TimeoutMs = 5000,
                Enabled = true,
                ParamsJson = "{}"
            };
        }

        private void AppendStep()
        {
            var newRow = CreateNewStepRow();
            _rows.Add(newRow);
            _gridView.FocusedRowHandle = _rows.Count - 1;
            _gridView.MakeRowVisible(_rows.Count - 1, false);
            SetStatus("Appended step: " + newRow.StepId, Color.FromArgb(39, 174, 96));
        }

        private void InsertStepAfterCurrent()
        {
            var handle = _gridView.FocusedRowHandle;
            var insertIndex = (handle >= 0 && handle < _rows.Count) ? handle + 1 : _rows.Count;

            var newRow = CreateNewStepRow();
            _rows.Insert(insertIndex, newRow);
            _gridView.FocusedRowHandle = insertIndex;
            _gridView.MakeRowVisible(insertIndex, false);
            SetStatus("Inserted step at row " + (insertIndex + 1) + ": " + newRow.StepId, Color.FromArgb(22, 160, 133));
        }

        private void RemoveSelectedStep()
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _rows.Count) return;
            var stepId = _rows[handle].StepId;
            _rows.RemoveAt(handle);
            SetStatus("Removed step: " + stepId, Color.FromArgb(231, 76, 60));
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

        private void SaveAs(string runbookId)
        {
            if (string.IsNullOrWhiteSpace(runbookId))
            {
                SetStatus("Please enter a valid RunBook Id", Color.FromArgb(231, 76, 60));
                return;
            }

            CommitPendingEdits();

            try
            {
                foreach (var row in _rows)
                {
                    row.ValidateJson();
                    if (!row.IsParamsJsonValid)
                    {
                        SetStatus("ParamsJson error: " + row.StepId + " - " + row.ParamsJsonError, Color.FromArgb(231, 76, 60));
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
                _filePathLabel.Text = _runbookFileService.BuildRunbookPath(runbookId);

                var enabledCount = runbook.Steps.Count(st => st.Enabled);
                var disabledCount = runbook.Steps.Count - enabledCount;
                var msg = "Saved: " + runbookId + " (" + enabledCount + " enabled, " + disabledCount + " disabled)";
                SetStatus(msg, Color.FromArgb(39, 174, 96));
                RunbookSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message, Color.FromArgb(231, 76, 60));
            }
        }

        private void CommitPendingEdits()
        {
            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();
        }

        private void SetStatus(string message, Color color)
        {
            _statusLabel.Text = message;
            _statusLabel.Appearance.ForeColor = color;
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
                ParamsJson = JsonConvert.SerializeObject(def.Params, Formatting.Indented)
            };
        }

        public RunbookStepDefinition ToDefinition()
        {
            ValidateJson();
            if (!IsParamsJsonValid)
            {
                throw new InvalidOperationException("Step " + StepId + " has invalid ParamsJson: " + ParamsJsonError);
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
