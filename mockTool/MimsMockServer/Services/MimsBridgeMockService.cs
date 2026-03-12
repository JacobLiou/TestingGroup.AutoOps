using System.Text;
using Grpc.Core;
using MockDiagTool.Protos;

namespace MimsMockServer.Services;

public sealed class MimsBridgeMockService : MimsBridge.MimsBridgeBase
{
    public override Task<SubmitReply> SubmitXml(XmlEnvelope request, ServerCallContext context)
    {
        var reply = new SubmitReply
        {
            Success = true,
            Code = "MOCK_OK",
            Message = $"Mock MIMS 已接收 XML，source={request.Source}, type={request.MessageType}, length={request.Xml?.Length ?? 0}",
            ReceivedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        return Task.FromResult(reply);
    }

    public override Task<EnvironmentConfigReply> GetEnvironmentConfig(EnvironmentConfigRequest request, ServerCallContext context)
    {
        var xml = BuildMockConfigXml(request.StationId, request.LineId);
        var reply = new EnvironmentConfigReply
        {
            Success = true,
            Code = "MOCK_OK",
            Message = "Mock MIMS 配置返回成功",
            ConfigXml = xml
        };
        return Task.FromResult(reply);
    }

    private static string BuildMockConfigXml(string stationId, string lineId)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<MIMS_ENV_CONFIG>");
        sb.Append($"<BASE_INFO STATION_ID=\"{stationId}\" LINE_ID=\"{lineId}\"/>");
        sb.Append("<MES_API>http://127.0.0.1:7001/api/mes/health</MES_API>");
        sb.Append("<TMS_API>http://127.0.0.1:7002/api/tms/health</TMS_API>");
        sb.Append("<TAS_AOI_API>http://127.0.0.1:7003/api/tas/aoi/health</TAS_AOI_API>");
        sb.Append("<FILE_SERVER_API>http://127.0.0.1:7004/api/fileserver/health</FILE_SERVER_API>");
        sb.Append("<LAN_API>http://127.0.0.1:7005/api/lan/health</LAN_API>");
        sb.Append("</MIMS_ENV_CONFIG>");
        return sb.ToString();
    }
}
