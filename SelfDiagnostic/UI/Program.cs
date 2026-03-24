using System;
using System.Windows.Forms;
using SelfDiagnostic.Services;

namespace SelfDiagnostic.UI
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            LanguageService.Instance.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
