using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services.Abstractions
{
    /// <summary>
    /// 外部系统通信客户端接口（主项目副本）。
    /// </summary>
    public interface IExternalSystemClient
    {
        /// <summary>
        /// 向外部系统异步发送 AskInfo 请求。
        /// </summary>
        Task<ExternalSendResult> SendAskInfoAsync(MimsAskInfoRequest request, CancellationToken cancellationToken = default);
        /// <summary>
        /// 异步获取 MIMS 环境配置。
        /// </summary>
        Task<MimsEnvironmentConfigResult> GetEnvironmentConfigAsync(MimsEnvironmentConfigRequest request, CancellationToken cancellationToken = default);
    }
}
