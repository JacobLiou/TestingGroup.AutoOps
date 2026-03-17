using System.ComponentModel;
using MockDiagTool.Models;
using MockDiagTool.Services;
using MockDiagTool.Services.Abstractions;

namespace AutoOpsWinform.App;

public sealed class MainForm : Form
{
    private const string DefaultMimsAuthor = "YUD";
    private const string DefaultMimsSpec = "GUI";
    private const string DefaultMimsPartNumber = "TEST-001";
    private const string DefaultStationId = "STATION-001";
    private const string DefaultLineId = "LINE-001";

    private readonly IExternalSystemClient _externalSystemClient = new MimsGrpcClient(new MimsXmlBuilder());
    private readonly MimsConfigXmlParser _mimsConfigXmlParser = new();
    private readonly MimsStationCapabilityParser _mimsStationCapabilityParser = new();
    private readonly MimsPowerSupplyParser _mimsPowerSupplyParser = new();
    private readonly TpConnectivityInspector _tpConnectivityInspector = new();

    private readonly BindingList<DiagnosticItem> _diagnosticItems = [];
    private readonly Dictionary<string, RunbookStepDefinition> _runbookStepsByStepId = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _scanLock = new();

    private CancellationTokenSource? _cts;
    private RunbookDefinition? _activeRunbook;
    private int _passCount;
    private int _warningCount;
    private int _failCount;

    private readonly Label _runbookLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _externalConfigLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _scanProgressLabel = new();
    private readonly Label _currentScanLabel = new();
    private readonly ComboBox _languageCombo = new();
    private readonly CheckBox _autoScrollCheckBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _fixAllButton = new();
    private readonly Button _reportButton = new();
    private readonly Button _editorButton = new();
    private readonly DataGridView _grid = new();
    private readonly ScoreRingControl _scoreRing = new();

    public MainForm()
    {
        InitializeUi();
        ApplyLanguage();
        ResetState();
        LanguageService.Instance.LanguageChanged += OnLanguageChanged;
        FormClosing += (_, _) => _cts?.Cancel();
    }

    private void InitializeUi()
    {
        Width = 1420;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1200, 700);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var topPanel = BuildTopPanel();
        var statePanel = BuildStatePanel();
        var gridPanel = BuildGridPanel();
        root.Controls.Add(topPanel, 0, 0);
        root.Controls.Add(statePanel, 0, 1);
        root.Controls.Add(gridPanel, 0, 2);
    }

    private Control BuildTopPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            BackColor = Color.White
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));

        _scoreRing.Dock = DockStyle.Fill;
        panel.Controls.Add(_scoreRing, 0, 0);
        panel.SetRowSpan(_scoreRing, 2);

        var titleStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        _runbookLabel.AutoSize = true;
        _runbookLabel.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
        _statusLabel.AutoSize = true;
        _statusLabel.Font = new Font(Font.FontFamily, 10f, FontStyle.Regular);
        _externalConfigLabel.AutoSize = true;
        _externalConfigLabel.ForeColor = Color.DimGray;
        titleStack.Controls.Add(_runbookLabel);
        titleStack.Controls.Add(_statusLabel);
        titleStack.Controls.Add(_externalConfigLabel);
        panel.Controls.Add(titleStack, 1, 0);
        panel.SetRowSpan(titleStack, 2);

        var languageLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        panel.Controls.Add(languageLabel, 2, 0);

        _languageCombo.Dock = DockStyle.Fill;
        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Items.AddRange(["🇨🇳 中文", "🇺🇸 English"]);
        _languageCombo.SelectedIndex = LanguageService.Instance.CurrentLanguage == LanguageService.EnUs ? 1 : 0;
        _languageCombo.SelectedIndexChanged += (_, _) =>
        {
            var code = _languageCombo.SelectedIndex == 1 ? LanguageService.EnUs : LanguageService.ZhCn;
            LanguageService.Instance.SetLanguage(code);
        };
        panel.Controls.Add(_languageCombo, 3, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        InitButton(_startButton, async (_, _) => await StartScanAsync(), Color.FromArgb(41, 128, 185));
        InitButton(_stopButton, (_, _) => StopScan(), Color.FromArgb(149, 165, 166));
        InitButton(_fixAllButton, async (_, _) => await FixAllAsync(), Color.FromArgb(46, 204, 113));
        InitButton(_reportButton, async (_, _) => await SendToMimsCoreAsync("manual", CancellationToken.None), Color.FromArgb(230, 126, 34));
        InitButton(_editorButton, (_, _) => OpenEditor(), Color.FromArgb(142, 68, 173));
        actions.Controls.AddRange([_startButton, _stopButton, _fixAllButton, _reportButton, _editorButton]);
        panel.Controls.Add(actions, 4, 0);

        _autoScrollCheckBox.AutoSize = true;
        _autoScrollCheckBox.Checked = true;
        _autoScrollCheckBox.Dock = DockStyle.Left;
        panel.Controls.Add(_autoScrollCheckBox, 4, 1);

        languageLabel.Name = "LanguageLabel";
        return panel;
    }

    private Control BuildStatePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(10, 6, 10, 6)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        _currentScanLabel.Dock = DockStyle.Fill;
        _currentScanLabel.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleRight;
        _scanProgressLabel.Dock = DockStyle.Bottom;

        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(_currentScanLabel);
        left.Controls.Add(_scanProgressLabel);
        panel.Controls.Add(left, 0, 0);
        panel.Controls.Add(_summaryLabel, 1, 0);
        return panel;
    }

    private Control BuildGridPanel()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.CellFormatting += GridCellFormatting;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DiagnosticItem.Id), HeaderText = "CheckId", Width = 90, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DiagnosticItem.Name), HeaderText = "Name", Width = 280, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DiagnosticItem.CategoryName), HeaderText = "Category", Width = 170, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DiagnosticItem.StatusText), HeaderText = "Status", Width = 130, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DiagnosticItem.Detail), HeaderText = "Detail", Width = 480, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DiagnosticItem.FixSuggestion), HeaderText = "FixSuggestion", Width = 280, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DiagnosticItem.Score), HeaderText = "Score", Width = 70, ReadOnly = true });
        _grid.DataSource = _diagnosticItems;
        return _grid;
    }

    private static void InitButton(Button button, EventHandler onClick, Color backColor)
    {
        button.Width = 78;
        button.Height = 34;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = Color.White;
        button.BackColor = backColor;
        button.Cursor = Cursors.Hand;
        button.Click += onClick;
        button.MouseEnter += (_, _) => button.BackColor = ControlPaint.Light(backColor, 0.1f);
        button.MouseLeave += (_, _) => button.BackColor = backColor;
    }

    private void ApplyLanguage()
    {
        Text = T("Loc.App.Title", "自动诊断工具 - WinForms");
        _startButton.Text = T("Loc.App.Start", "开始诊断");
        _stopButton.Text = T("Loc.App.Stop", "停止");
        _fixAllButton.Text = T("Loc.App.FixAll", "一键修复");
        _reportButton.Text = T("Loc.App.Report", "上报 MIMS");
        _editorButton.Text = T("Loc.App.Editor", "RunBook 编辑器");
        _autoScrollCheckBox.Text = T("Loc.App.AutoScroll", "自动跟随当前项");
        _runbookLabel.Text = $"{T("Loc.App.Runbook", "RunBook")}: {(_activeRunbook?.Id ?? "default")}";
        _scanProgressLabel.Text = TF("Loc.Main.ScannedProgress", "已扫描 {0} / {1} 项", _diagnosticItems.Count(i => i.Status is not (CheckStatus.Pending or CheckStatus.Scanning)), _diagnosticItems.Count);
        if (Controls.OfType<TableLayoutPanel>().FirstOrDefault()?.Controls.Find("LanguageLabel", true).FirstOrDefault() is Label langLabel)
        {
            langLabel.Text = T("Loc.App.Language", "语言");
        }
        _grid.Refresh();
    }

    private void OnLanguageChanged(string _)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnLanguageChanged("_")));
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
        _runbookLabel.Text = $"{T("Loc.App.Runbook", "RunBook")}: {_activeRunbook.Id}";
        _statusLabel.Text = TF("Loc.Runtime.ClickRunbook", "点击“开始诊断”执行 RunBook：{0}", _activeRunbook.Id);
        _externalConfigLabel.Text = TF("Loc.Runtime.ConfigUnavailable", "外部配置不可用: {0}", "未获取 MIMS 外部依赖配置");
        _currentScanLabel.Text = T("Loc.Runtime.Ready", "就绪");
        _summaryLabel.Text = TF("Loc.App.Summary", "通过 {0} | 风险 {1} | 异常 {2}", _passCount, _warningCount, _failCount);
        _scanProgressLabel.Text = TF("Loc.Main.ScannedProgress", "已扫描 {0} / {1} 项", 0, _diagnosticItems.Count);
        _grid.Refresh();
    }

    private async Task StartScanAsync()
    {
        lock (_scanLock)
        {
            if (_cts is { IsCancellationRequested: false })
            {
                return;
            }
            _cts = new CancellationTokenSource();
        }

        SetButtonsEnabled(isScanning: true);
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
                var item = _diagnosticItems.FirstOrDefault(i => i.Id.Equals(currentStep.CheckId, StringComparison.OrdinalIgnoreCase));
                if (item is null)
                {
                    break;
                }

                _currentScanLabel.Text = $"{item.CategoryIcon} {item.Name}";
                SelectCurrentRow(item);

                var outcome = await DiagnosticEngine.RunCheckAsync(item, currentStep, runContext, token);
                _grid.Refresh();
                RefreshCountersAndScore();

                var nextStepId = outcome.Success ? currentStep.NextOnSuccess : currentStep.NextOnFailure;
                currentStep = string.IsNullOrWhiteSpace(nextStepId) || !_runbookStepsByStepId.TryGetValue(nextStepId, out var next)
                    ? null
                    : next;
                stepGuard++;
            }

            _currentScanLabel.Text = T("Loc.Runtime.ScanComplete", "扫描完成");
            _statusLabel.Text = TF("Loc.Runtime.Summary", "体检完成！通过 {0} 项 | 风险 {1} 项 | 异常 {2} 项", _passCount, _warningCount, _failCount);
            await SendToMimsCoreAsync("auto", CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = T("Loc.Runtime.ScanCancelled", "扫描已取消");
            _currentScanLabel.Text = T("Loc.Runtime.Cancelled", "已取消");
        }
        finally
        {
            SetButtonsEnabled(isScanning: false);
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
            if (item.Status is CheckStatus.Warning or CheckStatus.Fail)
            {
                item.Status = CheckStatus.Scanning;
                _grid.Refresh();
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
                ConfigError = $"{configResult.Code} - {configResult.Message}",
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
            ConfigSource = $"MIMS({configResult.Endpoint})",
            TpConnectivity = tpSnapshot,
            StationCapabilityRequirements = stationRequirements,
            PowerSupplyRequirements = powerRequirements,
            RawMimsConfigXml = configResult.ConfigXml
        };
    }

    private void RefreshCountersAndScore()
    {
        _passCount = _diagnosticItems.Count(i => i.Status is CheckStatus.Pass or CheckStatus.Fixed);
        _warningCount = _diagnosticItems.Count(i => i.Status == CheckStatus.Warning);
        _failCount = _diagnosticItems.Count(i => i.Status == CheckStatus.Fail);
        var scored = _diagnosticItems.Where(i => i.Status is not (CheckStatus.Pending or CheckStatus.Scanning)).ToList();
        var score = scored.Count == 0 ? 100 : scored.Sum(i => i.Score) / scored.Count;
        _scoreRing.Score = score;
        _summaryLabel.Text = TF("Loc.App.Summary", "通过 {0} | 风险 {1} | 异常 {2}", _passCount, _warningCount, _failCount);
        _scanProgressLabel.Text = TF("Loc.Main.ScannedProgress", "已扫描 {0} / {1} 项", scored.Count, _diagnosticItems.Count);
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
        if (_grid.Rows.Count == 0)
        {
            return;
        }

        var rowIndex = _diagnosticItems.IndexOf(item);
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
        {
            return;
        }

        _grid.ClearSelection();
        _grid.Rows[rowIndex].Selected = true;
        if (_autoScrollCheckBox.Checked)
        {
            try
            {
                _grid.FirstDisplayedScrollingRowIndex = rowIndex;
            }
            catch
            {
                // Ignore edge scrolling failures.
            }
        }
    }

    private void OpenEditor()
    {
        var editor = new RunbookEditorForm(_activeRunbook?.Id ?? "default");
        editor.RunbookSaved += (_, _) => ResetState();
        editor.ShowDialog(this);
    }

    private void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _diagnosticItems.Count)
        {
            return;
        }

        var item = _diagnosticItems[e.RowIndex];
        var row = _grid.Rows[e.RowIndex];
        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 233, 255);
        row.DefaultCellStyle.SelectionForeColor = Color.Black;

        row.DefaultCellStyle.BackColor = item.Status switch
        {
            CheckStatus.Fail => Color.FromArgb(255, 237, 237),
            CheckStatus.Warning => Color.FromArgb(255, 248, 229),
            CheckStatus.Pass or CheckStatus.Fixed => Color.FromArgb(236, 255, 236),
            CheckStatus.Scanning => Color.FromArgb(233, 245, 255),
            _ => row.DefaultCellStyle.BackColor
        };
    }

    private static string T(string key, string fallback) => LanguageService.Instance.Get(key, fallback);
    private static string TF(string key, string fallback, params object[] args) => LanguageService.Instance.Format(key, fallback, args);
}
