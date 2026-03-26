using MockDiagTool.Models;

namespace MockDiagTool.Services.Abstractions;

public interface IExternalSystemClient
{
    Task<ExternalSendResult> SendAskInfoAsync(MimsAskInfoRequest request, CancellationToken cancellationToken = default);
    Task<MimsEnvironmentConfigResult> GetEnvironmentConfigAsync(MimsEnvironmentConfigRequest request, CancellationToken cancellationToken = default);
}
