using System.Xml.Linq;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    public sealed class MimsXmlBuilder
    {
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
