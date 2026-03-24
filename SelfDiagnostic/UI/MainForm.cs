using System.Drawing;
using DevExpress.XtraEditors;
using SelfDiagnostic.Services;

namespace SelfDiagnostic.UI
{
    /// <summary>
    /// Standalone form wrapper for the DiagnosticMainControl.
    /// When embedding into MIMS, use DiagnosticMainControl directly instead.
    /// </summary>
    public sealed class MainForm : XtraForm
    {
        public MainForm()
        {
            Text = LanguageService.Instance.Get("Loc.App.Title", "自动诊断工具 - SelfDiagnostic");
            Width = 1420;
            Height = 860;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            MinimumSize = new Size(1200, 700);

            var diagnosticControl = new DiagnosticMainControl
            {
                Dock = System.Windows.Forms.DockStyle.Fill
            };
            Controls.Add(diagnosticControl);
        }
    }
}
