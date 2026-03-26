using SelfDiagnostic.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services.Abstractions
{
    /// <summary>
    /// 外部系统通信客户端接口（当前实现为 MIMS gRPC 客户端的 Stub）。
    /// 负责向 MIMS 发送 AskInfo 报告及获取环境配置。
    /// </summary>
    public interface IExternalSystemClient
    {
        /// <summary>向 MIMS 发送诊断 AskInfo 报告</summary>
        Task<ExternalSendResult> SendAskInfoAsync(MimsAskInfoRequest request, CancellationToken cancellationToken = default);

        /// <summary>从 MIMS 获取环境配置（含工站基线、电源要求等 XML 配置）</summary>
        Task<MimsEnvironmentConfigResult> GetEnvironmentConfigAsync(MimsEnvironmentConfigRequest request, CancellationToken cancellationToken = default);
    }
}