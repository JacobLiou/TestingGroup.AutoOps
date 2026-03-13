using System.IO;
using System.Net.Http;
using System.Text.Json;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class StationCapabilityComplianceChecker
{
    private const string ActualMetricsConfigRelativePath = @"config\stationActualMetrics.json";
    private const string TpConnectivityConfigRelativePath = @"config\tpConnectivity.json";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
    private static readonly string[] TpMetricFileNames =
    [
        "stationActualMetrics.json",
        "station_metrics.json",
        "tp_station_metrics.json",
        "station_capability.json",
        "capability_metrics.json"
    ];

    private sealed class ActualMetricsConfig
    {
        public double GrrPercent { get; set; }
        public double GdsPercent { get; set; }
        public double MaxOutputPowerDbm { get; set; }
        public double SnrDb { get; set; }
        public double SwitchRepeatabilityDb { get; set; }
        public double PowerStabilityDb { get; set; }
        public string ChannelPlanActual { get; set; } = "100G-4CH";
    }

    private sealed class TpConnectivityConfig
    {
        public string TpRootPath { get; set; } = string.Empty;
        public string StationMetricsApiUrl { get; set; } = string.Empty;
        public string StationMetricsRelativePath { get; set; } = string.Empty;
    }

    public StationCapabilityComplianceResult Check(DiagnosticRunContext? runContext)
    {
        var req = runContext?.StationCapabilityRequirements;
        if (req is null)
        {
            return new StationCapabilityComplianceResult
            {
                Success = false,
                Metrics =
                [
                    new StationCapabilityMetricResult
                    {
                        Metric = "StationCapability",
                        Required = "MIMS requirement",
                        Actual = "Unavailable",
                        Pass = false
                    }
                ]
            };
        }

        var (actual, source) = LoadActual(runContext);
        var metrics = new List<StationCapabilityMetricResult>
        {
            new()
            {
                Metric = "GR&R",
                Required = $"<= {req.GrrMaxPercent:F2}%",
                Actual = $"{actual.GrrPercent:F2}%",
                Pass = actual.GrrPercent <= req.GrrMaxPercent
            },
            new()
            {
                Metric = "GDS",
                Required = $">= {req.GdsMinPercent:F2}%",
                Actual = $"{actual.GdsPercent:F2}%",
                Pass = actual.GdsPercent >= req.GdsMinPercent
            },
            new()
            {
                Metric = "最大出光功率",
                Required = $">= {req.MaxOutputPowerMinDbm:F2} dBm",
                Actual = $"{actual.MaxOutputPowerDbm:F2} dBm",
                Pass = actual.MaxOutputPowerDbm >= req.MaxOutputPowerMinDbm
            },
            new()
            {
                Metric = "信噪比",
                Required = $">= {req.SnrMinDb:F2} dB",
                Actual = $"{actual.SnrDb:F2} dB",
                Pass = actual.SnrDb >= req.SnrMinDb
            },
            new()
            {
                Metric = "开关重复性",
                Required = $"<= {req.SwitchRepeatabilityMaxDb:F2} dB",
                Actual = $"{actual.SwitchRepeatabilityDb:F2} dB",
                Pass = actual.SwitchRepeatabilityDb <= req.SwitchRepeatabilityMaxDb
            },
            new()
            {
                Metric = "功率稳定性",
                Required = $"<= {req.PowerStabilityMaxDb:F2} dB",
                Actual = $"{actual.PowerStabilityDb:F2} dB",
                Pass = actual.PowerStabilityDb <= req.PowerStabilityMaxDb
            },
            new()
            {
                Metric = "Channel Plan",
                Required = req.ChannelPlanRequired,
                Actual = actual.ChannelPlanActual,
                Pass = string.Equals(actual.ChannelPlanActual, req.ChannelPlanRequired, StringComparison.OrdinalIgnoreCase)
            }
        };

        return new StationCapabilityComplianceResult
        {
            Success = metrics.All(m => m.Pass),
            ActualSource = source,
            Metrics = metrics
        };
    }

    private static (StationCapabilityActual Actual, string Source) LoadActual(DiagnosticRunContext? runContext)
    {
        var tpConfig = LoadTpConnectivityConfig();
        var tpRootPath = ResolveTpRootPath(runContext, tpConfig);

        if (TryLoadFromMeasurementApi(tpConfig.StationMetricsApiUrl, out var apiActual, out var apiSource))
        {
            return (apiActual, apiSource);
        }

        if (TryLoadFromTpFile(tpRootPath, tpConfig.StationMetricsRelativePath, runContext?.TpConnectivity?.ConfigFiles ?? [], out var tpActual, out var tpSource))
        {
            return (tpActual, tpSource);
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, ActualMetricsConfigRelativePath);
            if (!File.Exists(path))
            {
                return (new StationCapabilityActual(), "missing-fallback");
            }

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<ActualMetricsConfig>(json);
            if (cfg is null)
            {
                return (new StationCapabilityActual(), "invalid-fallback-json");
            }

            return (MapActual(cfg), "fallback-json");
        }
        catch
        {
            return (new StationCapabilityActual(), "fallback-json-error");
        }
    }

    private static bool TryLoadFromMeasurementApi(string apiUrl, out StationCapabilityActual actual, out string source)
    {
        actual = new StationCapabilityActual();
        source = string.Empty;
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return false;
        }

        try
        {
            var response = HttpClient.GetAsync(apiUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var cfg = JsonSerializer.Deserialize<ActualMetricsConfig>(payload);
            if (cfg is null)
            {
                return false;
            }

            actual = MapActual(cfg);
            source = $"tp-api:{apiUrl}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLoadFromTpFile(
        string tpRootPath,
        string configuredRelativePath,
        IReadOnlyList<string> tpConfigFiles,
        out StationCapabilityActual actual,
        out string source)
    {
        actual = new StationCapabilityActual();
        source = string.Empty;

        if (string.IsNullOrWhiteSpace(tpRootPath) || !Directory.Exists(tpRootPath))
        {
            return false;
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRelativePath))
        {
            candidates.Add(Path.IsPathRooted(configuredRelativePath)
                ? configuredRelativePath
                : Path.Combine(tpRootPath, configuredRelativePath));
        }

        foreach (var name in TpMetricFileNames)
        {
            candidates.Add(Path.Combine(tpRootPath, name));
            candidates.Add(Path.Combine(tpRootPath, "config", name));
            candidates.Add(Path.Combine(tpRootPath, "configs", name));
        }

        foreach (var cfgPath in tpConfigFiles)
        {
            var cfgDir = Path.GetDirectoryName(cfgPath);
            if (string.IsNullOrWhiteSpace(cfgDir))
            {
                continue;
            }

            foreach (var name in TpMetricFileNames)
            {
                candidates.Add(Path.Combine(cfgDir, name));
            }
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                var json = File.ReadAllText(candidate);
                var cfg = JsonSerializer.Deserialize<ActualMetricsConfig>(json);
                if (cfg is null)
                {
                    continue;
                }

                actual = MapActual(cfg);
                source = $"tp-file:{candidate}";
                return true;
            }
            catch
            {
                // Try next candidate.
            }
        }

        return false;
    }

    private static string ResolveTpRootPath(DiagnosticRunContext? runContext, TpConnectivityConfig config)
    {
        if (!string.IsNullOrWhiteSpace(runContext?.TpConnectivity?.TpRootPath))
        {
            return runContext.TpConnectivity.TpRootPath;
        }

        return string.IsNullOrWhiteSpace(config.TpRootPath) ? string.Empty : config.TpRootPath;
    }

    private static TpConnectivityConfig LoadTpConnectivityConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, TpConnectivityConfigRelativePath);
            if (!File.Exists(path))
            {
                return new TpConnectivityConfig();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TpConnectivityConfig>(json) ?? new TpConnectivityConfig();
        }
        catch
        {
            return new TpConnectivityConfig();
        }
    }

    private static StationCapabilityActual MapActual(ActualMetricsConfig cfg)
    {
        return new StationCapabilityActual
        {
            GrrPercent = cfg.GrrPercent,
            GdsPercent = cfg.GdsPercent,
            MaxOutputPowerDbm = cfg.MaxOutputPowerDbm,
            SnrDb = cfg.SnrDb,
            SwitchRepeatabilityDb = cfg.SwitchRepeatabilityDb,
            PowerStabilityDb = cfg.PowerStabilityDb,
            ChannelPlanActual = cfg.ChannelPlanActual
        };
    }
}
