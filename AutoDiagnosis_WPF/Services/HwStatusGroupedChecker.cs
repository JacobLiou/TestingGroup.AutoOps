using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class HwStatusGroupedChecker
{
    private const string FallbackRelativePath = @"config\hwStatusGrouped.mock.json";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<HwStatusGroupedResult> CheckAsync(
        RunbookStepDefinition step,
        DiagnosticRunContext? runContext,
        CancellationToken cancellationToken)
    {
        var groupKey = step.Params.TryGetValue("groupKey", out var group) && !string.IsNullOrWhiteSpace(group)
            ? group.Trim().ToLowerInvariant()
            : "optical";

        if (runContext?.ExternalConfig is null ||
            !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
        {
            return new HwStatusGroupedResult
            {
                Success = false,
                GroupKey = groupKey,
                Source = "none",
                FailReasons = ["未找到 TMS 接口配置"]
            };
        }

        var apiUrl = ResolveApiUrl(step, runContext.RawMimsConfigXml, tmsEndpoint.Url, groupKey);
        var fallbackPath = ResolveFallbackPath(step);

        if (await TryLoadFromApi(apiUrl, groupKey, cancellationToken) is { } apiResult)
        {
            return apiResult;
        }

        if (TryLoadFromFallback(fallbackPath, groupKey) is { } fallbackResult)
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
            FailReasons = [$"接口和兜底文件均不可用: {apiUrl}, {fallbackPath}"]
        };
    }

    private static async Task<HwStatusGroupedResult?> TryLoadFromApi(string apiUrl, string groupKey, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResult(json, groupKey, $"tms-api:{apiUrl}");
        }
        catch
        {
            return null;
        }
    }

    private static HwStatusGroupedResult? TryLoadFromFallback(string path, string groupKey)
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
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement targetGroup = default;
        var found = false;

        if (root.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in groups.EnumerateArray())
            {
                var key = ReadString(group, "groupKey", "id", "name").ToLowerInvariant();
                if (key == groupKey)
                {
                    targetGroup = group;
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
                FailReasons = [$"未找到分组: {groupKey}"]
            };
        }

        var metrics = new List<GroupedCheckMetric>();
        var failReasons = new List<string>();
        if (targetGroup.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
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
            return url.Contains("group=", StringComparison.OrdinalIgnoreCase)
                ? url
                : $"{url}{(url.Contains('?') ? "&" : "?")}group={groupKey}";
        }

        var fromXml = ResolveXmlElement(mimsXml, "HW_STATUS_GROUPS_API");
        if (!string.IsNullOrWhiteSpace(fromXml))
        {
            return fromXml.Contains("group=", StringComparison.OrdinalIgnoreCase)
                ? fromXml
                : $"{fromXml}{(fromXml.Contains('?') ? "&" : "?")}group={groupKey}";
        }

        var baseUrl = tmsHealthUrl.Replace("/health", "/api/tms/hw-status-groups", StringComparison.OrdinalIgnoreCase);
        return $"{baseUrl}?group={groupKey}";
    }

    private static string ResolveFallbackPath(RunbookStepDefinition step)
    {
        var rawPath = step.Params.TryGetValue("fallbackFile", out var path) && !string.IsNullOrWhiteSpace(path)
            ? path
            : FallbackRelativePath;
        return Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(AppContext.BaseDirectory, rawPath);
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

    private static string ReadString(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static bool ReadBool(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.GetInt32() != 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }
}
