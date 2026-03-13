using System.IO;
using System.Net.Http;
using System.Text.Json;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class PowerSupplyQualityChecker
{
    private const string TpConnectivityConfigRelativePath = @"config\tpConnectivity.json";
    private const string FallbackPowerMockRelativePath = @"config\powerSupplyMock.json";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

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
        public List<double> SamplesV { get; set; } = [];
    }

    private sealed class VoltageApiResponse
    {
        public double VoltageV { get; set; }
        public double Voltage { get; set; }
    }

    private sealed class VoltageFilePayload
    {
        public List<double> SamplesV { get; set; } = [];
        public double CurrentVoltageV { get; set; }
        public double CurrentVoltage { get; set; }
    }

    public async Task<PowerSupplyQualityResult> CheckAsync(DiagnosticRunContext? runContext, CancellationToken ct)
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

            // Timer tick simulates "MIMS dispatch -> TP acquisition" handshake.
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

        return Evaluate(samples, requirements, source);
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
                    var payload = await response.Content.ReadAsStringAsync(ct);
                    var data = JsonSerializer.Deserialize<VoltageApiResponse>(payload);
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
        var value = center + (Random.Shared.NextDouble() * 2 - 1) * amp;
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
                FailReasons = ["未采集到电压样本"]
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

    private static List<double> LoadTpVoltageSeries(DiagnosticRunContext? runContext, TpConnectivityConfig config)
    {
        var tpRoot = runContext?.TpConnectivity?.TpRootPath;
        if (string.IsNullOrWhiteSpace(tpRoot))
        {
            tpRoot = config.TpRootPath;
        }

        if (string.IsNullOrWhiteSpace(tpRoot) || !Directory.Exists(tpRoot))
        {
            return [];
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
                var payload = JsonSerializer.Deserialize<VoltageFilePayload>(json);
                if (payload is null)
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
                    return [single];
                }
            }
            catch
            {
                // Try next candidate.
            }
        }

        return [];
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

    private static FallbackPowerMock LoadFallbackMock()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, FallbackPowerMockRelativePath);
            if (!File.Exists(path))
            {
                return new FallbackPowerMock();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FallbackPowerMock>(json) ?? new FallbackPowerMock();
        }
        catch
        {
            return new FallbackPowerMock();
        }
    }
}
