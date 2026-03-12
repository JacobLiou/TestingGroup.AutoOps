using Grpc.Core;
using Grpc.Net.Client;
using MockDiagTool.Models;
using MockDiagTool.Protos;
using MockDiagTool.Services.Abstractions;

namespace MockDiagTool.Services;

public sealed class MimsGrpcClient : IExternalSystemClient
{
    private const string DefaultEndpoint = "http://127.0.0.1:50051";
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;
    private readonly MimsXmlBuilder _xmlBuilder;

    public MimsGrpcClient(MimsXmlBuilder xmlBuilder, string? endpoint = null, TimeSpan? timeout = null)
    {
        _xmlBuilder = xmlBuilder;
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<ExternalSendResult> SendAskInfoAsync(MimsAskInfoRequest request, CancellationToken cancellationToken = default)
    {
        var xml = _xmlBuilder.BuildAskInfoXml(request);

        using var channel = GrpcChannel.ForAddress(_endpoint);
        var client = new MimsBridge.MimsBridgeClient(channel);

        var grpcRequest = new XmlEnvelope
        {
            Xml = xml,
            Source = "mockTool",
            MessageType = "MIMS_GUI_ASK_INFO"
        };

        try
        {
            var reply = await client.SubmitXmlAsync(
                grpcRequest,
                deadline: DateTime.UtcNow.Add(_timeout),
                cancellationToken: cancellationToken);

            return new ExternalSendResult
            {
                Success = reply.Success,
                Code = string.IsNullOrWhiteSpace(reply.Code) ? "OK" : reply.Code,
                Message = string.IsNullOrWhiteSpace(reply.Message) ? "MIMS 上报成功" : reply.Message,
                Endpoint = _endpoint
            };
        }
        catch (RpcException ex)
        {
            return new ExternalSendResult
            {
                Success = false,
                Code = ex.StatusCode.ToString(),
                Message = ex.Status.Detail,
                Endpoint = _endpoint
            };
        }
        catch (Exception ex)
        {
            return new ExternalSendResult
            {
                Success = false,
                Code = "UNEXPECTED_ERROR",
                Message = ex.Message,
                Endpoint = _endpoint
            };
        }
    }
}
