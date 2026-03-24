using System;
using System.IO;
using Newtonsoft.Json;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    public sealed class RunbookProvider
    {
        private const string DefaultRunbookRelativePath = @"config\runbook\default.runbook.json";

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
