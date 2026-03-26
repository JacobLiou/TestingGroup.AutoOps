using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services;
using SelfDiagnostic.Services.Abstractions;
using SelfDiagnostic.UI.Controls;

namespace SelfDiagnostic.UI
{
    /// <summary>
    /// 诊断主控件 — 嵌入 MIMS 宿主窗体的核心 UserControl，包含 Grid 表格、扫描控制、右键调试菜单与评分环。
    /// </summary>
    public sealed class DiagnosticMainControl : XtraUserControl
    {
        private const string DefaultStationId = "STATION-001";
        private const string DefaultLineId = "LINE-001";

        private readonly IExternalSystemClient _externalSystemClient;
        private readonly MimsConfigXmlParser _mimsConfigXmlParser = new MimsConfigXmlParser();
        private readonly MimsStationCapabilityParser _mimsStationCapabilityParser = new MimsStationCapabilityParser();
        private readonly MimsPowerSupplyParser _mimsPowerSupplyParser = new MimsPowerSupplyParser();
        private readonly TpConnectivityInspector _tpConnectivityInspector = new TpConnectivityInspector();

        private readonly BindingList<DiagnosticItem> _diagnosticItems = new BindingList<DiagnosticItem>();
        private List<RunbookStepDefinition> _enabledSteps = new List<RunbookStepDefinition>();
        private readonly object _scanLock = new object();

        private const string DefaultRunbookFileId = "default";

        private CancellationTokenSource _cts;
        private RunbookDefinition _activeRunbook;
        private string _activeRunbookFileId = DefaultRunbookFileId;
        private int _passCount;
        private int _warningCount;
        private int _failCount;

        private LabelControl _runbookLabel;
        private LabelControl _statusLabel;
        private LabelControl _externalConfigLabel;
        private LabelControl _summaryLabel;
        private LabelControl _scanProgressLabel;
        private LabelControl _currentScanLabel;
        private CheckEdit _autoScrollCheck;
        private SimpleButton _startButton;
        private SimpleButton _stopButton;
        private SimpleButton _editorButton;
        private GridControl _gridControl;
        private GridView _gridView;
        private ScoreRingControl _scoreRing;
        private ContextMenuStrip _gridContextMenu;

        /// <summary>
        /// 使用默认的外部系统客户端（MIMS gRPC）创建控件。
        /// </summary>
        public DiagnosticMainControl() : this(null) { }

        /// <summary>
        /// 使用指定的外部系统客户端创建控件。
        /// </summary>
        public DiagnosticMainControl(IExternalSystemClient externalSystemClient)
        {
            _externalSystemClient = externalSystemClient
                ?? new MimsGrpcClient(new MimsXmlBuilder());
            InitializeUi();
            ResetState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts?.Cancel();
            }
            base.Dispose(disposing);
        }

        private void InitializeUi()
        {
            SuspendLayout();
            Padding = new Padding(8);

            // =====================================================
            //  Header area: TableLayoutPanel for predictable layout
            // =====================================================
            var headerTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 140,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = false
            };
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145F));
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            headerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            headerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            headerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _scoreRing = new ScoreRingControl { Size = new Size(130, 130), Margin = new Padding(4) };
            _scoreRing.Subtitle = "Overall Score";
            headerTable.Controls.Add(_scoreRing, 0, 0);
            headerTable.SetRowSpan(_scoreRing, 4);

            // Row 0: RunbookLabel
            var row0 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _runbookLabel = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None, Width = 260, Height = 22 };
            _runbookLabel.Appearance.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
            row0.Controls.Add(_runbookLabel);
            headerTable.Controls.Add(row0, 1, 0);

            // Row 1: Action buttons + auto-scroll
            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 2, 0, 2)
            };
            _startButton = CreateButton("Start Scan", Color.FromArgb(41, 128, 185));
            _startButton.Click += async (s, e) => await StartScanAsync();
            _stopButton = CreateButton("Stop", Color.FromArgb(192, 57, 43));
            _stopButton.Click += (s, e) => StopScan();
            _editorButton = CreateButton("RunBook Editor", Color.FromArgb(142, 68, 173));
            _editorButton.Click += (s, e) => OpenEditor();
            _autoScrollCheck = new CheckEdit { Text = "Auto-follow current item", Checked = true, Width = 200, Height = 34 };
            btnRow.Controls.AddRange(new Control[] { _startButton, _stopButton, _editorButton, _autoScrollCheck });
            headerTable.Controls.Add(btnRow, 1, 1);
            SetButtonsEnabled(false);

            // Row 2: Status label
            _statusLabel = new LabelControl { Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            headerTable.Controls.Add(_statusLabel, 1, 2);

            // Row 3: External config label
            _externalConfigLabel = new LabelControl { Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            _externalConfigLabel.Appearance.ForeColor = Color.DimGray;
            headerTable.Controls.Add(_externalConfigLabel, 1, 3);

            // =====================================================
            //  Scan bar (Dock: Top, fixed height 30)
            // =====================================================
            var scanBar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 28,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(4, 4, 4, 0)
            };
            scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280F));
            scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _currentScanLabel = new LabelControl { Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            _currentScanLabel.Appearance.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
            _scanProgressLabel = new LabelControl { Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            _summaryLabel = new LabelControl { Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };

            scanBar.Controls.Add(_currentScanLabel, 0, 0);
            scanBar.Controls.Add(_scanProgressLabel, 1, 0);
            scanBar.Controls.Add(_summaryLabel, 2, 0);

            // =====================================================
            //  Grid (Dock: Fill)
            // =====================================================
            _gridControl = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_gridControl);
            _gridControl.MainView = _gridView;
            _gridControl.DataSource = _diagnosticItems;

            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ShowIndicator = false;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsSelection.EnableAppearanceFocusedRow = true;
            _gridView.OptionsView.RowAutoHeight = true;
            _gridView.OptionsView.ColumnAutoWidth = true;

            var colId = _gridView.Columns.AddVisible("Id", "CheckId");
            colId.Width = 70;
            colId.MinWidth = 50;

            var colName = _gridView.Columns.AddVisible("Name", "Name");
            colName.Width = 180;
            colName.MinWidth = 100;

            var colCat = _gridView.Columns.AddVisible("CategoryName", "Category");
            colCat.Width = 100;
            colCat.MinWidth = 70;

            var colStatus = _gridView.Columns.AddVisible("StatusText", "Status");
            colStatus.Width = 80;
            colStatus.MinWidth = 50;

            var colDetail = _gridView.Columns.AddVisible("Detail", "Detail");
            colDetail.Width = 350;
            colDetail.MinWidth = 150;

            var colFix = _gridView.Columns.AddVisible("FixSuggestion", "FixSuggestion");
            colFix.Width = 200;
            colFix.MinWidth = 100;

            var colScore = _gridView.Columns.AddVisible("Score", "Score");
            colScore.Width = 60;
            colScore.MinWidth = 40;

            _gridView.RowStyle += GridView_RowStyle;

            // =====================================================
            //  Grid context menu (right-click debug actions)
            // =====================================================
            _gridContextMenu = new ContextMenuStrip();
            _gridContextMenu.Items.Add("Run This Step", null, async (s, ev) => await DebugRunSingleStepAsync());
            _gridContextMenu.Items.Add("Run From Here", null, async (s, ev) => await DebugRunFromHereAsync());
            _gridContextMenu.Items.Add(new ToolStripSeparator());
            _gridContextMenu.Items.Add("Reset This Step", null, (s, ev) => ResetSingleStep());
            _gridContextMenu.Items.Add("Reset All Steps", null, (s, ev) => ResetAllSteps());
            _gridContextMenu.Opening += GridContextMenu_Opening;
            _gridControl.ContextMenuStrip = _gridContextMenu;

            // =====================================================
            //  Assemble: add Fill first, then Top panels last
            // =====================================================
            Controls.Add(_gridControl);
            Controls.Add(scanBar);
            Controls.Add(headerTable);

            ResumeLayout(true);
        }

        // ──────────────────────────────────────────────────────────────
        //  Context menu
        // ──────────────────────────────────────────────────────────────

        private void GridContextMenu_Opening(object sender, CancelEventArgs e)
        {
            var handle = _gridView.FocusedRowHandle;
            bool hasRow = handle >= 0 && handle < _diagnosticItems.Count;
            bool canRun = hasRow && !_isScanning;

            _gridContextMenu.Items[0].Enabled = canRun;
            _gridContextMenu.Items[1].Enabled = canRun;
            _gridContextMenu.Items[3].Enabled = hasRow && !_isScanning;
            _gridContextMenu.Items[4].Enabled = !_isScanning;

            if (!hasRow) e.Cancel = true;
        }

        // ──────────────────────────────────────────────────────────────
        //  Row styling
        // ──────────────────────────────────────────────────────────────

        private void GridView_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
        {
            if (e.RowHandle < 0) return;
            var item = _gridView.GetRow(e.RowHandle) as DiagnosticItem;
            if (item == null) return;

            switch (item.Status)
            {
                case CheckStatus.Fail:
                    e.Appearance.BackColor = Color.FromArgb(255, 237, 237);
                    break;
                case CheckStatus.Warning:
                    e.Appearance.BackColor = Color.FromArgb(255, 248, 229);
                    break;
                case CheckStatus.Pass:
                case CheckStatus.Fixed:
                    e.Appearance.BackColor = Color.FromArgb(236, 255, 236);
                    break;
                case CheckStatus.Scanning:
                    e.Appearance.BackColor = Color.FromArgb(233, 245, 255);
                    break;
            }
        }

        private static SimpleButton CreateButton(string text, Color backColor)
        {
            var btn = new SimpleButton
            {
                Text = text,
                Width = 100,
                Height = 34,
                Appearance =
                {
                    BackColor = backColor,
                    ForeColor = Color.White,
                    Options = { UseBackColor = true, UseForeColor = true }
                }
            };
            return btn;
        }

        // ──────────────────────────────────────────────────────────────
        //  State management
        // ──────────────────────────────────────────────────────────────

        private void ResetState()
        {
            _activeRunbook = DiagnosticEngine.LoadRunbook();
            _enabledSteps = _activeRunbook.Steps.Where(s => s.Enabled).ToList();

            _diagnosticItems.Clear();
            foreach (var item in DiagnosticEngine.BuildCheckList(_activeRunbook))
            {
                _diagnosticItems.Add(item);
            }

            _passCount = 0;
            _warningCount = 0;
            _failCount = 0;
            _scoreRing.Score = 100;
            _runbookLabel.Text = "RunBook: " + _activeRunbook.Id;
            _statusLabel.Text = string.Format("Click \"Start Scan\" to run RunBook: {0}", _activeRunbook.Id);
            _externalConfigLabel.Text = "Config unavailable: MIMS external dependency config not fetched";
            _currentScanLabel.Text = "Ready";
            _summaryLabel.Text = string.Format("Pass {0} | Warning {1} | Fail {2}",
                _passCount, _warningCount, _failCount);
            _scanProgressLabel.Text = string.Format("Scanned {0} / {1}",
                0, _diagnosticItems.Count);
            _gridView.RefreshData();
        }

        private void ResetItemState(DiagnosticItem item)
        {
            item.Status = CheckStatus.Pending;
            item.Detail = string.Empty;
            item.FixSuggestion = string.Empty;
            item.Score = 100;
        }

        private void ResetSingleStep()
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _diagnosticItems.Count) return;

            ResetItemState(_diagnosticItems[handle]);
            _gridView.RefreshData();
            RefreshCountersAndScore();
            _statusLabel.Text = string.Format("Reset step: {0}", _diagnosticItems[handle].Id);
        }

        private void ResetAllSteps()
        {
            foreach (var item in _diagnosticItems)
            {
                ResetItemState(item);
            }

            _gridView.RefreshData();
            RefreshCountersAndScore();
            _currentScanLabel.Text = "Ready";
            _statusLabel.Text = "All steps reset";
        }

        // ──────────────────────────────────────────────────────────────
        //  Execution — shared core
        // ──────────────────────────────────────────────────────────────

        private bool _isScanning;

        /// <summary>
        /// Acquires the scan lock and runs steps [startIndex .. startIndex+count).
        /// If resetTargetItems is true, resets only the items in that range before running.
        /// </summary>
        private async Task RunStepsAsync(int startIndex, int count, bool resetTargetItems)
        {
            lock (_scanLock)
            {
                if (_isScanning) return;
                _isScanning = true;
                _cts = new CancellationTokenSource();
            }

            SetButtonsEnabled(true);
            var token = _cts.Token;
            var endIndex = Math.Min(startIndex + count, _enabledSteps.Count);

            if (resetTargetItems)
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    ResetItemState(_diagnosticItems[i]);
                }
                _gridView.RefreshData();
            }

            try
            {
                _statusLabel.Text = "Fetching MIMS environment config...";
                var runContext = await BuildRunContextAsync(token);
                if (runContext.ExternalChecksEnabled)
                {
                    _externalConfigLabel.Text = string.Format("Config source: {0}", runContext.ConfigSource);
                }
                else
                {
                    _externalConfigLabel.Text = string.Format("Config unavailable: {0}", runContext.ConfigError);
                }

                var label = count == 1 ? "Debug step" : (startIndex == 0 && endIndex == _enabledSteps.Count ? "Scanning" : "Debug from step " + (startIndex + 1));
                _statusLabel.Text = label + "...";

                for (int i = startIndex; i < endIndex; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var currentStep = _enabledSteps[i];
                    var item = _diagnosticItems[i];

                    _currentScanLabel.Text = item.CategoryIcon + " " + item.Name;
                    SelectCurrentRow(item);

                    await DiagnosticEngine.RunCheckAsync(item, currentStep, runContext, token);
                    _gridView.RefreshData();
                    RefreshCountersAndScore();
                }

                _currentScanLabel.Text = count == 1 ? "Step complete" : "Scan complete";
                _statusLabel.Text = string.Format("Finished! Pass {0} | Warning {1} | Fail {2}",
                    _passCount, _warningCount, _failCount);
            }
            catch (OperationCanceledException)
            {
                _statusLabel.Text = "Scan cancelled";
                _currentScanLabel.Text = "Cancelled";
            }
            finally
            {
                lock (_scanLock)
                {
                    _isScanning = false;
                    _cts?.Dispose();
                    _cts = null;
                }
                SetButtonsEnabled(false);
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Public execution entry points
        // ──────────────────────────────────────────────────────────────

        private async Task StartScanAsync()
        {
            ResetState();
            await RunStepsAsync(0, _enabledSteps.Count, false);
        }

        private async Task DebugRunSingleStepAsync()
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _enabledSteps.Count) return;
            await RunStepsAsync(handle, 1, true);
        }

        private async Task DebugRunFromHereAsync()
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0 || handle >= _enabledSteps.Count) return;
            await RunStepsAsync(handle, _enabledSteps.Count - handle, true);
        }

        private void StopScan()
        {
            if (_cts == null || _cts.IsCancellationRequested) return;

            var result = XtraMessageBox.Show(
                "Are you sure you want to stop the current scan?",
                "Stop Scan",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            _cts.Cancel();
            _statusLabel.Text = "Stopping...";

            var resetResult = XtraMessageBox.Show(
                "Do you want to reset the diagnostic table?\nChoose \"Yes\" to clear results and restore initial state.",
                "Reset Table",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (resetResult == DialogResult.Yes)
            {
                ResetState();
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Context helpers
        // ──────────────────────────────────────────────────────────────

        private async Task<DiagnosticRunContext> BuildRunContextAsync(CancellationToken cancellationToken)
        {
            var configRequest = new MimsEnvironmentConfigRequest { StationId = DefaultStationId, LineId = DefaultLineId };
            var configResult = await _externalSystemClient.GetEnvironmentConfigAsync(configRequest, cancellationToken);
            var tpSnapshot = await _tpConnectivityInspector.InspectAsync(cancellationToken);
            if (!configResult.Success)
            {
                return new DiagnosticRunContext
                {
                    ExternalChecksEnabled = false,
                    ConfigError = configResult.Code + " - " + configResult.Message,
                    TpConnectivity = tpSnapshot
                };
            }

            var parsed = _mimsConfigXmlParser.ParseOrDefault(configResult.ConfigXml);
            var stationRequirements = _mimsStationCapabilityParser.ParseOrDefault(configResult.ConfigXml);
            var powerRequirements = _mimsPowerSupplyParser.ParseOrDefault(configResult.ConfigXml);
            return new DiagnosticRunContext
            {
                ExternalChecksEnabled = true,
                ExternalConfig = parsed,
                ConfigSource = "MIMS(" + configResult.Endpoint + ")",
                TpConnectivity = tpSnapshot,
                StationCapabilityRequirements = stationRequirements,
                PowerSupplyRequirements = powerRequirements,
                RawMimsConfigXml = configResult.ConfigXml
            };
        }

        private void RefreshCountersAndScore()
        {
            _passCount = _diagnosticItems.Count(i => i.Status == CheckStatus.Pass || i.Status == CheckStatus.Fixed);
            _warningCount = _diagnosticItems.Count(i => i.Status == CheckStatus.Warning);
            _failCount = _diagnosticItems.Count(i => i.Status == CheckStatus.Fail);
            var scored = _diagnosticItems
                .Where(i => i.Status != CheckStatus.Pending && i.Status != CheckStatus.Scanning)
                .ToList();
            var score = scored.Count == 0 ? 100 : scored.Sum(i => i.Score) / scored.Count;
            _scoreRing.Score = score;
            _summaryLabel.Text = string.Format("Pass {0} | Warning {1} | Fail {2}",
                _passCount, _warningCount, _failCount);
            _scanProgressLabel.Text = string.Format("Scanned {0} / {1}",
                scored.Count, _diagnosticItems.Count);
        }

        private void SetButtonsEnabled(bool isScanning)
        {
            _startButton.Enabled = !isScanning;
            _stopButton.Enabled = isScanning;
            _editorButton.Enabled = !isScanning;
        }

        private void SelectCurrentRow(DiagnosticItem item)
        {
            var index = _diagnosticItems.IndexOf(item);
            if (index < 0) return;

            _gridView.FocusedRowHandle = index;
            if (_autoScrollCheck.Checked)
            {
                _gridView.MakeRowVisible(index, false);
            }
        }

        private void OpenEditor()
        {
            using (var editor = new RunbookEditorForm(_activeRunbookFileId))
            {
                editor.RunbookSaved += (s, e) => ResetState();
                editor.ShowDialog(FindForm());
            }
        }
    }
}
