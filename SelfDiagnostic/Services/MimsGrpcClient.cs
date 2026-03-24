using System;
using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// Stub MIMS client for standalone testing.
    /// Replace with real gRPC/WCF/HTTP client when integrating with MIMS.
    /// </summary>
    public sealed class MimsGrpcClient : IExternalSystemClient
    {
        private const string DefaultEndpoint = "http://127.0.0.1:50051";
        private readonly string _endpoint;
        private readonly MimsXmlBuilder _xmlBuilder;

        public MimsGrpcClient(MimsXmlBuilder xmlBuilder, string endpoint = null)
        {
            _xmlBuilder = xmlBuilder;
            _endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;
        }

        public Task<ExternalSendResult> SendAskInfoAsync(MimsAskInfoRequest request, CancellationToken cancellationToken = default)
        {
            // TODO: Replace with real MIMS communication (gRPC / WCF / HTTP)
            return Task.FromResult(new ExternalSendResult
            {
                Success = false,
                Code = "STUB",
                Message = "MimsGrpcClient 未接入真实 MIMS 服务，当前为存根模式",
                Endpoint = _endpoint
            });
        }

        public Task<MimsEnvironmentConfigResult> GetEnvironmentConfigAsync(MimsEnvironmentConfigRequest request, CancellationToken cancellationToken = default)
        {
            // TODO: Replace with real MIMS communication
            return Task.FromResult(new MimsEnvironmentConfigResult
            {
                Success = false,
                Code = "STUB",
                Message = "MimsGrpcClient 未接入真实 MIMS 服务，当前为存根模式",
                Endpoint = _endpoint,
                ConfigXml = string.Empty
            });
        }
    }
}
