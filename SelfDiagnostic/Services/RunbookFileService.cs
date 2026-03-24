using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    public sealed class RunbookFileService
    {
        private const string RunbookDirRelativePath = @"config\runbook";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        public string GetRunbookDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RunbookDirRelativePath);
        }

        public string BuildRunbookPath(string runbookId)
        {
            if (string.IsNullOrWhiteSpace(runbookId))
            {
                throw new ArgumentException("RunBook Id 不能为空", nameof(runbookId));
            }

            return Path.Combine(GetRunbookDirectory(), runbookId + ".runbook.json");
        }

        public RunbookDefinition Load(string runbookId)
        {
            var path = BuildRunbookPath(runbookId);
            return LoadFromPath(path);
        }

        public RunbookDefinition LoadFromPath(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("RunBook 文件不存在: " + path);
            }

            var json = File.ReadAllText(path);
            var runbook = JsonConvert.DeserializeObject<RunbookDefinition>(json);
            if (runbook == null)
            {
                throw new InvalidOperationException("RunBook 反序列化失败");
            }

            Validate(runbook);
            return runbook;
        }

        public void Save(RunbookDefinition runbook, string runbookId)
        {
            Validate(runbook);
            var directory = GetRunbookDirectory();
            Directory.CreateDirectory(directory);

            var path = BuildRunbookPath(runbookId);
            var runbookToSave = new RunbookDefinition
            {
                Id = runbookId,
                Title = runbook.Title,
                Version = runbook.Version,
                Steps = runbook.Steps
            };
            var json = JsonConvert.SerializeObject(runbookToSave, JsonSettings);
            File.WriteAllText(path, json);
        }

        public void AutoRelinkByEnabledOrder(IList<RunbookStepDefinition> steps)
        {
            var enabledIndexes = steps
                .Select((s, i) => new { Step = s, Index = i })
                .Where(x => x.Step.Enabled)
                .ToList();

            for (var i = 0; i < enabledIndexes.Count; i++)
            {
                var current = enabledIndexes[i];
                var next = i < enabledIndexes.Count - 1 ? enabledIndexes[i + 1].Step.StepId : string.Empty;
                var src = current.Step;
                steps[current.Index] = new RunbookStepDefinition
                {
                    StepId = src.StepId,
                    CheckId = src.CheckId,
                    DisplayName = src.DisplayName,
                    Category = src.Category,
                    TimeoutMs = src.TimeoutMs,
                    Enabled = src.Enabled,
                    NextOnSuccess = next,
                    NextOnFailure = next,
                    Params = new Dictionary<string, string>(src.Params, StringComparer.OrdinalIgnoreCase)
                };
            }
        }

        public static void Validate(RunbookDefinition runbook)
        {
            if (runbook.Steps.Count == 0)
            {
                throw new InvalidOperationException("RunBook 至少需要一个步骤");
            }

            var enabledSteps = runbook.Steps.Where(s => s.Enabled).ToList();
            if (enabledSteps.Count == 0)
            {
                throw new InvalidOperationException("RunBook 至少需要一个启用步骤");
            }

            var allStepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var step in runbook.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.StepId))
                {
                    throw new InvalidOperationException("存在空 StepId");
                }

                if (string.IsNullOrWhiteSpace(step.CheckId))
                {
                    throw new InvalidOperationException("步骤 " + step.StepId + " 的 CheckId 为空");
                }

                if (!allStepIds.Add(step.StepId))
                {
                    throw new InvalidOperationException("RunBook 存在重复 StepId: " + step.StepId);
                }
            }

            var enabledSet = new HashSet<string>(enabledSteps.Select(s => s.StepId), StringComparer.OrdinalIgnoreCase);
            foreach (var step in enabledSteps)
            {
                if (!string.IsNullOrWhiteSpace(step.NextOnSuccess) && !enabledSet.Contains(step.NextOnSuccess))
                {
                    throw new InvalidOperationException("步骤 " + step.StepId + " 的 NextOnSuccess 指向不存在或已禁用步骤: " + step.NextOnSuccess);
                }

                if (!string.IsNullOrWhiteSpace(step.NextOnFailure) && !enabledSet.Contains(step.NextOnFailure))
                {
                    throw new InvalidOperationException("步骤 " + step.StepId + " 的 NextOnFailure 指向不存在或已禁用步骤: " + step.NextOnFailure);
                }
            }
        }
    }
}
