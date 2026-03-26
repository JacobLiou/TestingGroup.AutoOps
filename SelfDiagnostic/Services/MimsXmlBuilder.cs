using System.Xml.Linq;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// MIMS XML 构建器 — 构建发送给 MIMS 的请求 XML。
    /// </summary>
    public sealed class MimsXmlBuilder
    {
        /// <summary>
        /// 根据请求信息构建 MIMS_GUI_ASK_INFO 格式的 XML 字符串。
        /// </summary>
        public string BuildAskInfoXml(MimsAskInfoRequest request)
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("MIMS_GUI_ASK_INFO",
                    new XElement("BASE_INFO",
                        new XAttribute("Version", "A1"),
                        new XElement("INFO",
                            new XAttribute("AUTHOR", request.Author),
                            new XAttribute("SPEC", request.Spec),
                            new XAttribute("PN", request.PartNumber),
                            new XAttribute("DATE", request.Date.ToString("yyyyMMdd"))
                        )
                    )
                )
            );

            return doc.ToString(SaveOptions.DisableFormatting);
        }
    }
}
