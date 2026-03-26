using System;
using System.IO;
using Newtonsoft.Json;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// RunBook 提供者 — 加载默认 RunBook 配置。
    /// </summary>
    public sealed class RunbookProvider
    {
        private const string DefaultRunbookRelativePath = @"config\runbook\default.runbook.json";

        /// <summary>
        /// 从应用程序基目录下的默认路径加载 RunBook，并调用 <see cref="RunbookFileService.Validate"/> 校验。
        /// </summary>
        public RunbookDefinition Load()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultRunbookRelativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("RunBook 配置文件不存在: " + path);
            }

            var json = File.ReadAllText(path);
            var runbook = JsonConvert.DeserializeObject<RunbookDefinition>(json);
            if (runbook == null)
            {
                throw new InvalidOperationException("RunBook 配置解析失败");
            }

            RunbookFileService.Validate(runbook);
            return runbook;
        }
    }
}
