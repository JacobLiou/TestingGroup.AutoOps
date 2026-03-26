using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SelfDiagnostic.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 设备版本合规性检查器 — 从 TMS 获取版本要求，与本地实际版本做对比，输出不匹配项列表。
    /// </summary>
    public sealed class DeviceVersionComplianceChecker
    {
        private const string ActualVersionsConfigRelativePath = @"config\deviceActualVersions.json";
        private const string RequirementFallbackConfigRelativePath = @"config\deviceVersionRequirements.mock.json";

        private static readonly HttpClient HttpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        private sealed class ActualVersionConfig
        {
            public List<ActualDeviceItem> Devices { get; set; } = new List<ActualDeviceItem>();
        }

        private sealed class ActualDeviceItem
        {
            public string DeviceKey { get; set; } = string.Empty;
            public string ActualVersion { get; set; } = string.Empty;
        }

        /// <summary>
        /// 从 TMS 请求版本要求并与本地配置的实际版本比对，失败时可回退本地兜底要求文件。
        /// </summary>
        public async Task<DeviceVersionComplianceResult> CheckAsync(
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken)
        {
            var actuals = LoadActualVersions();
            if (runContext?.ExternalConfig == null ||
                !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
            {
                return new DeviceVersionComplianceResult
                {
                    ApiSuccess = false,
                    ApiMessage = "未找到 TMS 接口配置",
                    Actuals = actuals
                };
            }

            var requirementUrl = ResolveRequirementUrl(tmsEndpoint.Url, step);
            try
            {
                var payload = BuildRequestPayload(step);
                using (var request = new HttpRequestMessage(HttpMethod.Post, requirementUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                })
                {
                    using (var response = await HttpClient.SendAsync(request, cancellationToken))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                        {
                            return TryBuildFallbackResult(
                                step,
                                actuals,
                                requirementUrl,
                                $"TMS 返回 HTTP {(int)response.StatusCode}");
                        }

                        var requirements = ParseRequirements(responseBody);
                        var mismatches = Compare(requirements, actuals);
                        return new DeviceVersionComplianceResult
                        {
                            ApiSuccess = true,
                            ApiMessage = "TMS 版本要求获取成功",
                            RequirementUrl = requirementUrl,
                            RequirementSource = "tms",
                            Requirements = requirements,
                            Actuals = actuals,
                            Mismatches = mismatches
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return TryBuildFallbackResult(
                    step,
                    actuals,
                    requirementUrl,
                    $"TMS 请求异常: {ex.Message}");
            }
        }

        private static string ResolveRequirementUrl(string tmsHealthUrl, RunbookStepDefinition step)
        {
            if (step.Params.TryGetValue("requirementUrl", out var fullUrl) && Uri.TryCreate(fullUrl, UriKind.Absolute, out _))
            {
                return fullUrl;
            }

            if (step.Params.TryGetValue("requirementPath", out var path) && !string.IsNullOrWhiteSpace(path))
            {
                var baseUri = new Uri(tmsHealthUrl);
                return new Uri(baseUri, path).ToString();
            }

            return tmsHealthUrl.Replace("/health", "/version-requirements");
        }

        private static string BuildRequestPayload(RunbookStepDefinition step)
        {
            var stationId = step.Params.TryGetValue("stationId", out var station) ? station : "STATION-001";
            var lineId = step.Params.TryGetValue("lineId", out var line) ? line : "LINE-001";
            return $"{{\"stationId\":\"{stationId}\",\"lineId\":\"{lineId}\",\"source\":\"mockTool\"}}";
        }

        private static List<DeviceVersionRequirement> ParseRequirements(string responseBody)
        {
            var requirements = new List<DeviceVersionRequirement>();
            var root = JToken.Parse(responseBody);

            if (root.Type == JTokenType.Object)
            {
                var devices = root["devices"];
                if (devices != null && devices.Type == JTokenType.Array)
                {
                    requirements.AddRange(ParseRequirementArray((JArray)devices));
                }
                else
                {
                    var reqs = root["requirements"];
                    if (reqs != null && reqs.Type == JTokenType.Array)
                    {
                        requirements.AddRange(ParseRequirementArray((JArray)reqs));
                    }
                    else
                    {
                        var items = root["items"];
                        if (items != null && items.Type == JTokenType.Array)
                        {
                            requirements.AddRange(ParseRequirementArray((JArray)items));
                        }
                    }
                }
            }
            else if (root.Type == JTokenType.Array)
            {
                requirements.AddRange(ParseRequirementArray((JArray)root));
            }

            return requirements
                .Where(r => !string.IsNullOrWhiteSpace(r.DeviceKey) && !string.IsNullOrWhiteSpace(r.RequiredVersion))
                .ToList();
        }

        private static List<DeviceVersionRequirement> ParseRequirementArray(JArray array)
        {
            var list = new List<DeviceVersionRequirement>();
            foreach (var item in array)
            {
                if (item.Type != JTokenType.Object) continue;
                var key = ReadString(item, "deviceKey", "device", "name", "id");
                var version = ReadString(item, "requiredVersion", "version", "targetVersion");
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(version))
                {
                    list.Add(new DeviceVersionRequirement
                    {
                        DeviceKey = key.Trim(),
                        RequiredVersion = version.Trim()
                    });
                }
            }
            return list;
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

        private static List<DeviceVersionActual> LoadActualVersions()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ActualVersionsConfigRelativePath);
                if (!File.Exists(path))
                {
                    return new List<DeviceVersionActual>();
                }

                var json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<ActualVersionConfig>(json);
                return config?.Devices
                    .Where(d => !string.IsNullOrWhiteSpace(d.DeviceKey))
                    .Select(d => new DeviceVersionActual
                    {
                        DeviceKey = d.DeviceKey.Trim(),
                        ActualVersion = d.ActualVersion?.Trim() ?? string.Empty
                    })
                    .ToList() ?? new List<DeviceVersionActual>();
            }
            catch
            {
                return new List<DeviceVersionActual>();
            }
        }

        private static DeviceVersionComplianceResult TryBuildFallbackResult(
            RunbookStepDefinition step,
            List<DeviceVersionActual> actuals,
            string requirementUrl,
            string tmsFailureMessage)
        {
            var fallbackFile = ResolveFallbackFile(step);
            var fallbackRequirements = LoadFallbackRequirements(fallbackFile);
            if (fallbackRequirements.Count == 0)
            {
                return new DeviceVersionComplianceResult
                {
                    ApiSuccess = false,
                    ApiMessage = $"{tmsFailureMessage}；本地兜底要求不可用",
                    RequirementUrl = requirementUrl,
                    RequirementSource = "none",
                    Actuals = actuals
                };
            }

            var mismatches = Compare(fallbackRequirements, actuals);
            return new DeviceVersionComplianceResult
            {
                ApiSuccess = true,
                ApiMessage = $"{tmsFailureMessage}；已切换本地兜底要求",
                RequirementUrl = fallbackFile,
                RequirementSource = "local-fallback",
                Requirements = fallbackRequirements,
                Actuals = actuals,
                Mismatches = mismatches
            };
        }

        private static string ResolveFallbackFile(RunbookStepDefinition step)
        {
            if (step.Params.TryGetValue("requirementFallbackFile", out var fallbackFile) &&
                !string.IsNullOrWhiteSpace(fallbackFile))
            {
                return fallbackFile;
            }

            return RequirementFallbackConfigRelativePath;
        }

        private static List<DeviceVersionRequirement> LoadFallbackRequirements(string fallbackFile)
        {
            try
            {
                var path = Path.IsPathRooted(fallbackFile)
                    ? fallbackFile
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fallbackFile);
                if (!File.Exists(path))
                {
                    return new List<DeviceVersionRequirement>();
                }

                var json = File.ReadAllText(path);
                return ParseRequirements(json);
            }
            catch
            {
                return new List<DeviceVersionRequirement>();
            }
        }

        private static List<DeviceVersionMismatch> Compare(
            List<DeviceVersionRequirement> requirements,
            List<DeviceVersionActual> actuals)
        {
            var actualMap = actuals.ToDictionary(a => a.DeviceKey, a => a.ActualVersion, StringComparer.OrdinalIgnoreCase);
            var mismatches = new List<DeviceVersionMismatch>();

            foreach (var requirement in requirements)
            {
                if (!actualMap.TryGetValue(requirement.DeviceKey, out var actual))
                {
                    mismatches.Add(new DeviceVersionMismatch
                    {
                        DeviceKey = requirement.DeviceKey,
                        RequiredVersion = requirement.RequiredVersion,
                        ActualVersion = string.Empty,
                        MissingActual = true
                    });
                    continue;
                }

                if (!string.Equals(requirement.RequiredVersion, actual, StringComparison.OrdinalIgnoreCase))
                {
                    mismatches.Add(new DeviceVersionMismatch
                    {
                        DeviceKey = requirement.DeviceKey,
                        RequiredVersion = requirement.RequiredVersion,
                        ActualVersion = actual
                    });
                }
            }

            return mismatches;
        }
    }
}