using System.Drawing;
using DevExpress.XtraEditors;

namespace SelfDiagnostic.UI
{
    /// <summary>
    /// 主窗体 — 应用程序入口窗口，承载 DiagnosticMainControl。
    /// </summary>
    public sealed class MainForm : XtraForm
    {
        /// <summary>
        /// 创建主窗体并填充 <see cref="DiagnosticMainControl"/>。
        /// </summary>
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
