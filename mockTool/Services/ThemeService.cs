using System.IO;
using System.Text.Json;
using System.Windows;

namespace MockDiagTool.Services;

public enum AppTheme
{
    Auto,  // Follow system
    Dark,
    Light
}

/// <summary>
/// Detects and monitors the Windows system theme (Light/Dark).
/// Supports Auto (follow system), manual Dark, and manual Light modes.
/// </summary>
public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private readonly string _settingsFilePath;

    private AppTheme _currentMode = AppTheme.Light;
    public AppTheme CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                SaveSettings();
                ApplyTheme();
            }
        }
    }

    /// <summary>
    /// Whether the currently active visual theme is dark.
    /// </summary>
    public bool IsDarkTheme { get; private set; }

    public event Action<AppTheme, bool>? ThemeChanged; // mode, isDark

    private ThemeService()
    {
        _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.json");
    }

    /// <summary>
    /// Detect Windows system dark/light preference.
    /// Returns true if system is using dark theme.
    /// </summary>
    public static bool IsSystemDarkTheme()
    {
        return false;
    }

    public void Initialize()
    {
        _currentMode = AppTheme.Light;
        SaveSettings();
        ApplyTheme();
    }

    public void CycleTheme()
    {
        // Disabled: app now uses single light theme to match HandyControl style.
        CurrentMode = AppTheme.Light;
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var mode = JsonSerializer.Deserialize<AppTheme>(json);
                _currentMode = mode == AppTheme.Dark ? AppTheme.Light : mode;
            }
        }
        catch
        {
            _currentMode = AppTheme.Light;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currentMode);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { }
    }

    private void ApplyTheme()
    {
        IsDarkTheme = false;
        _currentMode = AppTheme.Light;

        var app = Application.Current;
        if (app != null)
        {
            var themeDictUri = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
            var newDict = new ResourceDictionary { Source = themeDictUri };

            var merged = app.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var uri = merged[i].Source;
                if (uri != null && (uri.OriginalString.Contains("LightTheme") || uri.OriginalString.Contains("DarkTheme")))
                {
                    merged.RemoveAt(i);
                }
            }
            merged.Add(newDict);
        }

        ThemeChanged?.Invoke(_currentMode, false);
    }

    ~ThemeService()
    {
    }
}
