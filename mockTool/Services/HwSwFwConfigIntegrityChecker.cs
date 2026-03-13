using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class HwSwFwConfigIntegrityChecker
{
    private const string FallbackRelativePath = @"config\hwSwFwConfigIntegrity.mock.json";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<HwSwFwConfigIntegrityResult> CheckAsync(
        RunbookStepDefinition step,
        DiagnosticRunContext? runContext,
        CancellationToken cancellationToken)
    {
        if (runContext?.ExternalConfig is null ||
            !runContext.ExternalConfig.Endpoints.TryGetValue(ExternalDependencyIds.Tms, out var tmsEndpoint))
        {
            return new HwSwFwConfigIntegrityResult
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
            return new HwSwFwConfigIntegrityResult
            {
                Success = apiResult.Success,
                Source = $"tms-api:{apiUrl}",
                Metrics = apiResult.Metrics,
                FailReasons = apiResult.FailReasons
            };
        }

        if (TryLoadFromFallback(fallbackPath) is { } fallbackResult)
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
            FailReasons = [$"接口和兜底文件均不可用: {apiUrl}, {fallbackPath}"]
        };
    }

    private static async Task<HwSwFwConfigIntegrityResult?> TryLoadFromApi(string apiUrl, CancellationToken cancellationToken)
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

    private static HwSwFwConfigIntegrityResult? TryLoadFromFallback(string path)
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

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
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

        return tmsHealthUrl.Replace("/health", "/api/tms/hw-config-integrity", StringComparison.OrdinalIgnoreCase);
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

    private static int ReadInt(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String => int.TryParse(value.GetString(), out var parsed) ? parsed : 0,
            _ => 0
        };
    }
}
