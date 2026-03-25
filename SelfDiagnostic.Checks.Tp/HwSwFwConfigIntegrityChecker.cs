using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    public sealed class HwSwFwConfigIntegrityChecker
    {
        private const string FallbackRelativePath = @"config\hwSwFwConfigIntegrity.mock.json";
        private static readonly HttpClient HttpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(6) };

        public async Task<HwSwFwConfigIntegrityResult> CheckAsync(
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken)
        {
            if (runContext?.ExternalConfig == null ||
                !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
            {
                return new HwSwFwConfigIntegrityResult
                {
                    Success = false,
                    Source = "none",
                    FailReasons = new List<string> { "未找到 TMS 接口配置" }
                };
            }

            var apiUrl = ResolveApiUrl(step, runContext.RawMimsConfigXml, tmsEndpoint.Url);
            var fallbackPath = ResolveFallbackPath(step);

            var apiResult = await TryLoadFromApi(apiUrl, cancellationToken);
            if (apiResult != null)
            {
                return new HwSwFwConfigIntegrityResult
                {
                    Success = apiResult.Success,
                    Source = $"tms-api:{apiUrl}",
                    Metrics = apiResult.Metrics,
                    FailReasons = apiResult.FailReasons
                };
            }

            var fallbackResult = TryLoadFromFallback(fallbackPath);
            if (fallbackResult != null)
            {
                return new HwSwFwConfigIntegrityResult
                {
                    Success = fallbackResult.Success,
                    Source = $"fallback-json:{fallbackPath}",
                    Metrics = fallbackResult.Metrics,
                    FailReasons = fallbackResult.FailReasons
                };
            }

            return new HwSwFwConfigIntegrityResult
            {
                Success = false,
                Source = "none",
                FailReasons = new List<string> { $"接口和兜底文件均不可用: {apiUrl}, {fallbackPath}" }
            };
        }

        private static async Task<HwSwFwConfigIntegrityResult> TryLoadFromApi(string apiUrl, CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await HttpClient.GetAsync(apiUrl, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    return ParseResult(json);
                }
            }
            catch
            {
                return null;
            }
        }

        private static HwSwFwConfigIntegrityResult TryLoadFromFallback(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return ParseResult(json);
            }
            catch
            {
                return null;
            }
        }

        private static HwSwFwConfigIntegrityResult ParseResult(string json)
        {
            var metrics = new List<GroupedCheckMetric>();
            var failReasons = new List<string>();

            var root = JToken.Parse(json);

            var items = root["items"];
            if (items != null && items.Type == JTokenType.Array)
            {
                foreach (var item in items)
                {
                    var name = ReadString(item, "name", "key", "metric");
                    var expected = ReadString(item, "expected", "requirement");
                    var actual = ReadString(item, "actual", "value");
                    var pass = ReadBool(item, "pass");
                    metrics.Add(new GroupedCheckMetric
                    {
                        Name = name,
                        Expected = expected,
                        Actual = actual,
                        Pass = pass
                    });
                    if (!pass && !string.IsNullOrWhiteSpace(name))
                    {
                        failReasons.Add($"{name} 不通过");
                    }
                }
            }
            else
            {
                var corrupted = ReadInt(root, "corruptedDataCount");
                var configErrors = ReadInt(root, "configErrorCount");
                metrics.Add(new GroupedCheckMetric
                {
                    Name = "CorruptedData",
                    Expected = "0",
                    Actual = corrupted.ToString(),
                    Pass = corrupted == 0
                });
                metrics.Add(new GroupedCheckMetric
                {
                    Name = "IncorrectConfig",
                    Expected = "0",
                    Actual = configErrors.ToString(),
                    Pass = configErrors == 0
                });
                if (corrupted > 0) failReasons.Add($"损坏数据 {corrupted} 项");
                if (configErrors > 0) failReasons.Add($"错误配置 {configErrors} 项");
            }

            return new HwSwFwConfigIntegrityResult
            {
                Success = metrics.Count > 0 && metrics.All(m => m.Pass),
                Metrics = metrics,
                FailReasons = failReasons
            };
        }

        private static string ResolveApiUrl(RunbookStepDefinition step, string mimsXml, string tmsHealthUrl)
        {
            if (step.Params.TryGetValue("apiUrl", out var apiUrl) && Uri.TryCreate(apiUrl, UriKind.Absolute, out _))
            {
                return apiUrl;
            }

            if (step.Params.TryGetValue("apiPath", out var path) && !string.IsNullOrWhiteSpace(path))
            {
                return new Uri(new Uri(tmsHealthUrl), path).ToString();
            }

            var fromXml = ResolveXmlElement(mimsXml, "HW_CONFIG_CHECK_API");
            if (!string.IsNullOrWhiteSpace(fromXml))
            {
                return fromXml;
            }

            return tmsHealthUrl.Replace("/health", "/api/tms/hw-config-integrity");
        }

        private static string ResolveFallbackPath(RunbookStepDefinition step)
        {
            var rawPath = step.Params.TryGetValue("fallbackFile", out var path) && !string.IsNullOrWhiteSpace(path)
                ? path
                : FallbackRelativePath;
            return Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rawPath);
        }

        private static string ResolveXmlElement(string xml, string key)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return string.Empty;
            }

            try
            {
                var doc = XDocument.Parse(xml);
                var element = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase));
                return element?.Value?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadString(JToken obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = obj[key];
                if (value != null && value.Type == JTokenType.String)
                {
                    return value.Value<string>() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private static bool ReadBool(JToken obj, string key)
        {
            var value = obj[key];
            if (value == null)
            {
                return false;
            }

            return value.Type switch
            {
                JTokenType.Boolean => value.Value<bool>(),
                JTokenType.Integer => value.Value<int>() != 0,
                JTokenType.String => bool.TryParse(value.Value<string>(), out var parsed) && parsed,
                _ => false
            };
        }

        private static int ReadInt(JToken obj, string key)
        {
            var value = obj[key];
            if (value == null)
            {
                return 0;
            }

            return value.Type switch
            {
                JTokenType.Integer => value.Value<int>(),
                JTokenType.String => int.TryParse(value.Value<string>(), out var parsed) ? parsed : 0,
                _ => 0
            };
        }
    }
}
