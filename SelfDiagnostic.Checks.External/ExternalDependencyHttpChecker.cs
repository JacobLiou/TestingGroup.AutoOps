using SelfDiagnostic.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 外部依赖 HTTP 探测器 — 对配置的各个端点执行 HTTP POST，验证其可达性与响应状态。
    /// </summary>
    public sealed class ExternalDependencyHttpChecker
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        /// <summary>各依赖项 ID 对应的探测请求体</summary>
        private static readonly Dictionary<string, string> RequestBodies = new Dictionary<string, string>
        {
            [ExternalDependencyIds.Mes] = "{\"source\":\"selfDiagnostic\",\"check\":\"mes\"}",
            [ExternalDependencyIds.Tms] = "{\"source\":\"selfDiagnostic\",\"check\":\"tms\"}",
            [ExternalDependencyIds.Tas] = "{\"source\":\"selfDiagnostic\",\"check\":\"tas_aoi\"}",
            [ExternalDependencyIds.FileServer] = "{\"source\":\"selfDiagnostic\",\"check\":\"file_server\"}",
            [ExternalDependencyIds.Lan] = "{\"source\":\"selfDiagnostic\",\"check\":\"lan\"}"
        };

        /// <summary>
        /// 对指定依赖项执行 HTTP POST 探测，返回探测结果（含状态码、耗时等）。
        /// </summary>
        public async Task<ExternalDependencyCheckResult> CheckAsync(
            string dependencyId,
            ExternalDependencyConfig config,
            CancellationToken cancellationToken)
        {
            ExternalDependencyEndpoint endpoint;
            if (!config.Endpoints.TryGetValue(dependencyId, out endpoint))
            {
                return new ExternalDependencyCheckResult
                {
                    Success = false,
                    Error = "未找到依赖配置: " + dependencyId,
                    EndpointName = dependencyId
                };
            }

            string body;
            if (!RequestBodies.TryGetValue(dependencyId, out body))
            {
                body = "{}";
            }

            try
            {
                var sw = Stopwatch.StartNew();
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var response = await HttpClient.SendAsync(request, cancellationToken))
                    {
                        sw.Stop();
                        return new ExternalDependencyCheckResult
                        {
                            Success = response.IsSuccessStatusCode,
                            StatusCode = (int)response.StatusCode,
                            ElapsedMs = sw.ElapsedMilliseconds,
                            Url = endpoint.Url,
                            EndpointName = endpoint.Name,
                            Error = response.IsSuccessStatusCode ? string.Empty : "HTTP " + (int)response.StatusCode
                        };
                    }
                }
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
}