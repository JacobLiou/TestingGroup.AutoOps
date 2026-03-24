using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    public sealed class PowerSupplyQualityChecker
    {
        private const string TpConnectivityConfigRelativePath = @"config\tpConnectivity.json";
        private const string FallbackPowerMockRelativePath = @"config\powerSupplyMock.json";
        private const string CurveOutputRelativeDir = @"logs\power-curves";
        private static readonly HttpClient HttpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(3) };
        private static readonly Random SharedRandom = new Random();

        private sealed class TpConnectivityConfig
        {
            public string TpRootPath { get; set; } = string.Empty;
            public string VoltageAcquisitionApiUrl { get; set; } = string.Empty;
            public string VoltageDataRelativePath { get; set; } = @"config\voltage_samples.json";
        }

        private sealed class FallbackPowerMock
        {
            public double TargetVoltageV { get; set; } = 12.0;
            public double NoiseAmplitudeV { get; set; } = 0.03;
            public List<double> SamplesV { get; set; } = new List<double>();
        }

        private sealed class VoltageApiResponse
        {
            public double VoltageV { get; set; }
            public double Voltage { get; set; }
        }

        private sealed class VoltageFilePayload
        {
            public List<double> SamplesV { get; set; } = new List<double>();
            public double CurrentVoltageV { get; set; }
            public double CurrentVoltage { get; set; }
        }

        private sealed class CurveArchivePayload
        {
            public DateTime GeneratedAt { get; set; }
            public string Source { get; set; } = string.Empty;
            public double MeanV { get; set; }
            public double StdDevV { get; set; }
            public double MinV { get; set; }
            public double MaxV { get; set; }
            public double RippleV { get; set; }
            public List<PowerVoltageSample> Samples { get; set; } = new List<PowerVoltageSample>();
        }

        public async Task<PowerSupplyQualityResult> CheckAsync(DiagnosticRunContext runContext, CancellationToken ct)
        {
            var requirements = runContext?.PowerSupplyRequirements ?? new PowerSupplyRequirements();
            var tpConfig = LoadTpConnectivityConfig();
            var apiUrl = ResolveApiUrl(requirements, tpConfig);
            var fileSeries = LoadTpVoltageSeries(runContext, tpConfig);
            var fallback = LoadFallbackMock();
            var samples = new List<PowerVoltageSample>();
            var source = "mock-random";

            for (var i = 0; i < requirements.SampleCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var (voltage, sampleSource) = await AcquireVoltageAsync(i, apiUrl, fileSeries, fallback, requirements, ct);
                source = sampleSource;
                samples.Add(new PowerVoltageSample
                {
                    Timestamp = DateTime.Now,
                    VoltageV = voltage
                });

                if (i < requirements.SampleCount - 1)
                {
                    await Task.Delay(requirements.SampleIntervalMs, ct);
                }
            }

            var result = Evaluate(samples, requirements, source);
            var (jsonPath, csvPath) = PersistCurveArtifacts(result);
            return new PowerSupplyQualityResult
            {
                Success = result.Success,
                Source = result.Source,
                Samples = result.Samples,
                CurveJsonPath = jsonPath,
                CurveCsvPath = csvPath,
                MeanV = result.MeanV,
                StdDevV = result.StdDevV,
                MinV = result.MinV,
                MaxV = result.MaxV,
                RippleV = result.RippleV,
                FailReasons = result.FailReasons
            };
        }

        private static async Task<(double Voltage, string Source)> AcquireVoltageAsync(
            int sampleIndex,
            string apiUrl,
            List<double> fileSeries,
            FallbackPowerMock fallback,
            PowerSupplyRequirements requirements,
            CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                try
                {
                    var response = await HttpClient.GetAsync(apiUrl, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var payload = await response.Content.ReadAsStringAsync();
                        var data = JsonConvert.DeserializeObject<VoltageApiResponse>(payload);
                        var apiValue = data?.VoltageV > 0 ? data.VoltageV : data?.Voltage ?? 0;
                        if (apiValue > 0)
                        {
                            return (apiValue, $"tp-api:{apiUrl}");
                        }
                    }
                }
                catch
                {
                    // fallback to file/random
                }
            }

            if (fileSeries.Count > 0)
            {
                var idx = sampleIndex % fileSeries.Count;
                return (fileSeries[idx], "tp-file:voltage-series");
            }

            if (fallback.SamplesV.Count > 0)
            {
                var idx = sampleIndex % fallback.SamplesV.Count;
                return (fallback.SamplesV[idx], "fallback-json:powerSupplyMock");
            }

            var center = fallback.TargetVoltageV > 0 ? fallback.TargetVoltageV : requirements.TargetVoltageV;
            var amp = Math.Max(0.005, fallback.NoiseAmplitudeV);
            var value = center + (SharedRandom.NextDouble() * 2 - 1) * amp;
            return (value, "fallback-random");
        }

        private static PowerSupplyQualityResult Evaluate(
            List<PowerVoltageSample> samples,
            PowerSupplyRequirements req,
            string source)
        {
            if (samples.Count == 0)
            {
                return new PowerSupplyQualityResult
                {
                    Success = false,
                    Source = source,
                    FailReasons = new List<string> { "未采集到电压样本" }
                };
            }

            var values = samples.Select(s => s.VoltageV).ToList();
            var mean = values.Average();
            var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
            var stddev = Math.Sqrt(variance);
            var min = values.Min();
            var max = values.Max();
            var ripple = max - min;

            var reasons = new List<string>();
            if (min < req.MinVoltageV || max > req.MaxVoltageV)
            {
                reasons.Add($"电压越界: 最小{min:F3}V, 最大{max:F3}V, 要求[{req.MinVoltageV:F3}, {req.MaxVoltageV:F3}]V");
            }

            if (stddev > req.MaxStdDevV)
            {
                reasons.Add($"稳定性不足: 标准差{stddev:F4}V > {req.MaxStdDevV:F4}V");
            }

            if (ripple > req.MaxRippleV)
            {
                reasons.Add($"纹波过大: {ripple:F4}V > {req.MaxRippleV:F4}V");
            }

            return new PowerSupplyQualityResult
            {
                Success = reasons.Count == 0,
                Source = source,
                Samples = samples,
                MeanV = mean,
                StdDevV = stddev,
                MinV = min,
                MaxV = max,
                RippleV = ripple,
                FailReasons = reasons
            };
        }

        private static string ResolveApiUrl(PowerSupplyRequirements requirements, TpConnectivityConfig config)
        {
            if (!string.IsNullOrWhiteSpace(requirements.TpVoltageApiUrl))
            {
                return requirements.TpVoltageApiUrl;
            }

            return config.VoltageAcquisitionApiUrl;
        }

        private static List<double> LoadTpVoltageSeries(DiagnosticRunContext runContext, TpConnectivityConfig config)
        {
            var tpRoot = runContext?.TpConnectivity?.TpRootPath;
            if (string.IsNullOrWhiteSpace(tpRoot))
            {
                tpRoot = config.TpRootPath;
            }

            if (string.IsNullOrWhiteSpace(tpRoot) || !Directory.Exists(tpRoot))
            {
                return new List<double>();
            }

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(config.VoltageDataRelativePath))
            {
                var path = Path.IsPathRooted(config.VoltageDataRelativePath)
                    ? config.VoltageDataRelativePath
                    : Path.Combine(tpRoot, config.VoltageDataRelativePath);
                candidates.Add(path);
            }

            candidates.Add(Path.Combine(tpRoot, "config", "voltage_samples.json"));
            candidates.Add(Path.Combine(tpRoot, "voltage_samples.json"));

            foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var json = File.ReadAllText(path);
                    var payload = JsonConvert.DeserializeObject<VoltageFilePayload>(json);
                    if (payload == null)
                    {
                        continue;
                    }

                    if (payload.SamplesV.Count > 0)
                    {
                        return payload.SamplesV.Where(v => v > 0).ToList();
                    }

                    var single = payload.CurrentVoltageV > 0 ? payload.CurrentVoltageV : payload.CurrentVoltage;
                    if (single > 0)
                    {
                        return new List<double> { single };
                    }
                }
                catch
                {
                    // Try next candidate.
                }
            }

            return new List<double>();
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

        private static FallbackPowerMock LoadFallbackMock()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FallbackPowerMockRelativePath);
                if (!File.Exists(path))
                {
                    return new FallbackPowerMock();
                }

                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<FallbackPowerMock>(json) ?? new FallbackPowerMock();
            }
            catch
            {
                return new FallbackPowerMock();
            }
        }

        private static (string JsonPath, string CsvPath) PersistCurveArtifacts(PowerSupplyQualityResult result)
        {
            if (result.Samples.Count == 0)
            {
                return (string.Empty, string.Empty);
            }

            try
            {
                var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CurveOutputRelativeDir);
                Directory.CreateDirectory(outputDir);

                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var baseName = $"power_curve_{stamp}";
                var jsonPath = Path.Combine(outputDir, $"{baseName}.json");
                var csvPath = Path.Combine(outputDir, $"{baseName}.csv");

                var archive = new CurveArchivePayload
                {
                    GeneratedAt = DateTime.Now,
                    Source = result.Source,
                    MeanV = result.MeanV,
                    StdDevV = result.StdDevV,
                    MinV = result.MinV,
                    MaxV = result.MaxV,
                    RippleV = result.RippleV,
                    Samples = result.Samples
                };

                var json = JsonConvert.SerializeObject(archive, Formatting.Indented);
                File.WriteAllText(jsonPath, json, Encoding.UTF8);

                var csvBuilder = new StringBuilder();
                csvBuilder.AppendLine("timestamp,voltageV");
                foreach (var s in result.Samples)
                {
                    csvBuilder.Append(s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    csvBuilder.Append(',');
                    csvBuilder.AppendLine(s.VoltageV.ToString("F6", CultureInfo.InvariantCulture));
                }
                File.WriteAllText(csvPath, csvBuilder.ToString(), Encoding.UTF8);

                return (jsonPath, csvPath);
            }
            catch
            {
                return (string.Empty, string.Empty);
            }
        }
    }
}
