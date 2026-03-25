using System.Drawing;
using DevExpress.XtraEditors;

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
            Text = "Auto Diagnostic Tool - SelfDiagnostic";
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
