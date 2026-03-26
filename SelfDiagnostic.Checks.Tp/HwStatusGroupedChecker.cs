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
    /// <summary>
    /// 硬件状态分组检查器 — 按功能组（光学 / 控制存储 / 接口通信）检查各硬件设备状态。
    /// </summary>
    public sealed class HwStatusGroupedChecker
    {
        private const string FallbackRelativePath = @"config\hwStatusGrouped.mock.json";
        private static readonly HttpClient HttpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(6) };

        /// <summary>
        /// 按步骤中的分组键从 TMS 或本地兜底 JSON 加载该组硬件状态指标并汇总通过情况。
        /// </summary>
        public async Task<HwStatusGroupedResult> CheckAsync(
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken)
        {
            var groupKey = step.Params.TryGetValue("groupKey", out var group) && !string.IsNullOrWhiteSpace(group)
                ? group.Trim().ToLowerInvariant()
                : "optical";

            if (runContext?.ExternalConfig == null ||
                !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
            {
                return new HwStatusGroupedResult
                {
                    Success = false,
                    GroupKey = groupKey,
                    Source = "none",
                    FailReasons = new List<string> { "未找到 TMS 接口配置" }
                };
            }

            var apiUrl = ResolveApiUrl(step, runContext.RawMimsConfigXml, tmsEndpoint.Url, groupKey);
            var fallbackPath = ResolveFallbackPath(step);

            var apiResult = await TryLoadFromApi(apiUrl, groupKey, cancellationToken);
            if (apiResult != null)
            {
                return apiResult;
            }

            var fallbackResult = TryLoadFromFallback(fallbackPath, groupKey);
            if (fallbackResult != null)
            {
                return new HwStatusGroupedResult
                {
                    Success = fallbackResult.Success,
                    GroupKey = fallbackResult.GroupKey,
                    Source = $"fallback-json:{fallbackPath}",
                    Metrics = fallbackResult.Metrics,
                    FailReasons = fallbackResult.FailReasons
                };
            }

            return new HwStatusGroupedResult
            {
                Success = false,
                GroupKey = groupKey,
                Source = "none",
                FailReasons = new List<string> { $"接口和兜底文件均不可用: {apiUrl}, {fallbackPath}" }
            };
        }

        private static async Task<HwStatusGroupedResult> TryLoadFromApi(string apiUrl, string groupKey, CancellationToken cancellationToken)
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
                    return ParseResult(json, groupKey, $"tms-api:{apiUrl}");
                }
            }
            catch
            {
                return null;
            }
        }

        private static HwStatusGroupedResult TryLoadFromFallback(string path, string groupKey)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return ParseResult(json, groupKey, string.Empty);
            }
            catch
            {
                return null;
            }
        }

        private static HwStatusGroupedResult ParseResult(string json, string groupKey, string source)
        {
            var root = JToken.Parse(json);
            JToken targetGroup = null;
            var found = false;

            var groups = root["groups"];
            if (groups != null && groups.Type == JTokenType.Array)
            {
                foreach (var groupItem in groups)
                {
                    var key = ReadString(groupItem, "groupKey", "id", "name").ToLowerInvariant();
                    if (key == groupKey)
                    {
                        targetGroup = groupItem;
                        found = true;
                        break;
                    }
                }
            }
            else
            {
                targetGroup = root;
                found = true;
            }

            if (!found)
            {
                return new HwStatusGroupedResult
                {
                    Success = false,
                    GroupKey = groupKey,
                    Source = source,
                    FailReasons = new List<string> { $"未找到分组: {groupKey}" }
                };
            }

            var metrics = new List<GroupedCheckMetric>();
            var failReasons = new List<string>();
            var items = targetGroup["items"];
            if (items != null && items.Type == JTokenType.Array)
            {
                foreach (var item in items)
                {
                    var metric = new GroupedCheckMetric
                    {
                        Name = ReadString(item, "name", "device", "metric"),
                        Expected = ReadString(item, "expected", "requirement", "target"),
                        Actual = ReadString(item, "actual", "value"),
                        Pass = ReadBool(item, "pass")
                    };
                    metrics.Add(metric);
                    if (!metric.Pass && !string.IsNullOrWhiteSpace(metric.Name))
                    {
                        failReasons.Add($"{metric.Name} 不通过");
                    }
                }
            }

            return new HwStatusGroupedResult
            {
                Success = metrics.Count > 0 && metrics.All(m => m.Pass),
                GroupKey = groupKey,
                Source = source,
                Metrics = metrics,
                FailReasons = failReasons
            };
        }

        private static string ResolveApiUrl(RunbookStepDefinition step, string mimsXml, string tmsHealthUrl, string groupKey)
        {
            if (step.Params.TryGetValue("apiUrl", out var apiUrl) && Uri.TryCreate(apiUrl, UriKind.Absolute, out _))
            {
                return apiUrl;
            }

            if (step.Params.TryGetValue("apiPath", out var path) && !string.IsNullOrWhiteSpace(path))
            {
                var url = new Uri(new Uri(tmsHealthUrl), path).ToString();
                return url.IndexOf("group=", StringComparison.OrdinalIgnoreCase) >= 0
                    ? url
                    : $"{url}{(url.Contains("?") ? "&" : "?")}group={groupKey}";
            }

            var fromXml = ResolveXmlElement(mimsXml, "HW_STATUS_GROUPS_API");
            if (!string.IsNullOrWhiteSpace(fromXml))
            {
                return fromXml.IndexOf("group=", StringComparison.OrdinalIgnoreCase) >= 0
                    ? fromXml
                    : $"{fromXml}{(fromXml.Contains("?") ? "&" : "?")}group={groupKey}";
            }

            var baseUrl = tmsHealthUrl.Replace("/health", "/api/tms/hw-status-groups");
            return $"{baseUrl}?group={groupKey}";
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
    }
}
