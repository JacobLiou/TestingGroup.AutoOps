using Newtonsoft.Json;
using SelfDiagnostic.Models;
using System;
using System.IO;
using System.Linq;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// RunBook 文件服务 — 负责 RunBook JSON 文件的读取、保存和列表查询。
    /// </summary>
    public sealed class RunbookFileService
    {
        private const string RunbookDirRelativePath = @"config\runbook";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        /// <summary>
        /// 返回 RunBook 目录的绝对路径（应用程序基目录下 config\runbook）。
        /// </summary>
        public string GetRunbookDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RunbookDirRelativePath);
        }

        /// <summary>
        /// 根据 RunBook Id 构建 JSON 文件的完整路径。
        /// </summary>
        public string BuildRunbookPath(string runbookId)
        {
            if (string.IsNullOrWhiteSpace(runbookId))
            {
                throw new ArgumentException("RunBook Id 不能为空", nameof(runbookId));
            }

            return Path.Combine(GetRunbookDirectory(), runbookId + ".runbook.json");
        }

        /// <summary>
        /// 按 Id 从标准目录加载 RunBook 并校验。
        /// </summary>
        public RunbookDefinition Load(string runbookId)
        {
            var path = BuildRunbookPath(runbookId);
            return LoadFromPath(path);
        }

        /// <summary>
        /// 从指定文件路径读取并反序列化 RunBook，并进行完整性校验。
        /// </summary>
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

        /// <summary>
        /// 校验后将 RunBook 序列化为 JSON 并写入磁盘（目录不存在时自动创建）。
        /// </summary>
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

        /// <summary>
        /// 校验 RunBook 至少包含一个 Step、至少一个已启用 Step，且启用项具备 CheckId 与 BindMethod。
        /// </summary>
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