using System.IO;
using System.Text.Json;
using System.Windows;

namespace MockDiagTool.Services;

public sealed class LanguageService
{
    public const string ZhCn = "zh-CN";
    public const string EnUs = "en-US";
    private const string DefaultLanguage = ZhCn;

    private static LanguageService? _instance;
    public static LanguageService Instance => _instance ??= new LanguageService();

    private readonly string _settingsPath;
    private string _currentLanguage = DefaultLanguage;

    public string CurrentLanguage => _currentLanguage;

    public event Action<string>? LanguageChanged;

    private LanguageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "MockDiagTool", "settings.json");
    }

    public void Initialize()
    {
        _currentLanguage = LoadSavedLanguage();
        ApplyLanguageDictionary(_currentLanguage);
    }

    public void SetLanguage(string language)
    {
        var normalized = NormalizeLanguage(language);
        if (string.Equals(_currentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentLanguage = normalized;
        SaveLanguage(_currentLanguage);
        ApplyLanguageDictionary(_currentLanguage);
    }

    public string Get(string key, string fallback = "")
    {
        var app = Application.Current;
        if (app is null)
        {
            return fallback;
        }

        var value = app.TryFindResource(key);
        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return fallback;
    }

    public string Format(string key, string fallbackFormat, params object[] args)
    {
        var format = Get(key, fallbackFormat);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return fallbackFormat;
        }
    }

    private string LoadSavedLanguage()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return DefaultLanguage;
            }

            var json = File.ReadAllText(_settingsPath);
            var doc = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (doc is null || !doc.TryGetValue("language", out var value))
            {
                return DefaultLanguage;
            }

            return NormalizeLanguage(value);
        }
        catch
        {
            return DefaultLanguage;
        }
    }

    private void SaveLanguage(string language)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var payload = new Dictionary<string, string>
            {
                ["language"] = language
            };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(payload));
        }
        catch
        {
            // Ignore setting write failure.
        }
    }

    private void ApplyLanguageDictionary(string language)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var uri = language.Equals(EnUs, StringComparison.OrdinalIgnoreCase)
            ? new Uri("Resources/Strings.en-US.xaml", UriKind.Relative)
            : new Uri("Resources/Strings.zh-CN.xaml", UriKind.Relative);

        var merged = app.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var source = merged[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("Strings.zh-CN.xaml", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("Strings.en-US.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Add(new ResourceDictionary { Source = uri });
        LanguageChanged?.Invoke(language);
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, EnUs, StringComparison.OrdinalIgnoreCase))
        {
            return EnUs;
        }
        return ZhCn;
    }
}
