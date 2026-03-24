using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    public sealed class StationCapabilityComplianceChecker
    {
        private const string ActualMetricsConfigRelativePath = @"config\stationActualMetrics.json";
        private const string TpConnectivityConfigRelativePath = @"config\tpConnectivity.json";
        private static readonly HttpClient HttpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(4) };
        private static readonly string[] TpMetricFileNames = new string[]
        {
            "stationActualMetrics.json",
            "station_metrics.json",
            "tp_station_metrics.json",
            "station_capability.json",
            "capability_metrics.json"
        };

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

        public StationCapabilityComplianceResult Check(DiagnosticRunContext runContext)
        {
            return CheckCore(null, runContext);
        }

        public StationCapabilityComplianceResult Check(RunbookStepDefinition step, DiagnosticRunContext runContext)
        {
            return CheckCore(step, runContext);
        }

        private StationCapabilityComplianceResult CheckCore(RunbookStepDefinition step, DiagnosticRunContext runContext)
        {
            var req = runContext?.StationCapabilityRequirements;
            if (req == null)
            {
                return new StationCapabilityComplianceResult
                {
                    Success = false,
                    Metrics = new List<StationCapabilityMetricResult>
                    {
                        new StationCapabilityMetricResult()
                        {
                            Metric = "StationCapability",
                            Required = "MIMS requirement",
                            Actual = "Unavailable",
                            Pass = false
                        }
                    },
                    FailReasons = new List<string> { "工位能力要求不可用" }
                };
            }

            var (actual, source) = LoadActual(runContext);
            if (step != null && IsCustomRule(step))
            {
                return EvaluateCustomRule(step, req, actual, source);
            }

            var metrics = new List<StationCapabilityMetricResult>
            {
                new StationCapabilityMetricResult()
                {
                    Metric = "GR&R",
                    Required = $"<= {req.GrrMaxPercent:F2}%",
                    Actual = $"{actual.GrrPercent:F2}%",
                    Pass = actual.GrrPercent <= req.GrrMaxPercent
                },
                new StationCapabilityMetricResult()
                {
                    Metric = "GDS",
                    Required = $">= {req.GdsMinPercent:F2}%",
                    Actual = $"{actual.GdsPercent:F2}%",
                    Pass = actual.GdsPercent >= req.GdsMinPercent
                },
                new StationCapabilityMetricResult()
                {
                    Metric = "最大出光功率",
                    Required = $">= {req.MaxOutputPowerMinDbm:F2} dBm",
                    Actual = $"{actual.MaxOutputPowerDbm:F2} dBm",
                    Pass = actual.MaxOutputPowerDbm >= req.MaxOutputPowerMinDbm
                },
                new StationCapabilityMetricResult()
                {
                    Metric = "信噪比",
                    Required = $">= {req.SnrMinDb:F2} dB",
                    Actual = $"{actual.SnrDb:F2} dB",
                    Pass = actual.SnrDb >= req.SnrMinDb
                },
                new StationCapabilityMetricResult()
                {
                    Metric = "开关重复性",
                    Required = $"<= {req.SwitchRepeatabilityMaxDb:F2} dB",
                    Actual = $"{actual.SwitchRepeatabilityDb:F2} dB",
                    Pass = actual.SwitchRepeatabilityDb <= req.SwitchRepeatabilityMaxDb
                },
                new StationCapabilityMetricResult()
                {
                    Metric = "功率稳定性",
                    Required = $"<= {req.PowerStabilityMaxDb:F2} dB",
                    Actual = $"{actual.PowerStabilityDb:F2} dB",
                    Pass = actual.PowerStabilityDb <= req.PowerStabilityMaxDb
                },
                new StationCapabilityMetricResult()
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
                Metrics = metrics,
                FailReasons = metrics.Where(m => !m.Pass)
                    .Select(m => $"{m.Metric} 实际{m.Actual} / 要求{m.Required}")
                    .ToList()
            };
        }

        private static bool IsCustomRule(RunbookStepDefinition step)
        {
            return step.Params.ContainsKey("metricKey");
        }

        private static StationCapabilityComplianceResult EvaluateCustomRule(
            RunbookStepDefinition step,
            StationCapabilityRequirements req,
            StationCapabilityActual actual,
            string source)
        {
            var metricKey = GetParam(step, "metricKey");
            if (string.IsNullOrWhiteSpace(metricKey))
            {
                return new StationCapabilityComplianceResult
                {
                    Success = false,
                    ActualSource = source,
                    Metrics = new List<StationCapabilityMetricResult>
                    {
                        new StationCapabilityMetricResult()
                        {
                            Metric = "CustomOpticalRule",
                            Required = "metricKey required",
                            Actual = "missing",
                            Pass = false
                        }
                    },
                    FailReasons = new List<string> { "缺少 metricKey 参数" }
                };
            }

            if (!TryResolveNumericMetric(metricKey, req, actual, out var metricName, out var actualValue, out var defaultOperator, out var defaultThreshold))
            {
                return new StationCapabilityComplianceResult
                {
                    Success = false,
                    ActualSource = source,
                    Metrics = new List<StationCapabilityMetricResult>
                    {
                        new StationCapabilityMetricResult()
                        {
                            Metric = string.IsNullOrWhiteSpace(GetParam(step, "ruleName")) ? metricKey : GetParam(step, "ruleName"),
                            Required = "supported metricKey",
                            Actual = metricKey,
                            Pass = false
                        }
                    },
                    FailReasons = new List<string> { $"不支持的 metricKey: {metricKey}" }
                };
            }

            var @operator = NormalizeOperator(GetParam(step, "operator"), defaultOperator);
            var required = BuildRequiredLabel(step, @operator, defaultThreshold, metricName);
            var pass = Evaluate(@operator, actualValue, step, defaultThreshold);
            var actualLabel = FormatMetric(metricKey, actualValue);
            var displayName = string.IsNullOrWhiteSpace(GetParam(step, "ruleName")) ? metricName : GetParam(step, "ruleName");
            var metric = new StationCapabilityMetricResult
            {
                Metric = displayName,
                Required = required,
                Actual = actualLabel,
                Pass = pass
            };

            var failReasons = new List<string>();
            if (!pass)
            {
                var template = GetParam(step, "failReasonTemplate");
                if (string.IsNullOrWhiteSpace(template))
                {
                    failReasons.Add($"{displayName} 实际{actualLabel} / 规则{required}");
                }
                else
                {
                    failReasons.Add(template
                        .Replace("{metric}", displayName)
                        .Replace("{actual}", actualLabel)
                        .Replace("{required}", required));
                }
            }

            return new StationCapabilityComplianceResult
            {
                Success = pass,
                ActualSource = source,
                Metrics = new List<StationCapabilityMetricResult> { metric },
                FailReasons = failReasons
            };
        }

        private static string GetParam(RunbookStepDefinition step, string key)
        {
            return step.Params.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private static bool TryResolveNumericMetric(
            string metricKey,
            StationCapabilityRequirements req,
            StationCapabilityActual actual,
            out string metricName,
            out double actualValue,
            out string defaultOperator,
            out double defaultThreshold)
        {
            metricName = metricKey;
            actualValue = 0;
            defaultOperator = "lte";
            defaultThreshold = 0;
            switch (metricKey.Trim().ToLowerInvariant())
            {
                case "grr":
                case "grrpercent":
                    metricName = "GR&R";
                    actualValue = actual.GrrPercent;
                    defaultOperator = "lte";
                    defaultThreshold = req.GrrMaxPercent;
                    return true;
                case "gds":
                case "gdspercent":
                    metricName = "GDS";
                    actualValue = actual.GdsPercent;
                    defaultOperator = "gte";
                    defaultThreshold = req.GdsMinPercent;
                    return true;
                case "maxoutputpower":
                case "maxoutputpowerdbm":
                    metricName = "最大出光功率";
                    actualValue = actual.MaxOutputPowerDbm;
                    defaultOperator = "gte";
                    defaultThreshold = req.MaxOutputPowerMinDbm;
                    return true;
                case "snr":
                case "snrdb":
                    metricName = "信噪比";
                    actualValue = actual.SnrDb;
                    defaultOperator = "gte";
                    defaultThreshold = req.SnrMinDb;
                    return true;
                case "switchrepeatability":
                case "switchrepeatabilitydb":
                    metricName = "开关重复性";
                    actualValue = actual.SwitchRepeatabilityDb;
                    defaultOperator = "lte";
                    defaultThreshold = req.SwitchRepeatabilityMaxDb;
                    return true;
                case "powerstability":
                case "powerstabilitydb":
                    metricName = "功率稳定性";
                    actualValue = actual.PowerStabilityDb;
                    defaultOperator = "lte";
                    defaultThreshold = req.PowerStabilityMaxDb;
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeOperator(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            return raw.Trim().ToLowerInvariant() switch
            {
                "gt" => "gt",
                "gte" => "gte",
                "lt" => "lt",
                "lte" => "lte",
                "range" => "range",
                _ => fallback
            };
        }

        private static string BuildRequiredLabel(RunbookStepDefinition step, string @operator, double defaultThreshold, string metricName)
        {
            if (@operator == "range")
            {
                if (!TryGetDoubleParam(step, "min", out var min))
                {
                    min = defaultThreshold;
                }

                if (!TryGetDoubleParam(step, "max", out var max))
                {
                    max = defaultThreshold;
                }

                return $"{metricName} in [{min:F2}, {max:F2}]";
            }

            if (!TryGetDoubleParam(step, "threshold", out var threshold))
            {
                threshold = defaultThreshold;
            }

            return @operator switch
            {
                "gt" => $"> {threshold:F2}",
                "gte" => $">= {threshold:F2}",
                "lt" => $"< {threshold:F2}",
                _ => $"<= {threshold:F2}"
            };
        }

        private static bool Evaluate(string @operator, double actual, RunbookStepDefinition step, double defaultThreshold)
        {
            if (@operator == "range")
            {
                if (!TryGetDoubleParam(step, "min", out var min))
                {
                    min = defaultThreshold;
                }

                if (!TryGetDoubleParam(step, "max", out var max))
                {
                    max = defaultThreshold;
                }

                if (min > max)
                {
                    (min, max) = (max, min);
                }

                return actual >= min && actual <= max;
            }

            if (!TryGetDoubleParam(step, "threshold", out var threshold))
            {
                threshold = defaultThreshold;
            }

            return @operator switch
            {
                "gt" => actual > threshold,
                "gte" => actual >= threshold,
                "lt" => actual < threshold,
                _ => actual <= threshold
            };
        }

        private static bool TryGetDoubleParam(RunbookStepDefinition step, string key, out double value)
        {
            value = 0;
            if (!step.Params.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value) ||
                   double.TryParse(raw, out value);
        }

        private static string FormatMetric(string metricKey, double value)
        {
            return metricKey.Trim().ToLowerInvariant() switch
            {
                "grr" => $"{value:F2}%",
                "grrpercent" => $"{value:F2}%",
                "gds" => $"{value:F2}%",
                "gdspercent" => $"{value:F2}%",
                "maxoutputpower" => $"{value:F2} dBm",
                "maxoutputpowerdbm" => $"{value:F2} dBm",
                "snr" => $"{value:F2} dB",
                "snrdb" => $"{value:F2} dB",
                "switchrepeatability" => $"{value:F2} dB",
                "switchrepeatabilitydb" => $"{value:F2} dB",
                "powerstability" => $"{value:F2} dB",
                "powerstabilitydb" => $"{value:F2} dB",
                _ => value.ToString("F2")
            };
        }

        private static (StationCapabilityActual Actual, string Source) LoadActual(DiagnosticRunContext runContext)
        {
            var tpConfig = LoadTpConnectivityConfig();
            var tpRootPath = ResolveTpRootPath(runContext, tpConfig);

            if (TryLoadFromMeasurementApi(tpConfig.StationMetricsApiUrl, out var apiActual, out var apiSource))
            {
                return (apiActual, apiSource);
            }

            if (TryLoadFromTpFile(tpRootPath, tpConfig.StationMetricsRelativePath, runContext?.TpConnectivity?.ConfigFiles ?? new List<string>(), out var tpActual, out var tpSource))
            {
                return (tpActual, tpSource);
            }

            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ActualMetricsConfigRelativePath);
                if (!File.Exists(path))
                {
                    return (new StationCapabilityActual(), "missing-fallback");
                }

                var json = File.ReadAllText(path);
                var cfg = JsonConvert.DeserializeObject<ActualMetricsConfig>(json);
                if (cfg == null)
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
                var cfg = JsonConvert.DeserializeObject<ActualMetricsConfig>(payload);
                if (cfg == null)
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
                    var cfg = JsonConvert.DeserializeObject<ActualMetricsConfig>(json);
                    if (cfg == null)
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

        private static string ResolveTpRootPath(DiagnosticRunContext runContext, TpConnectivityConfig config)
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
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TpConnectivityConfigRelativePath);
                if (!File.Exists(path))
                {
                    return new TpConnectivityConfig();
                }

                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<TpConnectivityConfig>(json) ?? new TpConnectivityConfig();
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
}
