using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services.Executors.External
{
    /// <summary>
    /// 外部依赖检查执行器集合 — 通过 HTTP POST 探测 MES / TMS / TAS / 文件服务器 / 网关 等外部系统的可达性。
    /// 每个方法标注 [CheckExecutor] 以便引擎自动发现并注册。
    /// </summary>
    internal static class ExternalCheckExecutors
    {
        private static readonly ExternalDependencyHttpChecker ExternalChecker = new ExternalDependencyHttpChecker();

        /// <summary>检查 MES API 连通性</summary>
        [CheckExecutor(ExternalDependencyIds.Mes, DisplayName = "MES API Connectivity", Description = "HTTP POST to MES endpoint and verify response", DefaultCategory = "StationCheck")]
        private static Task<CheckExecutionOutcome> CheckExternalMesAsync(
            DiagnosticItem item, RunbookStepDefinition step, DiagnosticRunContext runContext, CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Mes, item, step, runContext, ct);
        }

        /// <summary>检查 TMS API 连通性</summary>
        [CheckExecutor(ExternalDependencyIds.Tms, DisplayName = "TMS API Connectivity", Description = "HTTP POST to TMS endpoint and verify response", DefaultCategory = "StationCheck")]
        private static Task<CheckExecutionOutcome> CheckExternalTmsAsync(
            DiagnosticItem item, RunbookStepDefinition step, DiagnosticRunContext runContext, CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Tms, item, step, runContext, ct);
        }

        /// <summary>检查 TAS AOI API 连通性</summary>
        [CheckExecutor(ExternalDependencyIds.Tas, DisplayName = "TAS AOI API Connectivity", Description = "HTTP POST to TAS AOI endpoint and verify response", DefaultCategory = "StationCheck")]
        private static Task<CheckExecutionOutcome> CheckExternalTasAsync(
            DiagnosticItem item, RunbookStepDefinition step, DiagnosticRunContext runContext, CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Tas, item, step, runContext, ct);
        }

        /// <summary>检查文件服务器连通性</summary>
        [CheckExecutor(ExternalDependencyIds.FileServer, DisplayName = "File Server Connectivity", Description = "HTTP POST to File Server endpoint and verify response", DefaultCategory = "StationCheck")]
        private static Task<CheckExecutionOutcome> CheckExternalFileServerAsync(
            DiagnosticItem item, RunbookStepDefinition step, DiagnosticRunContext runContext, CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.FileServer, item, step, runContext, ct);
        }

        /// <summary>检查 LAN 网关连通性</summary>
        [CheckExecutor(ExternalDependencyIds.Lan, DisplayName = "LAN Gateway Connectivity", Description = "HTTP POST to LAN gateway endpoint and verify response", DefaultCategory = "StationCheck")]
        private static Task<CheckExecutionOutcome> CheckExternalLanAsync(
            DiagnosticItem item, RunbookStepDefinition step, DiagnosticRunContext runContext, CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Lan, item, step, runContext, ct);
        }

        /// <summary>
        /// 执行外部依赖探测的共享逻辑：
        /// 1. 优先从 step.Params["dependencyId"] 获取实际 ID，否则使用默认 dependencyId
        /// 2. 调用 HTTP 探测器执行 POST 请求
        /// </summary>
        private static async Task<CheckExecutionOutcome> ExecuteExternalAsync(
            string dependencyId,
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            string id;
            var resolvedDependencyId = step.Params.TryGetValue("dependencyId", out id) && !string.IsNullOrWhiteSpace(id)
                ? id
                : dependencyId;
            await CheckExternalApiAsync(item, resolvedDependencyId, runContext, ct);
            return new CheckExecutionOutcome { Success = IsSuccessful(item.Status) };
        }

        /// <summary>
        /// 对单个外部 API 端点执行 HTTP 探测并将结果写入 DiagnosticItem
        /// </summary>
        private static async Task CheckExternalApiAsync(
            DiagnosticItem item,
            string dependencyId,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            if (runContext == null || !runContext.ExternalChecksEnabled || runContext.ExternalConfig == null)
            {
                item.Status = CheckStatus.Warning;
                item.Detail = string.IsNullOrWhiteSpace(runContext != null ? runContext.ConfigError : null)
                    ? "未获取外部系统配置，已跳过该项"
                    : $"外部系统配置不可用: {runContext.ConfigError}";
                item.Score = 90;
                return;
            }

            try
            {
                var result = await ExternalChecker.CheckAsync(dependencyId, runContext.ExternalConfig, ct);
                var statusLabel = result.StatusCode.HasValue ? $"HTTP {result.StatusCode.Value}" : "NO_HTTP_STATUS";
                if (result.Success)
                {
                    item.Status = CheckStatus.Pass;
                    item.Detail = $"{result.EndpointName} POST {result.Url} -> {statusLabel} ({result.ElapsedMs}ms)";
                    item.Score = 100;
                }
                else
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = string.IsNullOrWhiteSpace(result.Error)
                        ? $"{result.EndpointName} POST {result.Url} -> {statusLabel} ({result.ElapsedMs}ms)"
                        : $"{result.EndpointName} POST {result.Url} -> {statusLabel} ({result.ElapsedMs}ms) | {result.Error}";
                    item.FixSuggestion = $"检查 {result.EndpointName} 服务状态、网关与路由配置";
                    item.Score = 70;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.Status = CheckStatus.Fail;
                item.Detail = $"外部接口调用失败: {ex.Message}";
                item.FixSuggestion = "检查外部系统地址可达性与服务进程";
                item.Score = 60;
            }
        }

        private static bool IsSuccessful(CheckStatus status)
        {
            return status == CheckStatus.Pass || status == CheckStatus.Fixed;
        }
    }
}