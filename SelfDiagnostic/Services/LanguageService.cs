using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 多语言服务 — 管理 UI 语言切换（中文/英文）。
    /// </summary>
    public sealed class LanguageService
    {
        /// <summary>简体中文区域标识。</summary>
        public const string ZhCn = "zh-CN";
        /// <summary>美式英语区域标识。</summary>
        public const string EnUs = "en-US";
        private const string DefaultLanguage = ZhCn;

        private static LanguageService _instance;
        private static readonly object InstanceLock = new object();

        /// <summary>
        /// 线程安全的单例实例。
        /// </summary>
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

        /// <summary>
        /// 当前 UI 语言代码（规范化后的 zh-CN 或 en-US）。
        /// </summary>
        public string CurrentLanguage => _currentLanguage;
        /// <summary>
        /// 语言切换后触发，参数为新的语言代码。
        /// </summary>
        public event Action<string> LanguageChanged;

        private LanguageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsPath = Path.Combine(appData, "SelfDiagnostic", "settings.json");
        }

        /// <summary>
        /// 从本地设置加载已保存语言并应用对应字符串资源。
        /// </summary>
        public void Initialize()
        {
            var language = NormalizeLanguage(LoadSavedLanguage());
            SetLanguage(language);
        }

        /// <summary>
        /// 设置当前语言、重新加载 i18n 字典并持久化到用户设置文件。
        /// </summary>
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

        /// <summary>
        /// 按键获取本地化字符串；缺失或为空时返回 <paramref name="fallback"/>。
        /// </summary>
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

        /// <summary>
        /// 获取本地化格式字符串后执行 <see cref="string.Format(string, object[])"/>；格式化失败时返回 <paramref name="fallbackFormat"/>。
        /// </summary>
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
