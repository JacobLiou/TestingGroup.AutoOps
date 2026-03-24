using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SelfDiagnostic.Services
{
    public sealed class LanguageService
    {
        public const string ZhCn = "zh-CN";
        public const string EnUs = "en-US";
        private const string DefaultLanguage = ZhCn;

        private static LanguageService _instance;
        private static readonly object InstanceLock = new object();

        public static LanguageService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LanguageService();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly object _sync = new object();
        private readonly string _settingsPath;
        private Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _currentLanguage = DefaultLanguage;

        public string CurrentLanguage => _currentLanguage;
        public event Action<string> LanguageChanged;

        private LanguageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsPath = Path.Combine(appData, "SelfDiagnostic", "settings.json");
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
                if (_strings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
                return fallback;
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
                var doc = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (doc != null && doc.TryGetValue("language", out var value))
                {
                    return value;
                }
                return DefaultLanguage;
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
                File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(payload));
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
                var fileName = string.Equals(language, EnUs, StringComparison.OrdinalIgnoreCase)
                    ? "strings.en-US.json"
                    : "strings.zh-CN.json";
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n", fileName);
                if (!File.Exists(filePath))
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string NormalizeLanguage(string language)
        {
            return string.Equals(language, EnUs, StringComparison.OrdinalIgnoreCase) ? EnUs : ZhCn;
        }
    }
}
