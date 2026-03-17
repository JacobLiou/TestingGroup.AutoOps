using MockDiagTool.Services;

namespace AutoOpsWinform.App;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        LanguageService.Instance.Initialize();

        // Optional headless validation path for CI/local verification.
        if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            var selfTest = new SelfTestRunner();
            var exitCode = await selfTest.RunAsync();
            Environment.ExitCode = exitCode;
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}