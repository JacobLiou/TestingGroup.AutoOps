using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class OpticalPathResidualRiskChecker
{
    private const string FallbackRelativePath = @"config\opticalResidualRisk.mock.json";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<OpticalResidualRiskResult> CheckAsync(
        RunbookStepDefinition step,
        DiagnosticRunContext? runContext,
        CancellationToken cancellationToken)
    {
        if (runContext?.ExternalConfig is null ||
            !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
        {
            return new OpticalResidualRiskResult
            {
                Success = false,
                Source = "none",
                FailReasons = ["未找到 TMS 接口配置"]
            };
        }

        var apiUrl = ResolveApiUrl(step, runContext.RawMimsConfigXml, tmsEndpoint.Url);
        var fallbackPath = ResolveFallbackPath(step);

        if (await TryLoadFromApi(apiUrl, cancellationToken) is { } apiResult)
        {
            return new OpticalResidualRiskResult
            {
                Success = apiResult.Success,
                Source = $"tms-api:{apiUrl}",
                Metrics = apiResult.Metrics,
                FailReasons = apiResult.FailReasons
            };
        }

        if (TryLoadFromFallback(fallbackPath) is { } fallbackResult)
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
            FailReasons = [$"接口和兜底文件均不可用: {apiUrl}, {fallbackPath}"]
        };
    }

    private static async Task<OpticalResidualRiskResult?> TryLoadFromApi(string apiUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResult(json);
        }
        catch
        {
            return null;
        }
    }

    private static OpticalResidualRiskResult? TryLoadFromFallback(string path)
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
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
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

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
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

        if (root.TryGetProperty("residualIssues", out var residualIssues) &&
            residualIssues.ValueKind == JsonValueKind.Array &&
            residualIssues.GetArrayLength() > 0)
        {
            foreach (var issue in residualIssues.EnumerateArray())
            {
                if (issue.ValueKind == JsonValueKind.String)
                {
                    failReasons.Add($"残留异常: {issue.GetString()}");
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

        return tmsHealthUrl.Replace("/health", "/api/tms/optical-risk", StringComparison.OrdinalIgnoreCase);
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
