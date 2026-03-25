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
    public sealed class DefaultInfoAndLutChecker
    {
        private static readonly HttpClient HttpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        private const string LocalLutFallbackRelativePath = @"config\lut\default_station.lut";

        public async Task<DefaultInfoAndLutResult> CheckAsync(
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken)
        {
            if (runContext?.ExternalConfig == null ||
                !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
            {
                return new DefaultInfoAndLutResult
                {
                    Success = false,
                    Source = "none",
                    FailReasons = new List<string> { "未找到 TMS 接口配置" }
                };
            }

            var defaultInfoUrl = ResolveDefaultInfoUrl(step, runContext.RawMimsConfigXml, tmsEndpoint.Url);
            var lutDownloadUrl = ResolveLutDownloadUrl(step, runContext.RawMimsConfigXml, tmsEndpoint.Url);
            var expectedStation = ResolveExpected(step, "expectedStationId", runContext.RawMimsConfigXml, "STATION_ID", "STATION-001");
            var expectedLine = ResolveExpected(step, "expectedLineId", runContext.RawMimsConfigXml, "LINE_ID", "LINE-001");

            var failReasons = new List<string>();
            var source = "mims-config+tms-api";
            var defaultInfoSummary = string.Empty;
            var lutSummary = string.Empty;

            try
            {
                using (var response = await HttpClient.GetAsync(defaultInfoUrl, cancellationToken))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        failReasons.Add($"默认信息接口失败: HTTP {(int)response.StatusCode}");
                    }
                    else
                    {
                        var root = JToken.Parse(body);
                        var stationId = ReadString(root, "stationId", "station_id");
                        var lineId = ReadString(root, "lineId", "line_id");
                        var partNumber = ReadString(root, "partNumber", "part_no");
                        var author = ReadString(root, "author", "owner");
                        var spec = ReadString(root, "spec", "productSpec");

                        if (string.IsNullOrWhiteSpace(stationId) || string.IsNullOrWhiteSpace(lineId) ||
                            string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(spec))
                        {
                            failReasons.Add("默认信息字段不完备（stationId/lineId/partNumber/author/spec）");
                        }
                        else
                        {
                            if (!string.Equals(stationId, expectedStation, StringComparison.OrdinalIgnoreCase))
                            {
                                failReasons.Add($"默认信息 stationId 不准确: 实际 {stationId} / 期望 {expectedStation}");
                            }
                            if (!string.Equals(lineId, expectedLine, StringComparison.OrdinalIgnoreCase))
                            {
                                failReasons.Add($"默认信息 lineId 不准确: 实际 {lineId} / 期望 {expectedLine}");
                            }
                        }

                        defaultInfoSummary = $"station={stationId}, line={lineId}, part={partNumber}, author={author}, spec={spec}";
                    }
                }
            }
            catch (Exception ex)
            {
                failReasons.Add($"默认信息检查异常: {ex.Message}");
            }

            try
            {
                using (var response = await HttpClient.GetAsync(lutDownloadUrl, cancellationToken))
                {
                    var lutContent = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(lutContent))
                    {
                        var fallbackPath = ResolveLocalLutPath(step);
                        if (!File.Exists(fallbackPath))
                        {
                            failReasons.Add($"LUT 下载失败且本地兜底不存在: {fallbackPath}");
                        }
                        else
                        {
                            lutContent = File.ReadAllText(fallbackPath);
                            source = "mims-config+local-lut-fallback";
                        }
                    }

                    if (string.IsNullOrWhiteSpace(lutContent))
                    {
                        failReasons.Add("LUT 内容为空");
                    }
                    else
                    {
                        var hasHeader = lutContent.IndexOf("LUT_VERSION", StringComparison.OrdinalIgnoreCase) >= 0;
                        var hasData = lutContent.IndexOf("TABLE_BEGIN", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                      lutContent.IndexOf("TABLE_END", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!hasHeader || !hasData)
                        {
                            failReasons.Add("LUT 内容不完整，缺少必要标识（LUT_VERSION/TABLE_BEGIN/TABLE_END）");
                        }

                        var lineCount = lutContent
                            .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Length;
                        lutSummary = $"lines={lineCount}, size={lutContent.Length} chars";
                    }
                }
            }
            catch (Exception ex)
            {
                failReasons.Add($"LUT 下载检查异常: {ex.Message}");
            }

            return new DefaultInfoAndLutResult
            {
                Success = failReasons.Count == 0,
                Source = source,
                DefaultInfoUrl = defaultInfoUrl,
                LutDownloadUrl = lutDownloadUrl,
                DefaultInfoSummary = defaultInfoSummary,
                LutSummary = lutSummary,
                FailReasons = failReasons
            };
        }

        private static string ResolveDefaultInfoUrl(RunbookStepDefinition step, string mimsXml, string tmsHealthUrl)
        {
            if (step.Params.TryGetValue("defaultInfoUrl", out var fullUrl) && Uri.TryCreate(fullUrl, UriKind.Absolute, out _))
            {
                return fullUrl;
            }

            if (step.Params.TryGetValue("defaultInfoPath", out var path) && !string.IsNullOrWhiteSpace(path))
            {
                var baseUri = new Uri(tmsHealthUrl);
                return new Uri(baseUri, path).ToString();
            }

            var fromXml = ResolveFromXml(mimsXml, "DEFAULT_INFO_API", "DEFAULT_INFO_URL");
            if (!string.IsNullOrWhiteSpace(fromXml))
            {
                return fromXml;
            }

            return tmsHealthUrl.Replace("/health", "/default-info");
        }

        private static string ResolveLutDownloadUrl(RunbookStepDefinition step, string mimsXml, string tmsHealthUrl)
        {
            if (step.Params.TryGetValue("lutDownloadUrl", out var fullUrl) && Uri.TryCreate(fullUrl, UriKind.Absolute, out _))
            {
                return fullUrl;
            }

            if (step.Params.TryGetValue("lutDownloadPath", out var path) && !string.IsNullOrWhiteSpace(path))
            {
                var baseUri = new Uri(tmsHealthUrl);
                return new Uri(baseUri, path).ToString();
            }

            var fromXml = ResolveFromXml(mimsXml, "LUT_DOWNLOAD_API", "LUT_API");
            if (!string.IsNullOrWhiteSpace(fromXml))
            {
                return fromXml;
            }

            return tmsHealthUrl.Replace("/health", "/lut/download/default");
        }

        private static string ResolveExpected(RunbookStepDefinition step, string key, string mimsXml, string xmlAttrKey, string fallback)
        {
            if (step.Params.TryGetValue(key, out var param) && !string.IsNullOrWhiteSpace(param))
            {
                return param.Trim();
            }

            var fromXml = ResolveAttributeFromXml(mimsXml, xmlAttrKey);
            return string.IsNullOrWhiteSpace(fromXml) ? fallback : fromXml;
        }

        private static string ResolveLocalLutPath(RunbookStepDefinition step)
        {
            var localPath = step.Params.TryGetValue("lutFallbackFile", out var path) && !string.IsNullOrWhiteSpace(path)
                ? path
                : LocalLutFallbackRelativePath;
            return Path.IsPathRooted(localPath)
                ? localPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, localPath);
        }

        private static string ResolveFromXml(string xml, params string[] keys)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return string.Empty;
            }

            try
            {
                var doc = XDocument.Parse(xml);
                foreach (var key in keys)
                {
                    var element = doc.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (element != null && !string.IsNullOrWhiteSpace(element.Value))
                    {
                        return element.Value.Trim();
                    }
                }
            }
            catch
            {
                // ignore parsing failure and keep defaults
            }

            return string.Empty;
        }

        private static string ResolveAttributeFromXml(string xml, string key)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return string.Empty;
            }

            try
            {
                var doc = XDocument.Parse(xml);
                var attr = doc.Descendants()
                    .Attributes()
                    .FirstOrDefault(a => a.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
                {
                    return attr.Value.Trim();
                }
            }
            catch
            {
                // ignore parsing failure and keep defaults
            }

            return string.Empty;
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
    }
}
