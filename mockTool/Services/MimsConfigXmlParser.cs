using System.Xml.Linq;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class MimsConfigXmlParser
{
    public ExternalDependencyConfig ParseOrDefault(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return BuildDefaultConfig();
        }

        try
        {
            var doc = XDocument.Parse(xml);
            var config = new ExternalDependencyConfig
            {
                Endpoints = new Dictionary<string, ExternalDependencyEndpoint>
                {
                    [ExternalDependencyIds.Mes] = new()
                    {
                        Id = ExternalDependencyIds.Mes,
                        Name = "MES API",
                        Url = ResolveValue(doc, "MES_API", "MES", "MES_URL") ?? "http://127.0.0.1:7001/api/mes/health"
                    },
                    [ExternalDependencyIds.Tms] = new()
                    {
                        Id = ExternalDependencyIds.Tms,
                        Name = "TMS API",
                        Url = ResolveValue(doc, "TMS_API", "TMS", "TMS_URL") ?? "http://127.0.0.1:7002/api/tms/health"
                    },
                    [ExternalDependencyIds.Tas] = new()
                    {
                        Id = ExternalDependencyIds.Tas,
                        Name = "TAS AOI API",
                        Url = ResolveValue(doc, "TAS_AOI_API", "TAS_API", "TAS", "AOI_URL") ?? "http://127.0.0.1:7003/api/tas/aoi/health"
                    },
                    [ExternalDependencyIds.FileServer] = new()
                    {
                        Id = ExternalDependencyIds.FileServer,
                        Name = "文件服务器 API",
                        Url = ResolveValue(doc, "FILE_SERVER_API", "FILE_SERVER", "FILESERVER_URL") ?? "http://127.0.0.1:7004/api/fileserver/health"
                    },
                    [ExternalDependencyIds.Lan] = new()
                    {
                        Id = ExternalDependencyIds.Lan,
                        Name = "局域网网关 API",
                        Url = ResolveValue(doc, "LAN_API", "LAN", "LAN_URL") ?? "http://127.0.0.1:7005/api/lan/health"
                    }
                }
            };

            return config;
        }
        catch
        {
            return BuildDefaultConfig();
        }
    }

    private static string? ResolveValue(XDocument doc, params string[] keys)
    {
        foreach (var key in keys)
        {
            var element = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (element != null && !string.IsNullOrWhiteSpace(element.Value))
            {
                return element.Value.Trim();
            }

            var attr = doc.Descendants().Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
            {
                return attr.Value.Trim();
            }
        }
        return null;
    }

    private static ExternalDependencyConfig BuildDefaultConfig()
    {
        return new ExternalDependencyConfig
        {
            Endpoints = new Dictionary<string, ExternalDependencyEndpoint>
            {
                [ExternalDependencyIds.Mes] = new()
                {
                    Id = ExternalDependencyIds.Mes,
                    Name = "MES API",
                    Url = "http://127.0.0.1:7001/api/mes/health"
                },
                [ExternalDependencyIds.Tms] = new()
                {
                    Id = ExternalDependencyIds.Tms,
                    Name = "TMS API",
                    Url = "http://127.0.0.1:7002/api/tms/health"
                },
                [ExternalDependencyIds.Tas] = new()
                {
                    Id = ExternalDependencyIds.Tas,
                    Name = "TAS AOI API",
                    Url = "http://127.0.0.1:7003/api/tas/aoi/health"
                },
                [ExternalDependencyIds.FileServer] = new()
                {
                    Id = ExternalDependencyIds.FileServer,
                    Name = "文件服务器 API",
                    Url = "http://127.0.0.1:7004/api/fileserver/health"
                },
                [ExternalDependencyIds.Lan] = new()
                {
                    Id = ExternalDependencyIds.Lan,
                    Name = "局域网网关 API",
                    Url = "http://127.0.0.1:7005/api/lan/health"
                }
            }
        };
    }
}
