using System.Windows;
using MockDiagTool.Services;

namespace MockDiagTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Instance.Initialize();
    }
}
