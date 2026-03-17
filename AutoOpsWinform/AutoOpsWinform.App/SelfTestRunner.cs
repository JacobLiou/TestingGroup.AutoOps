using MockDiagTool.Models;
using MockDiagTool.Services;
using MockDiagTool.Services.Abstractions;

namespace AutoOpsWinform.App;

internal sealed class SelfTestRunner
{
    private readonly IExternalSystemClient _externalSystemClient = new MimsGrpcClient(new MimsXmlBuilder());
    private readonly MimsConfigXmlParser _mimsConfigXmlParser = new();
    private readonly MimsStationCapabilityParser _mimsStationCapabilityParser = new();
    private readonly MimsPowerSupplyParser _mimsPowerSupplyParser = new();
    private readonly TpConnectivityInspector _tpConnectivityInspector = new();

    public async Task<int> RunAsync()
    {
        Console.WriteLine("Selftest start...");
        try
        {
            var runbook = DiagnosticEngine.LoadRunbook();
            var context = await BuildRunContextAsync(CancellationToken.None);
            var byStepId = runbook.Steps.Where(s => s.Enabled).ToDictionary(s => s.StepId, s => s, StringComparer.OrdinalIgnoreCase);
            var items = DiagnosticEngine.BuildCheckList(runbook);
            var current = runbook.Steps.FirstOrDefault(s => s.Enabled);
            var scanned = 0;
            var guard = Math.Max(1, byStepId.Count * 4);
            while (current != null && scanned < guard)
            {
                var item = items.FirstOrDefault(x => x.Id.Equals(current.CheckId, StringComparison.OrdinalIgnoreCase));
                if (item is null)
                {
                    break;
                }

                var outcome = await DiagnosticEngine.RunCheckAsync(item, current, context, CancellationToken.None);
                Console.WriteLine($"[{item.Id}] {item.Status} | {item.Detail}");
                scanned++;
                var nextId = outcome.Success ? current.NextOnSuccess : current.NextOnFailure;
                current = string.IsNullOrWhiteSpace(nextId) || !byStepId.TryGetValue(nextId, out var next) ? null : next;
            }

            var done = items.Count(i => i.Status is not (CheckStatus.Pending or CheckStatus.Scanning));
            var pass = items.Count(i => i.Status is CheckStatus.Pass or CheckStatus.Fixed);
            var warning = items.Count(i => i.Status == CheckStatus.Warning);
            var fail = items.Count(i => i.Status == CheckStatus.Fail);
            Console.WriteLine($"Selftest end. scanned={done}, pass={pass}, warning={warning}, fail={fail}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Selftest failed: {ex}");
            return 1;
        }
    }

    private async Task<DiagnosticRunContext> BuildRunContextAsync(CancellationToken cancellationToken)
    {
        var configRequest = new MimsEnvironmentConfigRequest { StationId = "STATION-001", LineId = "LINE-001" };
        var configResult = await _externalSystemClient.GetEnvironmentConfigAsync(configRequest, cancellationToken);
        var tpSnapshot = await _tpConnectivityInspector.InspectAsync(cancellationToken);
        if (!configResult.Success)
        {
            return new DiagnosticRunContext
            {
                ExternalChecksEnabled = false,
                ConfigError = $"{configResult.Code} - {configResult.Message}",
                TpConnectivity = tpSnapshot
            };
        }

        return new DiagnosticRunContext
        {
            ExternalChecksEnabled = true,
            ExternalConfig = _mimsConfigXmlParser.ParseOrDefault(configResult.ConfigXml),
            ConfigSource = $"MIMS({configResult.Endpoint})",
            TpConnectivity = tpSnapshot,
            StationCapabilityRequirements = _mimsStationCapabilityParser.ParseOrDefault(configResult.ConfigXml),
            PowerSupplyRequirements = _mimsPowerSupplyParser.ParseOrDefault(configResult.ConfigXml),
            RawMimsConfigXml = configResult.ConfigXml
        };
    }
}
