using Newtonsoft.Json.Linq;
using SelfDiagnostic.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 光路残余风险检查器 — 分析 RTS 后光路残余异常风险。
    /// </summary>
    public sealed class OpticalPathResidualRiskChecker
    {
        private const string FallbackRelativePath = @"config\opticalResidualRisk.mock.json";
        private static readonly HttpClient HttpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(6) };

        /// <summary>
        /// 从 TMS 或本地兜底 JSON 获取 RTS 完成情况及残余问题列表，汇总为指标并判定风险是否可接受。
        /// </summary>
        public async Task<OpticalResidualRiskResult> CheckAsync(
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken)
        {
            if (runContext?.ExternalConfig == null ||
                !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
            {
                return new OpticalResidualRiskResult
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
                return new OpticalResidualRiskResult
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
                return new OpticalResidualRiskResult
                {
                    Success = fallbackResult.Success,
                    Source = $"fallback-json:{fallbackPath}",
                    Metrics = fallbackResult.Metrics,
                    FailReasons = fallbackResult.FailReasons
                };
            }

            return new OpticalResidualRiskResult
            {
                Success = false,
                Source = "none",
                FailReasons = new List<string> { $"接口和兜底文件均不可用: {apiUrl}, {fallbackPath}" }
            };
        }

        private static async Task<OpticalResidualRiskResult> TryLoadFromApi(string apiUrl, CancellationToken cancellationToken)
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

        private static OpticalResidualRiskResult TryLoadFromFallback(string path)
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

        private static OpticalResidualRiskResult ParseResult(string json)
        {
            var root = JToken.Parse(json);
            var metrics = new List<GroupedCheckMetric>();
            var failReasons = new List<string>();

            var rtsPass = ReadBool(root, "rtsCompleted");
            var rtsActual = rtsPass ? "completed" : "not_completed";
            metrics.Add(new GroupedCheckMetric
            {
                Name = "RTSProcess",
                Expected = "completed",
                Actual = rtsActual,
                Pass = rtsPass
            });
            if (!rtsPass)
            {
                failReasons.Add("RTS 流程未完成");
            }

            var items = root["items"];
            if (items != null && items.Type == JTokenType.Array)
            {
                foreach (var item in items)
                {
                    var metric = new GroupedCheckMetric
                    {
                        Name = ReadString(item, "name", "metric"),
                        Expected = ReadString(item, "expected", "target"),
                        Actual = ReadString(item, "actual", "value"),
                        Pass = ReadBool(item, "pass")
                    };
                    metrics.Add(metric);
                    if (!metric.Pass && !string.IsNullOrWhiteSpace(metric.Name))
                    {
                        failReasons.Add($"{metric.Name} 未消除异常");
                    }
                }
            }

            var residualIssues = root["residualIssues"];
            if (residualIssues != null &&
                residualIssues.Type == JTokenType.Array &&
                residualIssues.Count() > 0)
            {
                foreach (var issue in residualIssues)
                {
                    if (issue.Type == JTokenType.String)
                    {
                        failReasons.Add($"残留异常: {issue.Value<string>()}");
                    }
                }
            }

            return new OpticalResidualRiskResult
            {
                Success = metrics.Count > 0 && metrics.All(m => m.Pass) && failReasons.Count == 0,
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

            var fromXml = ResolveXmlElement(mimsXml, "OPTICAL_RISK_API");
            if (!string.IsNullOrWhiteSpace(fromXml))
            {
                return fromXml;
            }

            return tmsHealthUrl.Replace("/health", "/api/tms/optical-risk");
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