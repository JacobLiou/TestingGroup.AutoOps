using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services.Abstractions
{
    public interface IExternalSystemClient
    {
        Task<ExternalSendResult> SendAskInfoAsync(MimsAskInfoRequest request, CancellationToken cancellationToken = default);
        Task<MimsEnvironmentConfigResult> GetEnvironmentConfigAsync(MimsEnvironmentConfigRequest request, CancellationToken cancellationToken = default);
    }
}
