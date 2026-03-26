using System;
using System.Windows.Forms;

namespace SelfDiagnostic.UI
{
    /// <summary>
    /// 程序入口点
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 应用程序主入口：启用可视化样式并启动主窗体。
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
