using System;
using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// MIMS gRPC 客户端（当前为 Stub 实现）— 模拟与 MIMS 的通信，返回预设的环境配置。
    /// </summary>
    public sealed class MimsGrpcClient : IExternalSystemClient
    {
        private const string DefaultEndpoint = "http://127.0.0.1:50051";
        private readonly string _endpoint;
        private readonly MimsXmlBuilder _xmlBuilder;

        /// <summary>
        /// 使用 XML 构建器与可选的 MIMS 端点地址初始化客户端。
        /// </summary>
        public MimsGrpcClient(MimsXmlBuilder xmlBuilder, string endpoint = null)
        {
            _xmlBuilder = xmlBuilder;
            _endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;
        }

        /// <summary>
        /// 异步发送 MIMS 询问信息请求（存根：未接入真实服务时返回失败结果）。
        /// </summary>
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

        /// <summary>
        /// 异步获取 MIMS 环境配置（存根：返回空配置 XML 与失败状态）。
        /// </summary>
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
