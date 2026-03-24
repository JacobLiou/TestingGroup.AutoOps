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
    /// Main diagnostic UserControl, designed to be embedded into the MIMS WinForm host.
    /// Usage: var ctrl = new DiagnosticMainControl(); hostPanel.Controls.Add(ctrl); ctrl.Dock = DockStyle.Fill;
    /// </summary>
    public sealed class DiagnosticMainControl : XtraUserControl
    {
        private const string DefaultMimsAuthor = "YUD";
        private const string DefaultMimsSpec = "GUI";
        private const string DefaultMimsPartNumber = "TEST-001";
        private const string DefaultStationId = "STATION-001";
        private const string DefaultLineId = "LINE-001";

        private readonly IExternalSystemClient _externalSystemClient;
        private readonly MimsConfigXmlParser _mimsConfigXmlParser = new MimsConfigXmlParser();
        private readonly MimsStationCapabilityParser _mimsStationCapabilityParser = new MimsStationCapabilityParser();
        private readonly MimsPowerSupplyParser _mimsPowerSupplyParser = new MimsPowerSupplyParser();
        private readonly TpConnectivityInspector _tpConnectivityInspector = new TpConnectivityInspector();

        private readonly BindingList<DiagnosticItem> _diagnosticItems = new BindingList<DiagnosticItem>();
        private readonly Dictionary<string, RunbookStepDefinition> _runbookStepsByStepId =
            new Dictionary<string, RunbookStepDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly object _scanLock = new object();

        private CancellationTokenSource _cts;
        private RunbookDefinition _activeRunbook;
        private int _passCount;
        private int _warningCount;
        private int _failCount;

        private LabelControl _runbookLabel;
        private LabelControl _statusLabel;
        private LabelControl _externalConfigLabel;
        private LabelControl _summaryLabel;
        private LabelControl _scanProgressLabel;
        private LabelControl _currentScanLabel;
        private ComboBoxEdit _languageCombo;
        private CheckEdit _autoScrollCheck;
        private SimpleButton _startButton;
        private SimpleButton _stopButton;
        private SimpleButton _fixAllButton;
        private SimpleButton _reportButton;
        private SimpleButton _editorButton;
        private GridControl _gridControl;
        private GridView _gridView;
        private ScoreRingControl _scoreRing;

        public DiagnosticMainControl() : this(null) { }

        public DiagnosticMainControl(IExternalSystemClient externalSystemClient)
        {
            _externalSystemClient = externalSystemClient
                ?? new MimsGrpcClient(new MimsXmlBuilder());
            InitializeUi();
            ApplyLanguage();
            ResetState();
            LanguageService.Instance.LanguageChanged += OnLanguageChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts?.Cancel();
                LanguageService.Instance.LanguageChanged -= OnLanguageChanged;
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
            headerTable.Controls.Add(_scoreRing, 0, 0);
            headerTable.SetRowSpan(_scoreRing, 4);

            // Row 0: RunbookLabel + Language combo
            var row0 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _runbookLabel = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None, Width = 160, Height = 22 };
            _runbookLabel.Appearance.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
            var langLabel = new LabelControl { Text = "语言", AutoSizeMode = LabelAutoSizeMode.None, Width = 36, Height = 22, Padding = new Padding(8, 4, 2, 0) };
            _languageCombo = new ComboBoxEdit { Width = 90 };
            _languageCombo.Properties.Items.AddRange(new[] { "中文", "English" });
            _languageCombo.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _languageCombo.SelectedIndexChanged += (s, e) =>
            {
                var code = _languageCombo.SelectedIndex == 1 ? LanguageService.EnUs : LanguageService.ZhCn;
                LanguageService.Instance.SetLanguage(code);
            };
            row0.Controls.AddRange(new Control[] { _runbookLabel, langLabel, _languageCombo });
            headerTable.Controls.Add(row0, 1, 0);

            // Row 1: Action buttons + auto-scroll (fixed height, no AutoSize)
            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 2, 0, 2)
            };
            _startButton = CreateButton("开始诊断", Color.FromArgb(41, 128, 185));
            _startButton.Click += async (s, e) => await StartScanAsync();
            _stopButton = CreateButton("停止", Color.FromArgb(149, 165, 166));
            _stopButton.Click += (s, e) => StopScan();
            _fixAllButton = CreateButton("一键修复", Color.FromArgb(46, 204, 113));
            _fixAllButton.Click += async (s, e) => await FixAllAsync();
            _reportButton = CreateButton("上报 MIMS", Color.FromArgb(230, 126, 34));
            _reportButton.Click += async (s, e) => await SendToMimsCoreAsync("manual", CancellationToken.None);
            _editorButton = CreateButton("RunBook 编辑器", Color.FromArgb(142, 68, 173));
            _editorButton.Click += (s, e) => OpenEditor();
            _autoScrollCheck = new CheckEdit { Checked = true, Width = 140, Height = 34 };
            btnRow.Controls.AddRange(new Control[] { _startButton, _stopButton, _fixAllButton, _reportButton, _editorButton, _autoScrollCheck });
            headerTable.Controls.Add(btnRow, 1, 1);

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
            //  Assemble: add Fill first, then Top panels last
            //  (WinForms docks last-added Top controls topmost)
            // =====================================================
            Controls.Add(_gridControl);
            Controls.Add(scanBar);
            Controls.Add(headerTable);

            ResumeLayout(true);
        }

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

        private void ApplyLanguage()
        {
            _startButton.Text = T("Loc.App.Start", "开始诊断");
            _stopButton.Text = T("Loc.App.Stop", "停止");
            _fixAllButton.Text = T("Loc.App.FixAll", "一键修复");
            _reportButton.Text = T("Loc.App.Report", "上报 MIMS");
            _editorButton.Text = T("Loc.App.Editor", "RunBook 编辑器");
            _autoScrollCheck.Text = T("Loc.App.AutoScroll", "自动跟随当前项");

            var runbookId = _activeRunbook?.Id ?? "default";
            _runbookLabel.Text = T("Loc.App.Runbook", "RunBook") + ": " + runbookId;

            var scannedCount = _diagnosticItems.Count(i =>
                i.Status != CheckStatus.Pending && i.Status != CheckStatus.Scanning);
            _scanProgressLabel.Text = TF("Loc.Main.ScannedProgress", "已扫描 {0} / {1} 项",
                scannedCount, _diagnosticItems.Count);

            _languageCombo.SelectedIndex =
                LanguageService.Instance.CurrentLanguage == LanguageService.EnUs ? 1 : 0;

            _gridView.RefreshData();
        }

        private void OnLanguageChanged(string lang)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnLanguageChanged(lang)));
                return;
            }

            ApplyLanguage();
            foreach (var item in _diagnosticItems)
            {
                item.RefreshLocalizedText();
            }
        }

        private void ResetState()
        {
            _activeRunbook = DiagnosticEngine.LoadRunbook();
            _runbookStepsByStepId.Clear();
            foreach (var step in _activeRunbook.Steps.Where(s => s.Enabled))
            {
                _runbookStepsByStepId[step.StepId] = step;
            }

            _diagnosticItems.Clear();
            foreach (var item in DiagnosticEngine.BuildCheckList(_activeRunbook))
            {
                _diagnosticItems.Add(item);
            }

            _passCount = 0;
            _warningCount = 0;
            _failCount = 0;
            _scoreRing.Score = 100;
            _runbookLabel.Text = T("Loc.App.Runbook", "RunBook") + ": " + _activeRunbook.Id;
            _statusLabel.Text = TF("Loc.Runtime.ClickRunbook", "点击\"开始诊断\"执行 RunBook：{0}", _activeRunbook.Id);
            _externalConfigLabel.Text = TF("Loc.Runtime.ConfigUnavailable", "外部配置不可用: {0}", "未获取 MIMS 外部依赖配置");
            _currentScanLabel.Text = T("Loc.Runtime.Ready", "就绪");
            _summaryLabel.Text = TF("Loc.App.Summary", "通过 {0} | 风险 {1} | 异常 {2}",
                _passCount, _warningCount, _failCount);
            _scanProgressLabel.Text = TF("Loc.Main.ScannedProgress", "已扫描 {0} / {1} 项",
                0, _diagnosticItems.Count);
            _gridView.RefreshData();
        }

        private async Task StartScanAsync()
        {
            lock (_scanLock)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                    return;
                _cts = new CancellationTokenSource();
            }

            SetButtonsEnabled(true);
            ResetState();
            var token = _cts.Token;
            _statusLabel.Text = T("Loc.Runtime.Scanning", "正在扫描...");

            try
            {
                _statusLabel.Text = T("Loc.Runtime.FetchMimsConfig", "正在向 MIMS 获取外部系统配置...");
                var runContext = await BuildRunContextAsync(token);
                if (runContext.ExternalChecksEnabled)
                {
                    _externalConfigLabel.Text = TF("Loc.Runtime.ConfigSource", "外部配置来源: {0}", runContext.ConfigSource);
                }
                else
                {
                    _externalConfigLabel.Text = TF("Loc.Runtime.ConfigUnavailable", "外部配置不可用: {0}", runContext.ConfigError);
                    _statusLabel.Text = T("Loc.Runtime.ConfigFailed", "MIMS 配置获取失败，外部依赖项将标记为跳过");
                }

                var currentStep = _activeRunbook?.Steps.FirstOrDefault(s => s.Enabled);
                var stepGuard = 0;
                var maxStepGuard = Math.Max(1, _runbookStepsByStepId.Count * 4);
                while (currentStep != null && stepGuard < maxStepGuard && !token.IsCancellationRequested)
                {
                    var item = _diagnosticItems.FirstOrDefault(i =>
                        i.Id.Equals(currentStep.CheckId, StringComparison.OrdinalIgnoreCase));
                    if (item == null) break;

                    _currentScanLabel.Text = item.CategoryIcon + " " + item.Name;
                    SelectCurrentRow(item);

                    var outcome = await DiagnosticEngine.RunCheckAsync(item, currentStep, runContext, token);
                    _gridView.RefreshData();
                    RefreshCountersAndScore();

                    var nextStepId = outcome.Success ? currentStep.NextOnSuccess : currentStep.NextOnFailure;
                    if (string.IsNullOrWhiteSpace(nextStepId) ||
                        !_runbookStepsByStepId.TryGetValue(nextStepId, out var next))
                    {
                        currentStep = null;
                    }
                    else
                    {
                        currentStep = next;
                    }
                    stepGuard++;
                }

                _currentScanLabel.Text = T("Loc.Runtime.ScanComplete", "扫描完成");
                _statusLabel.Text = TF("Loc.Runtime.Summary",
                    "体检完成！通过 {0} 项 | 风险 {1} 项 | 异常 {2} 项",
                    _passCount, _warningCount, _failCount);
                await SendToMimsCoreAsync("auto", CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _statusLabel.Text = T("Loc.Runtime.ScanCancelled", "扫描已取消");
                _currentScanLabel.Text = T("Loc.Runtime.Cancelled", "已取消");
            }
            finally
            {
                SetButtonsEnabled(false);
            }
        }

        private void StopScan()
        {
            _cts?.Cancel();
        }

        private async Task FixAllAsync()
        {
            foreach (var item in _diagnosticItems)
            {
                if (item.Status == CheckStatus.Warning || item.Status == CheckStatus.Fail)
                {
                    item.Status = CheckStatus.Scanning;
                    _gridView.RefreshData();
                    await Task.Delay(130);
                    item.Status = CheckStatus.Fixed;
                    item.Score = 100;
                    item.Detail += " [已修复]";
                }
            }

            RefreshCountersAndScore();
            _statusLabel.Text = TF("Loc.Runtime.FixDone", "修复完成！健康评分: {0}", _scoreRing.Score);
        }

        private async Task SendToMimsCoreAsync(string trigger, CancellationToken cancellationToken)
        {
            try
            {
                var request = new MimsAskInfoRequest
                {
                    Author = DefaultMimsAuthor,
                    Spec = DefaultMimsSpec,
                    PartNumber = DefaultMimsPartNumber,
                    Date = DateTime.Now,
                    TotalItems = _diagnosticItems.Count,
                    PassCount = _passCount,
                    WarningCount = _warningCount,
                    FailCount = _failCount
                };
                var result = await _externalSystemClient.SendAskInfoAsync(request, cancellationToken);
                if (result.Success)
                {
                    _statusLabel.Text = trigger == "auto"
                        ? TF("Loc.Runtime.AutoReportSuccess", "{0} | 已自动上报 MIMS", _statusLabel.Text)
                        : TF("Loc.Runtime.ManualReportSuccess", "手动上报成功: {0} ({1})", result.Code, result.Endpoint);
                }
                else
                {
                    _statusLabel.Text = trigger == "auto"
                        ? TF("Loc.Runtime.AutoReportFailed", "{0} | MIMS 自动上报失败: {1}", _statusLabel.Text, result.Code)
                        : TF("Loc.Runtime.ManualReportFailed", "手动上报失败: {0} - {1}", result.Code, result.Message);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = trigger == "auto"
                    ? TF("Loc.Runtime.AutoReportException", "{0} | MIMS 自动上报异常: {1}", _statusLabel.Text, ex.Message)
                    : TF("Loc.Runtime.ManualReportException", "手动上报异常: {0}", ex.Message);
            }
        }

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
            _summaryLabel.Text = TF("Loc.App.Summary", "通过 {0} | 风险 {1} | 异常 {2}",
                _passCount, _warningCount, _failCount);
            _scanProgressLabel.Text = TF("Loc.Main.ScannedProgress", "已扫描 {0} / {1} 项",
                scored.Count, _diagnosticItems.Count);
        }

        private void SetButtonsEnabled(bool isScanning)
        {
            _startButton.Enabled = !isScanning;
            _stopButton.Enabled = isScanning;
            _fixAllButton.Enabled = !isScanning;
            _editorButton.Enabled = !isScanning;
            _reportButton.Enabled = !isScanning;
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
            using (var editor = new RunbookEditorForm(_activeRunbook?.Id ?? "default"))
            {
                editor.RunbookSaved += (s, e) => ResetState();
                editor.ShowDialog(FindForm());
            }
        }

        private static string T(string key, string fallback)
        {
            return LanguageService.Instance.Get(key, fallback);
        }

        private static string TF(string key, string fallback, params object[] args)
        {
            return LanguageService.Instance.Format(key, fallback, args);
        }

    }
}
