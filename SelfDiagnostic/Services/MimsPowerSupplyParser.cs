using System;
using System.Linq;
using System.Xml.Linq;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    public sealed class MimsPowerSupplyParser
    {
        public PowerSupplyRequirements ParseOrDefault(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return new PowerSupplyRequirements();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                var sampleInterval = (int)ReadDouble(doc, "POWER_SAMPLE_INTERVAL_MS", 500);
                var sampleCount = (int)ReadDouble(doc, "POWER_SAMPLE_COUNT", 12);
                return new PowerSupplyRequirements
                {
                    TargetVoltageV = ReadDouble(doc, "POWER_TARGET_VOLTAGE_V", 12.0),
                    MinVoltageV = ReadDouble(doc, "POWER_MIN_VOLTAGE_V", 11.4),
                    MaxVoltageV = ReadDouble(doc, "POWER_MAX_VOLTAGE_V", 12.6),
                    MaxStdDevV = ReadDouble(doc, "POWER_MAX_STDDEV_V", 0.06),
                    MaxRippleV = ReadDouble(doc, "POWER_MAX_RIPPLE_V", 0.25),
                    SampleIntervalMs = Math.Max(100, sampleInterval),
                    SampleCount = Math.Max(3, sampleCount),
                    TpVoltageApiUrl = ReadString(doc, "TP_VOLTAGE_API", string.Empty)
                };
            }
            catch
            {
                return new PowerSupplyRequirements();
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
