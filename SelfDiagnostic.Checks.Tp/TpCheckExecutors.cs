using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services.Executors.Tp
{
    /// <summary>
    /// TP（测试程序）检查执行器集合 — 涵盖路径配置、串口、网络端点、版本合规、工站能力、电源质量、LUT、硬件状态分组、光学残留风险等检查。
    /// </summary>
    internal static class TpCheckExecutors
    {
        private static readonly DeviceVersionComplianceChecker VersionComplianceChecker = new DeviceVersionComplianceChecker();
        private static readonly StationCapabilityComplianceChecker StationCapabilityComplianceChecker = new StationCapabilityComplianceChecker();
        private static readonly PowerSupplyQualityChecker PowerSupplyQualityChecker = new PowerSupplyQualityChecker();
        private static readonly DefaultInfoAndLutChecker DefaultInfoAndLutChecker = new DefaultInfoAndLutChecker();
        private static readonly HwSwFwConfigIntegrityChecker HwSwFwConfigIntegrityChecker = new HwSwFwConfigIntegrityChecker();
        private static readonly HwStatusGroupedChecker HwStatusGroupedChecker = new HwStatusGroupedChecker();
        private static readonly OpticalPathResidualRiskChecker OpticalPathResidualRiskChecker = new OpticalPathResidualRiskChecker();

        [CheckExecutor(TpCheckIds.PathAndConfig, DisplayName = "TP Path & Config", Description = "Verify TP root path exists and config files are readable", DefaultCategory = "StationCheck")]
        private static Task<CheckExecutionOutcome> CheckTpPathAndConfigAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.PathAndConfig, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.SerialPorts, DisplayName = "TP Serial Port Mapping", Description = "Verify serial port mapping from TP config matches system COM ports", DefaultCategory = "HwStatusCheck")]
        private static Task<CheckExecutionOutcome> CheckTpSerialPortsAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.SerialPorts, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.NetworkEndpoints, DisplayName = "TP Network Endpoints", Description = "Verify all TP network endpoints are reachable", DefaultCategory = "StationCheck")]
        private static Task<CheckExecutionOutcome> CheckTpNetworkEndpointsAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.NetworkEndpoints, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.VersionCompliance, DisplayName = "HW/FW Version Compliance", Description = "Compare device HW/FW versions against TMS requirements", DefaultCategory = "HwSwFwCheck")]
        private static Task<CheckExecutionOutcome> CheckTpVersionComplianceAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.VersionCompliance, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.StationCapabilityCompliance, DisplayName = "Station Capability (GRR/GDS)", Description = "Validate station GRR/GDS/optical performance against MIMS requirements", DefaultCategory = "OpticalPerformanceCheck")]
        private static Task<CheckExecutionOutcome> CheckTpStationCapabilityComplianceAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.StationCapabilityCompliance, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.PowerSupplyQuality, DisplayName = "Power Supply Quality", Description = "Sample power supply voltage and check mean/stddev/ripple", DefaultCategory = "HwStatusCheck")]
        private static Task<CheckExecutionOutcome> CheckTpPowerSupplyQualityAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.PowerSupplyQuality, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.DefaultInfoAndLut, DisplayName = "Default Info & LUT Integrity", Description = "Verify default info and LUT table completeness via MIMS", DefaultCategory = "HwSwFwCheck")]
        private static Task<CheckExecutionOutcome> CheckTpDefaultInfoAndLutAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.DefaultInfoAndLut, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.HwSwFwConfigIntegrity, DisplayName = "Config/Corruption Integrity", Description = "Check HW/SW/FW configuration data integrity for corruption", DefaultCategory = "HwSwFwCheck")]
        private static Task<CheckExecutionOutcome> CheckTpHwSwFwConfigIntegrityAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.HwSwFwConfigIntegrity, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.HwStatusOpticalGroup, DisplayName = "HW Status - Optical Group", Description = "Check optical link devices (PD/VOA/SW/Pump/DFB/TEC/Heater)", DefaultCategory = "HwStatusCheck")]
        private static Task<CheckExecutionOutcome> CheckTpHwStatusOpticalGroupAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.HwStatusOpticalGroup, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.HwStatusControlStorageGroup, DisplayName = "HW Status - Control/Storage", Description = "Check control & storage devices (MCU/EEPROM/Flash/Sensor/Watchdog)", DefaultCategory = "HwStatusCheck")]
        private static Task<CheckExecutionOutcome> CheckTpHwStatusControlStorageGroupAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.HwStatusControlStorageGroup, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.HwStatusInterfaceCommGroup, DisplayName = "HW Status - Interface/Comm", Description = "Check interface & comm devices (I/O Port/DAC/ADC/SPI/I2C)", DefaultCategory = "HwStatusCheck")]
        private static Task<CheckExecutionOutcome> CheckTpHwStatusInterfaceCommGroupAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.HwStatusInterfaceCommGroup, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.OpticalResidualRisk, DisplayName = "Optical Residual Risk", Description = "Analyze optical path residual risk after RTS", DefaultCategory = "OpticalPerformanceCheck")]
        private static Task<CheckExecutionOutcome> CheckTpOpticalResidualRiskAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.OpticalResidualRisk, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.OpticalCustomGrrRule, DisplayName = "Custom Rule - GRR Threshold", Description = "Check optical GRR metric against custom threshold rule", DefaultCategory = "OpticalPerformanceCheck")]
        private static Task<CheckExecutionOutcome> CheckTpOpticalCustomGrrRuleAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.OpticalCustomGrrRule, item, step, runContext, ct);
        }

        [CheckExecutor(TpCheckIds.OpticalCustomSnrRule, DisplayName = "Custom Rule - SNR Threshold", Description = "Check optical SNR metric against custom threshold rule", DefaultCategory = "OpticalPerformanceCheck")]
        private static Task<CheckExecutionOutcome> CheckTpOpticalCustomSnrRuleAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteTpAsync(TpCheckIds.OpticalCustomSnrRule, item, step, runContext, ct);
        }

        /// <summary>
        /// 根据 checkId 分发到不同的 TP 子检查逻辑。
        /// </summary>
        private static async Task<CheckExecutionOutcome> ExecuteTpAsync(
            string checkId,
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            var snapshot = runContext != null ? runContext.TpConnectivity : null;
            if (snapshot == null &&
                checkId != TpCheckIds.VersionCompliance &&
                checkId != TpCheckIds.StationCapabilityCompliance &&
                checkId != TpCheckIds.PowerSupplyQuality &&
                checkId != TpCheckIds.DefaultInfoAndLut &&
                checkId != TpCheckIds.HwSwFwConfigIntegrity &&
                checkId != TpCheckIds.HwStatusOpticalGroup &&
                checkId != TpCheckIds.HwStatusControlStorageGroup &&
                checkId != TpCheckIds.HwStatusInterfaceCommGroup &&
                checkId != TpCheckIds.OpticalResidualRisk &&
                checkId != TpCheckIds.OpticalCustomGrrRule &&
                checkId != TpCheckIds.OpticalCustomSnrRule)
            {
                item.Status = CheckStatus.Warning;
                item.Detail = "未获取 TP 连接检查快照";
                item.Score = 90;
                return new CheckExecutionOutcome { Success = false };
            }
            var tpSnapshot = snapshot;

            if (checkId == TpCheckIds.PathAndConfig)
            {
                if (!tpSnapshot.TpPathExists)
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"TP 路径不可用: {tpSnapshot.TpRootPath}";
                    item.FixSuggestion = "确认 TP 路径和目录权限";
                    item.Score = 60;
                }
                else
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"TP 路径有效，发现配置文件 {tpSnapshot.ConfigFiles.Count} 个";
                    item.Score = 100;
                }
            }
            else if (checkId == TpCheckIds.SerialPorts)
            {
                if (!tpSnapshot.TpPathExists)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "TP 路径不可用，跳过串口检查";
                    item.Score = 90;
                }
                else if (tpSnapshot.ExpectedSerialPorts.Count == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "未在 TP 配置中识别到串口映射";
                    item.Score = 90;
                }
                else if (tpSnapshot.MissingSerialPorts.Count == 0)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"串口映射正常: {string.Join(", ", tpSnapshot.ExpectedSerialPorts)}";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"缺失串口: {string.Join(", ", tpSnapshot.MissingSerialPorts)}";
                    item.FixSuggestion = "检查串口设备连接、驱动与 COM 口映射";
                    item.Score = 65;
                }
            }
            else if (checkId == TpCheckIds.NetworkEndpoints)
            {
                if (!tpSnapshot.TpPathExists)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "TP 路径不可用，跳过网口检查";
                    item.Score = 90;
                }
                else if (tpSnapshot.NetworkEndpoints.Count == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = "未在 TP 配置中识别到网口目标";
                    item.Score = 90;
                }
                else
                {
                    var failed = tpSnapshot.NetworkEndpoints.Where(e => !e.Reachable).ToList();
                    if (failed.Count == 0)
                    {
                        item.Status = CheckStatus.Pass;
                        item.Detail = $"网口目标全部可达，共 {tpSnapshot.NetworkEndpoints.Count} 个";
                        item.Score = 100;
                    }
                    else
                    {
                        item.Status = CheckStatus.Fail;
                        item.Detail = $"不可达目标 {failed.Count} 个: {string.Join("; ", failed.Select(f => $"{f.Endpoint}({f.Error})"))}";
                        item.FixSuggestion = "检查网络连通、防火墙、交换机/VLAN 与目标服务状态";
                        item.Score = 65;
                    }
                }
            }
            else if (checkId == TpCheckIds.VersionCompliance)
            {
                var result = await VersionComplianceChecker.CheckAsync(step, runContext, ct);
                var sourceLabel = string.IsNullOrWhiteSpace(result.RequirementSource) ? "tms" : result.RequirementSource;
                var totalCount = result.Requirements.Count;
                var mismatchCount = result.Mismatches.Count;
                var missingCount = result.Mismatches.Count(m => m.MissingActual);
                var matchCount = Math.Max(0, totalCount - mismatchCount);
                if (!result.ApiSuccess)
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"版本要求获取失败（source: {sourceLabel}）: {result.ApiMessage}";
                    item.FixSuggestion = "检查 TMS API 地址、权限与接口可用性";
                    item.Score = 60;
                }
                else if (result.Requirements.Count == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = $"未返回版本要求（source: {sourceLabel}, path: {result.RequirementUrl}）";
                    item.FixSuggestion = "确认 TMS 版本配置是否已维护";
                    item.Score = 90;
                }
                else if (result.Mismatches.Count == 0)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"版本符合要求（source: {sourceLabel}）: 总计 {totalCount}，匹配 {matchCount}，不匹配 0，缺失 0（path: {result.RequirementUrl}）";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"版本不匹配（source: {sourceLabel}）: 总计 {totalCount}，匹配 {matchCount}，不匹配 {mismatchCount}，缺失 {missingCount}；明细: {string.Join("; ", result.Mismatches.Select(m => m.MissingActual ? $"{m.DeviceKey}:缺少实际版本(要求{m.RequiredVersion})" : $"{m.DeviceKey}:实际{m.ActualVersion}/要求{m.RequiredVersion}"))}";
                    item.FixSuggestion = "更新设备固件/版本或同步 TMS 目标版本配置";
                    item.Score = 65;
                }
            }
            else if (checkId == TpCheckIds.StationCapabilityCompliance ||
                     checkId == TpCheckIds.OpticalCustomGrrRule ||
                     checkId == TpCheckIds.OpticalCustomSnrRule)
            {
                var result = StationCapabilityComplianceChecker.Check(step, runContext);
                var failedMetrics = result.Metrics.Where(m => !m.Pass).ToList();
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"工位能力指标全部满足要求，共 {result.Metrics.Count} 项（数据源: {result.ActualSource}）";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = failedMetrics.Count == 0
                        ? $"工位能力要求不可用或数据缺失（数据源: {result.ActualSource}）"
                        : $"不满足 {failedMetrics.Count} 项（数据源: {result.ActualSource}）: {string.Join("; ", result.FailReasons.Count > 0 ? result.FailReasons : failedMetrics.Select(f => $"{f.Metric} 实际{f.Actual} / 要求{f.Required}"))}";
                    item.FixSuggestion = "检查工位实测数据、治具状态与 MIMS 下发要求";
                    item.Score = 65;
                }
            }
            else if (checkId == TpCheckIds.PowerSupplyQuality)
            {
                var result = await PowerSupplyQualityChecker.CheckAsync(runContext, ct);
                var curve = string.Join(", ", result.Samples.Select(s => s.VoltageV.ToString("F3")));
                var curveFiles = string.IsNullOrWhiteSpace(result.CurveJsonPath) && string.IsNullOrWhiteSpace(result.CurveCsvPath)
                    ? "未落盘"
                    : $"JSON={result.CurveJsonPath}, CSV={result.CurveCsvPath}";
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"电源电压质量合格（{result.Source}）| 均值{result.MeanV:F3}V 标准差{result.StdDevV:F4}V 纹波{result.RippleV:F4}V | 曲线: [{curve}] | 文件: {curveFiles}";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"电源电压质量不满足要求（{result.Source}）| 均值{result.MeanV:F3}V 标准差{result.StdDevV:F4}V 纹波{result.RippleV:F4}V | {string.Join(" | ", result.FailReasons)} | 曲线: [{curve}] | 文件: {curveFiles}";
                    item.FixSuggestion = "检查电源模块、负载波动、采样链路和 TP 采集接口";
                    item.Score = 60;
                }
            }
            else if (checkId == TpCheckIds.DefaultInfoAndLut)
            {
                var result = await DefaultInfoAndLutChecker.CheckAsync(step, runContext, ct);
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"默认信息与LUT校验通过（{result.Source}）| 默认信息: {result.DefaultInfoSummary} | LUT: {result.LutSummary}";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"默认信息与LUT校验失败（{result.Source}）| 默认信息URL: {result.DefaultInfoUrl} | LUT URL: {result.LutDownloadUrl} | {string.Join(" | ", result.FailReasons)}";
                    item.FixSuggestion = "检查 MIMS 下发默认信息、LUT 下载地址和内容完整性";
                    item.Score = 60;
                }
            }
            else if (checkId == TpCheckIds.HwSwFwConfigIntegrity)
            {
                var result = await HwSwFwConfigIntegrityChecker.CheckAsync(step, runContext, ct);
                var total = result.Metrics.Count;
                var failedCount = result.Metrics.Count(m => !m.Pass);
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"配置/损坏数据检查通过（{result.Source}）: 总计 {total}，失败 0";
                    item.Score = 100;
                }
                else if (total == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = $"配置/损坏数据检查无有效结果（{result.Source}）: {string.Join("; ", result.FailReasons)}";
                    item.Score = 88;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"配置/损坏数据检查失败（{result.Source}）: 总计 {total}，失败 {failedCount}；{string.Join("; ", result.FailReasons)}";
                    item.FixSuggestion = "修复损坏数据、校准默认配置并同步 MIMS/TMS 配置";
                    item.Score = 62;
                }
            }
            else if (checkId == TpCheckIds.HwStatusOpticalGroup ||
                     checkId == TpCheckIds.HwStatusControlStorageGroup ||
                     checkId == TpCheckIds.HwStatusInterfaceCommGroup)
            {
                var result = await HwStatusGroupedChecker.CheckAsync(step, runContext, ct);
                var total = result.Metrics.Count;
                var failedCount = result.Metrics.Count(m => !m.Pass);
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"HW 状态分组检查通过（{result.GroupKey}, {result.Source}）: 总计 {total}，失败 0";
                    item.Score = 100;
                }
                else if (total == 0)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = $"HW 状态分组无有效结果（{result.GroupKey}, {result.Source}）: {string.Join("; ", result.FailReasons)}";
                    item.Score = 86;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = $"HW 状态分组检查失败（{result.GroupKey}, {result.Source}）: 总计 {total}，失败 {failedCount}；{string.Join("; ", result.FailReasons)}";
                    item.FixSuggestion = "检查设备连接逻辑稳定性、指令链路和 I/O / DAC/ADC / SPI/I2C 通信";
                    item.Score = 60;
                }
            }
            else if (checkId == TpCheckIds.OpticalResidualRisk)
            {
                var result = await OpticalPathResidualRiskChecker.CheckAsync(step, runContext, ct);
                var total = result.Metrics.Count;
                var failedCount = result.Metrics.Count(m => !m.Pass);
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"光路残留风险分析通过（{result.Source}）: 总计 {total}，失败 0";
                    item.Score = 100;
                }
                else
                {
                    item.Status = failedCount == 0 ? CheckStatus.Warning : CheckStatus.Fail;
                    item.Detail = $"光路残留风险分析未通过（{result.Source}）: 总计 {total}，失败 {failedCount}；{string.Join("; ", result.FailReasons)}";
                    item.FixSuggestion = "重点排查纤芯、熔接点、RTS 后残留异常与盘盒异常";
                    item.Score = failedCount == 0 ? 84 : 58;
                }
            }

            return new CheckExecutionOutcome { Success = IsSuccessful(item.Status) };
        }

        private static bool IsSuccessful(CheckStatus status)
        {
            return status == CheckStatus.Pass || status == CheckStatus.Fixed;
        }
    }
}