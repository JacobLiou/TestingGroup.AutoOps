using System.Diagnostics;
using System.Net.Http;
using System.Text;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class ExternalDependencyHttpChecker
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly IReadOnlyDictionary<string, string> RequestBodies = new Dictionary<string, string>
    {
        [ExternalDependencyIds.Mes] = """{"source":"mockTool","check":"mes"}""",
        [ExternalDependencyIds.Tms] = """{"source":"mockTool","check":"tms"}""",
        [ExternalDependencyIds.Tas] = """{"source":"mockTool","check":"tas_aoi"}""",
        [ExternalDependencyIds.FileServer] = """{"source":"mockTool","check":"file_server"}""",
        [ExternalDependencyIds.Lan] = """{"source":"mockTool","check":"lan"}"""
    };

    public async Task<ExternalDependencyCheckResult> CheckAsync(string dependencyId, ExternalDependencyConfig config, CancellationToken cancellationToken)
    {
        if (!config.Endpoints.TryGetValue(dependencyId, out var endpoint))
        {
            return new ExternalDependencyCheckResult
            {
                Success = false,
                Error = $"未找到依赖配置: {dependencyId}",
                EndpointName = dependencyId
            };
        }

        var body = RequestBodies.TryGetValue(dependencyId, out var value) ? value : "{}";

        try
        {
            var sw = Stopwatch.StartNew();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            sw.Stop();

            return new ExternalDependencyCheckResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ElapsedMs = sw.ElapsedMilliseconds,
                Url = endpoint.Url,
                EndpointName = endpoint.Name,
                Error = response.IsSuccessStatusCode ? string.Empty : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ExternalDependencyCheckResult
            {
                Success = false,
                Error = ex.Message,
                Url = endpoint.Url,
                EndpointName = endpoint.Name
            };
        }
    }
}
