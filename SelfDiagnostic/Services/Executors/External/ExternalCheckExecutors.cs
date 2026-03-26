using System;
using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;

namespace SelfDiagnostic.Services.Executors.External
{
    /// <summary>
    /// 外部依赖检查执行器集合（主项目副本）— HTTP POST 探测外部系统。
    /// </summary>
    internal static class ExternalCheckExecutors
    {
        private static readonly ExternalDependencyHttpChecker ExternalChecker = new ExternalDependencyHttpChecker();

        [CheckExecutor(ExternalDependencyIds.Mes)]
        private static Task<CheckExecutionOutcome> CheckExternalMesAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Mes, item, step, runContext, ct);
        }

        [CheckExecutor(ExternalDependencyIds.Tms)]
        private static Task<CheckExecutionOutcome> CheckExternalTmsAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Tms, item, step, runContext, ct);
        }

        [CheckExecutor(ExternalDependencyIds.Tas)]
        private static Task<CheckExecutionOutcome> CheckExternalTasAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Tas, item, step, runContext, ct);
        }

        [CheckExecutor(ExternalDependencyIds.FileServer)]
        private static Task<CheckExecutionOutcome> CheckExternalFileServerAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.FileServer, item, step, runContext, ct);
        }

        [CheckExecutor(ExternalDependencyIds.Lan)]
        private static Task<CheckExecutionOutcome> CheckExternalLanAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            return ExecuteExternalAsync(ExternalDependencyIds.Lan, item, step, runContext, ct);
        }

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
