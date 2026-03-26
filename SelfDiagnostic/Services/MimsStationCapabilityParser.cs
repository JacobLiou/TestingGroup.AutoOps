using System;
using System.Linq;
using System.Xml.Linq;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// MIMS 工站能力要求解析器 — 从 MIMS 配置 XML 中提取 GRR/GDS/光功率/SNR 等基线要求。
    /// </summary>
    public sealed class MimsStationCapabilityParser
    {
        /// <summary>
        /// 解析 XML 为工站能力要求；若为空或解析失败则返回默认要求对象。
        /// </summary>
        public StationCapabilityRequirements ParseOrDefault(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return new StationCapabilityRequirements();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                return new StationCapabilityRequirements
                {
                    GrrMaxPercent = ReadDouble(doc, "GRR_MAX_PERCENT", 10.0),
                    GdsMinPercent = ReadDouble(doc, "GDS_MIN_PERCENT", 90.0),
                    MaxOutputPowerMinDbm = ReadDouble(doc, "MAX_OUTPUT_POWER_MIN_DBM", 6.0),
                    SnrMinDb = ReadDouble(doc, "SNR_MIN_DB", 30.0),
                    SwitchRepeatabilityMaxDb = ReadDouble(doc, "SWITCH_REPEATABILITY_MAX_DB", 0.5),
                    PowerStabilityMaxDb = ReadDouble(doc, "POWER_STABILITY_MAX_DB", 0.3),
                    ChannelPlanRequired = ReadString(doc, "CHANNEL_PLAN_REQUIRED", "100G-4CH")
                };
            }
            catch
            {
                return new StationCapabilityRequirements();
            }
        }

        private static double ReadDouble(XDocument doc, string key, double fallback)
        {
            var value = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
            return double.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static string ReadString(XDocument doc, string key, string fallback)
        {
            var value = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
