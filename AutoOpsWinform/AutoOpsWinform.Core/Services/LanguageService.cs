using System.Text.Json;

namespace MockDiagTool.Services;

public sealed class LanguageService
{
    public const string ZhCn = "zh-CN";
    public const string EnUs = "en-US";
    private const string DefaultLanguage = ZhCn;

    private static LanguageService? _instance;
    public static LanguageService Instance => _instance ??= new LanguageService();

    private readonly object _sync = new();
    private readonly string _settingsPath;
    private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
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
        var language = NormalizeLanguage(LoadSavedLanguage());
        SetLanguage(language);
    }

    public void SetLanguage(string language)
    {
        var normalized = NormalizeLanguage(language);
        lock (_sync)
        {
            _currentLanguage = normalized;
            _strings = LoadLanguageDictionary(normalized);
        }

        SaveLanguage(normalized);
        LanguageChanged?.Invoke(normalized);
    }

    public string Get(string key, string fallback = "")
    {
        lock (_sync)
        {
            return _strings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }
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
            return doc is not null && doc.TryGetValue("language", out var value)
                ? value
                : DefaultLanguage;
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

            var payload = new Dictionary<string, string> { ["language"] = language };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(payload));
        }
        catch
        {
            // Ignore persistence failures.
        }
    }

    private static Dictionary<string, string> LoadLanguageDictionary(string language)
    {
        try
        {
            var fileName = language.Equals(EnUs, StringComparison.OrdinalIgnoreCase)
                ? "strings.en-US.json"
                : "strings.zh-CN.json";
            var filePath = Path.Combine(AppContext.BaseDirectory, "i18n", fileName);
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        return string.Equals(language, EnUs, StringComparison.OrdinalIgnoreCase) ? EnUs : ZhCn;
    }
}
