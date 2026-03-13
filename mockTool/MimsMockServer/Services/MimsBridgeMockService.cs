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
        sb.Append("<DEFAULT_INFO_API>http://127.0.0.1:7002/api/tms/default-info</DEFAULT_INFO_API>");
        sb.Append("<LUT_DOWNLOAD_API>http://127.0.0.1:7002/api/tms/lut/download/default</LUT_DOWNLOAD_API>");
        sb.Append("<STATION_CAPABILITY_REQUIREMENTS>");
        sb.Append("<GRR_MAX_PERCENT>10.0</GRR_MAX_PERCENT>");
        sb.Append("<GDS_MIN_PERCENT>90.0</GDS_MIN_PERCENT>");
        sb.Append("<MAX_OUTPUT_POWER_MIN_DBM>6.0</MAX_OUTPUT_POWER_MIN_DBM>");
        sb.Append("<SNR_MIN_DB>30.0</SNR_MIN_DB>");
        sb.Append("<SWITCH_REPEATABILITY_MAX_DB>0.5</SWITCH_REPEATABILITY_MAX_DB>");
        sb.Append("<POWER_STABILITY_MAX_DB>0.3</POWER_STABILITY_MAX_DB>");
        sb.Append("<CHANNEL_PLAN_REQUIRED>100G-4CH</CHANNEL_PLAN_REQUIRED>");
        sb.Append("</STATION_CAPABILITY_REQUIREMENTS>");
        sb.Append("<POWER_SUPPLY_REQUIREMENTS>");
        sb.Append("<POWER_TARGET_VOLTAGE_V>12.0</POWER_TARGET_VOLTAGE_V>");
        sb.Append("<POWER_MIN_VOLTAGE_V>11.7</POWER_MIN_VOLTAGE_V>");
        sb.Append("<POWER_MAX_VOLTAGE_V>12.3</POWER_MAX_VOLTAGE_V>");
        sb.Append("<POWER_MAX_STDDEV_V>0.060</POWER_MAX_STDDEV_V>");
        sb.Append("<POWER_MAX_RIPPLE_V>0.220</POWER_MAX_RIPPLE_V>");
        sb.Append("<POWER_SAMPLE_INTERVAL_MS>350</POWER_SAMPLE_INTERVAL_MS>");
        sb.Append("<POWER_SAMPLE_COUNT>12</POWER_SAMPLE_COUNT>");
        sb.Append("<TP_VOLTAGE_API></TP_VOLTAGE_API>");
        sb.Append("</POWER_SUPPLY_REQUIREMENTS>");
        sb.Append("</MIMS_ENV_CONFIG>");
        return sb.ToString();
    }
}
