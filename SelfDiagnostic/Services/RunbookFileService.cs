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

        public static void Validate(RunbookDefinition runbook)
        {
            if (runbook.Steps.Count == 0)
            {
                throw new InvalidOperationException("RunBook must contain at least one step");
            }

            var enabledSteps = runbook.Steps.Where(s => s.Enabled).ToList();
            if (enabledSteps.Count == 0)
            {
                throw new InvalidOperationException("RunBook must contain at least one enabled step");
            }

            for (int i = 0; i < runbook.Steps.Count; i++)
            {
                var step = runbook.Steps[i];
                if (string.IsNullOrWhiteSpace(step.CheckId))
                {
                    throw new InvalidOperationException("Step #" + (i + 1) + " has empty CheckId");
                }

                if (step.Enabled && string.IsNullOrWhiteSpace(step.BindMethod))
                {
                    throw new InvalidOperationException("Enabled step " + step.CheckId + " has empty BindMethod");
                }
            }
        }
    }
}
