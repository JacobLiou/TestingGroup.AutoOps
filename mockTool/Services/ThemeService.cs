using System.Runtime.InteropServices;
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

    private AppTheme _currentMode = AppTheme.Auto;
    public AppTheme CurrentMode
    {
        get => _currentMode;
        set
        {
            _currentMode = value;
            ApplyTheme();
        }
    }

    /// <summary>
    /// Whether the currently active visual theme is dark.
    /// </summary>
    public bool IsDarkTheme { get; private set; }

    public event Action<bool>? ThemeChanged;

    private ThemeService()
    {
        // Listen for system theme changes
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }

    /// <summary>
    /// Detect Windows system dark/light preference.
    /// Returns true if system is using dark theme.
    /// </summary>
    public static bool IsSystemDarkTheme()
    {
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
        ApplyTheme();
    }

    public void CycleTheme()
    {
        CurrentMode = CurrentMode switch
        {
            AppTheme.Auto => AppTheme.Dark,
            AppTheme.Dark => AppTheme.Light,
            AppTheme.Light => AppTheme.Auto,
            _ => AppTheme.Auto
        };
    }

    private void ApplyTheme()
    {
        bool dark = _currentMode switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDarkTheme() // Auto
        };

        if (dark == IsDarkTheme && Application.Current?.Resources.MergedDictionaries.Count > 0)
            return; // No change needed

        IsDarkTheme = dark;

        var app = Application.Current;
        if (app == null) return;

        // Find and replace the theme dictionary
        var themeDictUri = dark
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        var newDict = new ResourceDictionary { Source = themeDictUri };

        // Remove existing theme dictionaries and add new one
        var merged = app.Resources.MergedDictionaries;
        // Remove any existing theme dictionary (keep other dicts)
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var uri = merged[i].Source;
            if (uri != null && (uri.OriginalString.Contains("DarkTheme") || uri.OriginalString.Contains("LightTheme")))
            {
                merged.RemoveAt(i);
            }
        }
        merged.Insert(0, newDict);

        ThemeChanged?.Invoke(dark);
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
