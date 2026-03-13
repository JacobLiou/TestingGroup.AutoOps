using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

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

    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";
    private readonly string _settingsFilePath;

    private AppTheme _currentMode = AppTheme.Auto;
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
        // Listen for system theme changes
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }

    /// <summary>
    /// Detect Windows system dark/light preference.
    /// Returns true if system is using dark theme.
    /// </summary>
    public static bool IsSystemDarkTheme()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;
            
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            if (value is int intVal)
                return intVal == 0; // 0 = dark, 1 = light
        }
        catch { }
        return true; // Default to dark
    }

    public void Initialize()
    {
        // Always start in Auto mode so theme follows OS at launch.
        _currentMode = AppTheme.Auto;
        SaveSettings();
        ApplyTheme();
    }

    public void CycleTheme()
    {
        // Toggle by effective visual state to guarantee one-click dark/light switch.
        CurrentMode = IsDarkTheme ? AppTheme.Light : AppTheme.Dark;
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var mode = JsonSerializer.Deserialize<AppTheme>(json);
                _currentMode = mode;
            }
        }
        catch
        {
            _currentMode = AppTheme.Auto;
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
        bool dark = _currentMode switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDarkTheme() // Auto
        };

        IsDarkTheme = dark;

        var app = Application.Current;
        if (app != null)
        {
            // Find and replace the theme dictionary
            var themeDictUri = dark
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

            var newDict = new ResourceDictionary { Source = themeDictUri };

            // Remove existing theme dictionaries and add new one
            var merged = app.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var uri = merged[i].Source;
                if (uri != null && (uri.OriginalString.Contains("DarkTheme") || uri.OriginalString.Contains("LightTheme")))
                {
                    merged.RemoveAt(i);
                }
            }
            merged.Insert(0, newDict);
        }

        ThemeChanged?.Invoke(_currentMode, dark);
    }

    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentMode == AppTheme.Auto)
        {
            // System theme changed and we're in auto mode
            Application.Current?.Dispatcher.Invoke(ApplyTheme);
        }
    }

    ~ThemeService()
    {
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
    }
}
