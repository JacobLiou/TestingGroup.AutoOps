using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
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
    /// <summary>
    /// RunBook 编辑器窗体 — 提供 RunBook 步骤的可视化编辑、方法绑定选择和保存功能。
    /// </summary>
    public sealed class RunbookEditorForm : XtraForm
    {
        private readonly RunbookFileService _runbookFileService = new RunbookFileService();
        private readonly BindingList<RunbookStepRow> _rows = new BindingList<RunbookStepRow>();
        private readonly IReadOnlyList<CheckExecutorInfo> _executorInfos = DiagnosticEngine.GetRegisteredExecutorInfos();

        private GridControl _gridControl;
        private GridView _gridView;
        private TextEdit _runbookIdBox;
        private TextEdit _runbookTitleBox;
        private TextEdit _runbookVersionBox;
        private TextEdit _saveAsIdBox;
        private LabelControl _statusLabel;
        private LabelControl _filePathLabel;

        /// <summary>
        /// RunBook 保存成功后引发。
        /// </summary>
        public event EventHandler RunbookSaved;

        /// <summary>
        /// 按指定 RunBook 标识加载并初始化编辑器界面。
        /// </summary>
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

            var colCheckId = _gridView.Columns.AddVisible("CheckId", "CheckId");
            colCheckId.Width = 100;
            colCheckId.MinWidth = 70;

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

            var colBindDll = _gridView.Columns.AddVisible("BindDll", "BindDll");
            colBindDll.Width = 180;
            colBindDll.MinWidth = 100;

            var colBindMethod = _gridView.Columns.AddVisible("BindMethod", "BindMethod");
            colBindMethod.Width = 280;
            colBindMethod.MinWidth = 140;
            var bindMethodBtnEdit = new RepositoryItemButtonEdit();
            bindMethodBtnEdit.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            bindMethodBtnEdit.ButtonClick += BindMethodButtonEdit_ButtonClick;
            colBindMethod.ColumnEdit = bindMethodBtnEdit;

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
                dlg.Text = "Edit Params JSON - " + row.CheckId;
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

        private void BindMethodButtonEdit_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _rows.Count) return;
            var row = _rows[handle];

            using (var dlg = new XtraForm())
            {
                dlg.Text = "Select Bind Method - " + row.CheckId;
                dlg.Width = 1200;
                dlg.Height = 620;
                dlg.MinimumSize = new Size(900, 460);
                dlg.StartPosition = FormStartPosition.CenterParent;

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    Padding = new Padding(10)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
                dlg.Controls.Add(layout);

                var currentLabel = new LabelControl
                {
                    Text = "Current: " + (string.IsNullOrWhiteSpace(row.BindMethod) ? "(none)" : row.BindMethod),
                    Dock = DockStyle.Fill,
                    AutoSizeMode = LabelAutoSizeMode.None,
                    Appearance = { ForeColor = Color.FromArgb(100, 100, 100) }
                };
                layout.Controls.Add(currentLabel, 0, 0);

                var toolbarPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Padding = new Padding(0, 2, 0, 2)
                };

                var showAllCheck = new CheckEdit
                {
                    Text = "Show all methods from loaded DLLs",
                    Checked = false,
                    Width = 260,
                    Height = 28
                };
                toolbarPanel.Controls.Add(showAllCheck);

                var loadDllBtn = new SimpleButton
                {
                    Text = "Load External DLL...",
                    Width = 150,
                    Height = 28,
                    Margin = new Padding(8, 0, 0, 0),
                    Appearance = { BackColor = Color.FromArgb(52, 73, 94), ForeColor = Color.White, Options = { UseBackColor = true, UseForeColor = true } }
                };
                toolbarPanel.Controls.Add(loadDllBtn);

                var methodCountLabel = new LabelControl
                {
                    AutoSizeMode = LabelAutoSizeMode.None,
                    Width = 200,
                    Height = 28,
                    Margin = new Padding(12, 6, 0, 0),
                    Appearance = { ForeColor = Color.Gray }
                };
                toolbarPanel.Controls.Add(methodCountLabel);

                layout.Controls.Add(toolbarPanel, 0, 1);

                var pickerGrid = new GridControl { Dock = DockStyle.Fill };
                var pickerView = new GridView(pickerGrid);
                pickerGrid.MainView = pickerView;
                pickerGrid.DataSource = _executorInfos;
                methodCountLabel.Text = _executorInfos.Count + " registered executors";

                pickerView.OptionsView.ShowGroupPanel = false;
                pickerView.OptionsView.ShowIndicator = false;
                pickerView.OptionsView.ShowAutoFilterRow = true;
                pickerView.OptionsBehavior.Editable = false;
                pickerView.OptionsSelection.EnableAppearanceFocusedRow = true;

                pickerView.Columns.Clear();
                var pColDll = pickerView.Columns.AddVisible("BindDll", "DLL");
                pColDll.Width = 220;
                var pColMethod = pickerView.Columns.AddVisible("BindMethod", "Method");
                pColMethod.Width = 420;
                var pColDesc = pickerView.Columns.AddVisible("Description", "Signature / Description");
                pColDesc.Width = 320;

                IReadOnlyList<CheckExecutorInfo> allMethodsCache = null;

                showAllCheck.CheckedChanged += (s2, e2) =>
                {
                    if (showAllCheck.Checked)
                    {
                        if (allMethodsCache == null)
                            allMethodsCache = DiagnosticEngine.BrowseAllLoadedMethods();
                        pickerGrid.DataSource = allMethodsCache;
                        methodCountLabel.Text = allMethodsCache.Count + " methods (all loaded DLLs)";
                    }
                    else
                    {
                        pickerGrid.DataSource = _executorInfos;
                        methodCountLabel.Text = _executorInfos.Count + " registered executors";
                    }
                    pickerView.RefreshData();
                };

                loadDllBtn.Click += (s2, e2) =>
                {
                    using (var ofd = new OpenFileDialog())
                    {
                        ofd.Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*";
                        ofd.Title = "Select a .NET Assembly to scan";
                        if (ofd.ShowDialog(dlg) == DialogResult.OK)
                        {
                            try
                            {
                                var methods = DiagnosticEngine.LoadExternalDll(ofd.FileName);
                                if (methods.Count == 0)
                                {
                                    methodCountLabel.Text = "No callable methods found in DLL";
                                    return;
                                }
                                allMethodsCache = null;
                                showAllCheck.Checked = true;
                                allMethodsCache = DiagnosticEngine.BrowseAllLoadedMethods();
                                pickerGrid.DataSource = allMethodsCache;
                                methodCountLabel.Text = allMethodsCache.Count + " methods (incl. " + System.IO.Path.GetFileName(ofd.FileName) + ")";
                                pickerView.RefreshData();
                            }
                            catch (Exception ex2)
                            {
                                methodCountLabel.Text = "Load failed: " + ex2.Message;
                            }
                        }
                    }
                };

                layout.Controls.Add(pickerGrid, 0, 2);

                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(0, 6, 0, 0)
                };
                var cancelBtn = new SimpleButton { Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
                var okBtn = new SimpleButton
                {
                    Text = "Bind Selected",
                    Width = 120,
                    Height = 32,
                    Appearance = { BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, Options = { UseBackColor = true, UseForeColor = true } }
                };

                CheckExecutorInfo selectedInfo = null;

                okBtn.Click += (s2, e2) =>
                {
                    var focusedRow = pickerView.FocusedRowHandle;
                    if (focusedRow < 0) return;
                    selectedInfo = pickerView.GetRow(focusedRow) as CheckExecutorInfo;
                    if (selectedInfo != null)
                    {
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    }
                };

                pickerView.DoubleClick += (s2, e2) =>
                {
                    var pt = pickerGrid.PointToClient(Control.MousePosition);
                    var hitInfo = pickerView.CalcHitInfo(pt);
                    if (hitInfo.InRow)
                    {
                        selectedInfo = pickerView.GetRow(hitInfo.RowHandle) as CheckExecutorInfo;
                        if (selectedInfo != null)
                        {
                            dlg.DialogResult = DialogResult.OK;
                            dlg.Close();
                        }
                    }
                };

                btnPanel.Controls.AddRange(new Control[] { cancelBtn, okBtn });
                layout.Controls.Add(btnPanel, 0, 3);

                dlg.AcceptButton = okBtn;
                dlg.CancelButton = cancelBtn;

                if (dlg.ShowDialog(this) == DialogResult.OK && selectedInfo != null)
                {
                    row.BindDll = selectedInfo.BindDll;
                    row.BindMethod = selectedInfo.BindMethod;
                    _gridView.RefreshData();
                    SetStatus("Bound: " + selectedInfo.BindMethod, Color.FromArgb(39, 174, 96));
                }
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
            var firstInfo = _executorInfos.FirstOrDefault();
            return new RunbookStepRow
            {
                CheckId = firstInfo?.CheckId ?? "SYS_01",
                DisplayName = firstInfo?.DisplayName ?? "New Step",
                Category = firstInfo?.DefaultCategory ?? nameof(CheckCategory.SystemCheck),
                BindDll = firstInfo?.BindDll ?? string.Empty,
                BindMethod = firstInfo?.BindMethod ?? string.Empty,
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
            SetStatus("Appended step: " + newRow.CheckId, Color.FromArgb(39, 174, 96));
        }

        private void InsertStepAfterCurrent()
        {
            var handle = _gridView.FocusedRowHandle;
            var insertIndex = (handle >= 0 && handle < _rows.Count) ? handle + 1 : _rows.Count;

            var newRow = CreateNewStepRow();
            _rows.Insert(insertIndex, newRow);
            _gridView.FocusedRowHandle = insertIndex;
            _gridView.MakeRowVisible(insertIndex, false);
            SetStatus("Inserted step at row " + (insertIndex + 1) + ": " + newRow.CheckId, Color.FromArgb(22, 160, 133));
        }

        private void RemoveSelectedStep()
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _rows.Count) return;
            var checkId = _rows[handle].CheckId;
            _rows.RemoveAt(handle);
            SetStatus("Removed step: " + checkId, Color.FromArgb(231, 76, 60));
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
                        SetStatus("ParamsJson error: " + row.CheckId + " - " + row.ParamsJsonError, Color.FromArgb(231, 76, 60));
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

    /// <summary>
    /// RunBook 编辑器网格中的一行，对应一条步骤定义的可编辑视图。
    /// </summary>
    public sealed class RunbookStepRow
    {
        public string CheckId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = nameof(CheckCategory.SystemCheck);
        public string BindDll { get; set; } = string.Empty;
        public string BindMethod { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 5000;
        public bool Enabled { get; set; } = true;
        public string ParamsJson { get; set; } = "{}";
        public bool IsParamsJsonValid { get; private set; } = true;
        public string ParamsJsonError { get; private set; } = string.Empty;

        /// <summary>
        /// 从 <see cref="RunbookStepDefinition"/> 创建行模型。
        /// </summary>
        public static RunbookStepRow FromDefinition(RunbookStepDefinition def)
        {
            return new RunbookStepRow
            {
                CheckId = def.CheckId,
                DisplayName = def.DisplayName,
                Category = string.IsNullOrWhiteSpace(def.Category) ? nameof(CheckCategory.SystemCheck) : def.Category,
                BindDll = def.BindDll ?? string.Empty,
                BindMethod = def.BindMethod ?? string.Empty,
                TimeoutMs = def.TimeoutMs,
                Enabled = def.Enabled,
                ParamsJson = JsonConvert.SerializeObject(def.Params, Formatting.Indented)
            };
        }

        /// <summary>
        /// 将当前行转换为 RunBook 步骤定义（会先校验 ParamsJson）。
        /// </summary>
        public RunbookStepDefinition ToDefinition()
        {
            ValidateJson();
            if (!IsParamsJsonValid)
            {
                throw new InvalidOperationException("Step " + CheckId + " has invalid ParamsJson: " + ParamsJsonError);
            }

            var paramMap = string.IsNullOrWhiteSpace(ParamsJson)
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(ParamsJson) ?? new Dictionary<string, string>();

            return new RunbookStepDefinition
            {
                CheckId = (CheckId ?? string.Empty).Trim(),
                DisplayName = (DisplayName ?? string.Empty).Trim(),
                Category = string.IsNullOrWhiteSpace(Category) ? nameof(CheckCategory.SystemCheck) : Category.Trim(),
                BindDll = (BindDll ?? string.Empty).Trim(),
                BindMethod = (BindMethod ?? string.Empty).Trim(),
                TimeoutMs = TimeoutMs <= 0 ? 5000 : TimeoutMs,
                Enabled = Enabled,
                Params = paramMap
            };
        }

        /// <summary>
        /// 校验 <see cref="ParamsJson"/> 是否为合法 JSON。
        /// </summary>
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
