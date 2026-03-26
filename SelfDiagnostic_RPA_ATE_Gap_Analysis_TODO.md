# SelfDiagnostic 对标 RPA/ATE 执行引擎 — 差距分析与演进路线图

> 文档版本: 1.0  
> 创建日期: 2026-03-25  
> 对标系统: NI TestStand / Keysight PathWave / OpenTAP / UiPath / Automation Anywhere

---

## 一、执行摘要

SelfDiagnostic 已建立了一个基于 **Runbook JSON + 插件 DLL + 反射注册** 的可编排诊断执行框架，具备：
- Step 级别的 DLL 方法绑定
- 插件化扫描与自动注册
- 可视化 RunBook 编辑器
- 超时控制 / Enable-Disable / 参数化

但对标 NI TestStand 等专业 ATE 序列执行器，或 UiPath 等 RPA 编排引擎，**在流程控制、通用方法适配、数据流、执行追踪、错误恢复等核心维度上存在结构性差距**。

本文档将差距拆解为 **7 大维度、35+ 项具体特性**，并给出优先级排序与实施建议。

---

## 二、架构对标：核心概念映射

| 概念 | NI TestStand | OpenTAP | UiPath RPA | SelfDiagnostic (当前) |
|------|-------------|---------|------------|----------------------|
| 测试计划/流程 | Sequence File (.seq) | Test Plan (.TapPlan) | Workflow (.xaml) | RunbookDefinition (.runbook.json) |
| 执行步骤 | Step | Test Step | Activity | RunbookStepDefinition |
| 步骤类型 | Action/Pass-Fail/Numeric Limit/... | ITestStep | Activity Type | 仅 CheckExecutor (单一类型) |
| 代码模块绑定 | Code Module (任意 DLL/EXE/脚本) | 任意 .NET 方法 / Plugin | Activity Package | `[CheckExecutor]` 标注的静态方法 |
| 方法签名约束 | 无限制 (通过 Adapter 适配) | `void Run()` 基类重写 | 无限制 | 仅 2 种固定签名 |
| 参数传递 | Step Properties + Locals + FileGlobals | Settings 属性 | Arguments (In/Out/InOut) | `Dictionary<string,string> Params` |
| 流程控制 | Goto/Loop/If-Then/Parallel/... | 条件/循环/并行 | If/Switch/While/Parallel/... | 纯顺序 for 循环 |
| 数据流 | Variables (Local/FileGlobal/StationGlobal) | Properties + Results | Variables + Arguments | 无 step 间数据传递 |
| 模块适配器 | C/CVI/LabVIEW/.NET/ActiveX/HTBasic/... | .NET Plugin 体系 | .NET Activity | 仅 .NET 反射 |
| 执行回调 | Process Model (Setup/Main/Cleanup) | PrePlanRun/PostPlanRun | Before/After hooks | 无 |
| 错误处理 | Step On-Fail (Goto/Retry/Ignore/Abort) | Error handling属性 | Try-Catch-Finally | 仅 catch 写 Warning |
| 执行报告 | Report Generator (HTML/XML/ATML) | Result Listeners | Output Panel + Log | DiagnosticItem.Detail 字符串 |
| 插件管理 | Type Palette / Adapter | Package Manager (NuGet-like) | Package Manager | 文件夹扫描 plugins/*.dll |
| 版本管理 | Sequence File Versioning | 基于 NuGet 版本 | Package Versioning | runbook.Version 字段 (无实际管控) |
| 用户交互 | Message Popup / Input Prompt | 无内建 | Input Dialog | 无 |
| 并行执行 | Parallel / Batch | Parallel Step | Parallel Activity | 无 |

---

## 三、差距详细分析 — 7 大维度

### 维度 1: 方法绑定的通用性

> **核心问题**：当前只能绑定标注了 `[CheckExecutor]` 且满足 2 种固定签名的方法，不能绑定真正"任意"的 DLL 方法。

| # | 特性 | TestStand/RPA | SelfDiagnostic | 差距等级 |
|---|------|--------------|----------------|---------|
| 1.1 | 绑定任意 .NET 公开方法（不需要特殊标注） | 支持 | 不支持，必须标注 `[CheckExecutor]` | **严重** |
| 1.2 | 支持任意方法签名（参数类型/数量/返回值不限） | 支持 (通过 Adapter) | 仅 2 种签名 | **严重** |
| 1.3 | 方法参数自动从 Step.Params 映射（类型转换） | 支持 | 不支持，需手动从字典取值 | **中等** |
| 1.4 | 支持实例方法（非 static） | 支持 | 不支持，仅 static 方法 | **中等** |
| 1.5 | 支持 Native DLL 调用 (P/Invoke / COM) | 支持 (CVI/ActiveX Adapter) | 不支持 | **中等** |
| 1.6 | 支持脚本语言 (Python/PowerShell/Lua) | TestStand: 部分; RPA: 支持 | 不支持 | **低** |
| 1.7 | BindDll 字段驱动按需加载 | TestStand: 自动定位 | 仅装饰性，不实际加载 | **中等** |

**建议实施方案**：

```
优先级: P0 — 这是达到"任意绑定"的最关键改造

方案A (最小改动): 在 BuildMethodExecutor 增加通用 MethodInfo.Invoke 反射路径
  - 对不匹配两种固定签名的方法，自动从 step.Params 映射参数
  - 返回值自动适配为 CheckExecutionOutcome

方案B (推荐): 引入 Adapter 模式
  - IMethodAdapter 接口，不同 Adapter 处理不同调用方式
  - DotNetReflectionAdapter: 通用 .NET 反射调用
  - NativeAdapter: P/Invoke 动态调用
  - ScriptAdapter: 调用 Python/PowerShell 脚本
```

---

### 维度 2: 流程控制

> **核心问题**：当前是纯顺序 for 循环，不支持任何分支、循环、并行、跳转。

| # | 特性 | TestStand/RPA | SelfDiagnostic | 差距等级 |
|---|------|--------------|----------------|---------|
| 2.1 | 条件分支 (If step A fails → skip B) | 支持 | 不支持 | **严重** |
| 2.2 | 循环执行 (Loop N times / Loop until) | 支持 | 不支持 | **严重** |
| 2.3 | 重试机制 (Retry on fail with delay) | 支持 | 不支持 | **严重** |
| 2.4 | 跳转 (Goto step X) | 支持 | 不支持 | **中等** |
| 2.5 | 并行执行 (Parallel steps) | 支持 | 不支持 | **中等** |
| 2.6 | 子序列 / 嵌套 Runbook 调用 | 支持 (Sub-Sequence) | 不支持 | **中等** |
| 2.7 | Setup / Cleanup 序列 (类似 BeforeAll/AfterAll) | 支持 (Process Model) | 不支持 | **低** |
| 2.8 | 断点 / 单步调试 | 支持 | 不支持 | **低** |

**建议实施方案**：

```
优先级: P0

Step 1: 在 RunbookStepDefinition 增加流程控制字段
  {
    "OnFail": "skip_next" | "goto:STEP_ID" | "retry" | "abort" | "continue",
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "Condition": "${SYS_01.Success} == true",
    "GroupId": "parallel-group-1"
  }

Step 2: 在 DiagnosticEngine 中将 for 循环改为状态机驱动的 StepExecutionLoop
Step 3: 实现 ConditionEvaluator 解析 ${} 表达式
```

---

### 维度 3: Step 间数据传递

> **核心问题**：Step 之间无法传递运行时数据，只有静态 Params 和共享的 DiagnosticRunContext。

| # | 特性 | TestStand/RPA | SelfDiagnostic | 差距等级 |
|---|------|--------------|----------------|---------|
| 3.1 | Step 输出变量 → 后续 Step 输入 | 支持 (Variables/Arguments) | 不支持 | **严重** |
| 3.2 | 全局变量 (Runbook 级别共享) | 支持 (FileGlobals/StationGlobals) | 仅 DiagnosticRunContext | **中等** |
| 3.3 | 表达式引用 (${stepA.output.serialNumber}) | 支持 | 不支持 | **中等** |
| 3.4 | 类型化参数 (int/double/bool, 非全是 string) | 支持 | Params 仅 `Dictionary<string,string>` | **中等** |

**建议实施方案**：

```
优先级: P1

Step 1: 增加 StepExecutionResult 包含 OutputVariables
  public class StepExecutionResult {
      public bool Success;
      public Dictionary<string, object> Outputs;
  }

Step 2: 在 DiagnosticRunContext 增加 RunVariables 共享字典
  public Dictionary<string, object> RunVariables { get; }

Step 3: Params 支持模板语法 "${PREV_STEP.output_key}" 在运行时替换

Step 4: Params 值类型从 string 扩展为 object (JSON any type)
```

---

### 维度 4: 错误处理与恢复

> **核心问题**：当前 step 失败后只记录 Warning/Fail 状态，没有结构化的错误恢复策略。

| # | 特性 | TestStand/RPA | SelfDiagnostic | 差距等级 |
|---|------|--------------|----------------|---------|
| 4.1 | Step 级别 OnFail 策略 (Retry/Skip/Abort/Goto) | 支持 | 不支持 | **严重** |
| 4.2 | Cleanup 步骤 (无论成功失败都执行) | 支持 | 不支持 | **中等** |
| 4.3 | 错误传播控制 (某个 step fail 不影响后续) | TestStand: 精细控制 | 当前全部继续执行 | **低** (已部分满足) |
| 4.4 | 异常分类 (Fatal vs Recoverable) | 支持 | 不区分 | **低** |

---

### 维度 5: 执行追踪与报告

> **核心问题**：缺少结构化的执行时间线、step 级别耗时、输入输出日志，无法满足 SPC/追溯需求。

| # | 特性 | TestStand/RPA | SelfDiagnostic | 差距等级 |
|---|------|--------------|----------------|---------|
| 5.1 | Step 执行时间戳 (StartTime/EndTime/ElapsedMs) | 支持 | 不记录 | **严重** |
| 5.2 | 结构化执行报告 (JSON/XML/HTML) | 支持 (ATML/HTML Report) | 无 | **严重** |
| 5.3 | 执行日志 (带时间线的详细 log) | 支持 | 仅 DiagnosticItem.Detail | **中等** |
| 5.4 | 历史记录存储与查询 | 支持 | 无 | **中等** |
| 5.5 | 执行结果导出 (CSV/PDF/Database) | 支持 | 无 | **中等** |
| 5.6 | 实时执行事件流 (Progress/Event) | 支持 | 仅 UI 刷新 | **低** |

**建议实施方案**：

```
优先级: P1

Step 1: 增加 StepExecutionRecord
  public class StepExecutionRecord {
      public string StepId;
      public DateTime StartTimeUtc;
      public DateTime EndTimeUtc;
      public long ElapsedMs;
      public string Status;
      public Dictionary<string, object> Inputs;
      public Dictionary<string, object> Outputs;
      public string ErrorMessage;
  }

Step 2: 增加 RunExecutionReport (聚合所有 StepExecutionRecord)

Step 3: 实现 IReportGenerator 接口
  - JsonReportGenerator
  - HtmlReportGenerator
  - CsvReportGenerator
```

---

### 维度 6: 插件管理与生态

> **核心问题**：当前是简单的文件夹扫描，缺少版本管理、依赖解析、热插拔能力。

| # | 特性 | TestStand/RPA | SelfDiagnostic | 差距等级 |
|---|------|--------------|----------------|---------|
| 6.1 | 插件版本管理 | 支持 (NuGet / Package Manager) | 无 | **中等** |
| 6.2 | 插件依赖解析 | 支持 | 无 | **中等** |
| 6.3 | 运行时热加载 / 卸载插件 | 部分支持 | 不支持 (static readonly) | **中等** |
| 6.4 | 插件沙箱隔离 (防止崩溃影响主进程) | TestStand: Out-of-Process | 无 | **低** |
| 6.5 | 插件开发 SDK / 模板项目 | 支持 | 仅靠引用 Abstractions 项目 | **低** |

---

### 维度 7: 用户交互与 UX

> **核心问题**：执行过程中无法暂停等待用户输入，Runbook 编辑器功能有限。

| # | 特性 | TestStand/RPA | SelfDiagnostic | 差距等级 |
|---|------|--------------|----------------|---------|
| 7.1 | 执行中暂停/恢复 | 支持 | 不支持 | **中等** |
| 7.2 | 执行中弹窗提示用户操作 (如"请连接设备") | 支持 (Message Popup) | 不支持 | **中等** |
| 7.3 | 可视化流程编辑器 (拖拽式) | 支持 | 仅表格式 Grid 编辑 | **低** |
| 7.4 | Step 执行进度条 / 时间预估 | 支持 | 仅 label 文本 | **低** |
| 7.5 | 多 Runbook 管理 / 切换 | 部分支持 | RunbookProvider 硬编码 default | **低** |

---

## 四、差距严重程度汇总

| 等级 | 数量 | 涉及特性 |
|------|------|---------|
| **严重 (P0)** | 10 项 | 任意方法绑定(1.1, 1.2)、条件分支(2.1)、循环(2.2)、重试(2.3)、数据传递(3.1)、OnFail策略(4.1)、执行时间戳(5.1)、结构化报告(5.2) |
| **中等 (P1)** | 16 项 | 参数映射、实例方法、Native DLL、Goto、并行、子序列、全局变量、表达式引用、类型化参数、Cleanup、日志、历史记录、导出、插件版本、热加载、暂停恢复、用户交互 |
| **低 (P2)** | 9 项 | 脚本语言、Setup/Cleanup序列、断点调试、异常分类、实时事件流、插件沙箱、SDK模板、可视化编辑器、进度条 |

---

## 五、演进路线图 (TODO)

### Phase 1: 核心执行能力 (P0) — 预计 2~3 周

> 目标：达到"可绑定任意 .NET 方法 + 基本流程控制"的能力

- [ ] **TODO-1.1** 通用方法适配器 — `GenericMethodAdapter`
  - 移除对 `[CheckExecutor]` 的强依赖
  - 通过 `BindDll` + `BindMethod` 直接定位 Assembly → Type → Method
  - 通过反射 `MethodInfo.Invoke` 调用任意签名方法
  - 参数从 `step.Params` 自动映射，支持基本类型转换 (string → int/double/bool/enum)
  - 保留 `[CheckExecutor]` 作为可选的元数据标注 (向后兼容)

- [ ] **TODO-1.2** BindDll 驱动的按需加载
  - `DiagnosticEngine` 根据 step.BindDll 按需 `Assembly.LoadFrom`
  - 维护已加载程序集缓存，避免重复加载
  - 支持相对路径 (plugins/ 下) 和绝对路径

- [ ] **TODO-1.3** 流程控制 — Step 级别 `OnFail` 策略
  - `RunbookStepDefinition` 增加 `OnFail` 枚举: `Continue | SkipNext | Abort | Retry | Goto`
  - `RetryCount` + `RetryDelayMs` 字段
  - 执行循环从 for → while + step pointer 状态机

- [ ] **TODO-1.4** 条件执行 — Step `Condition` 表达式
  - `RunbookStepDefinition` 增加 `Condition` 字段
  - 简单表达式求值器: `${STEP_ID.Success}`, `${STEP_ID.Score} > 80`
  - 条件不满足时 skip 该 step

- [ ] **TODO-1.5** Step 间数据传递 — `RunVariables`
  - `DiagnosticRunContext` 增加 `Dictionary<string, object> RunVariables`
  - `CheckExecutionOutcome` 增加 `Dictionary<string, object> Outputs`
  - 执行后自动将 outputs 写入 `RunVariables["STEP_ID.key"]`
  - `step.Params` 支持 `${VAR_NAME}` 模板替换

- [ ] **TODO-1.6** 执行追踪 — `StepExecutionRecord`
  - 记录每个 step 的 StartTime / EndTime / ElapsedMs / Status / Error
  - 聚合为 `RunExecutionReport`
  - 执行完成后自动保存 JSON 报告到 `logs/` 目录

### Phase 2: 增强执行能力 (P1) — 预计 2~3 周

> 目标：接近 OpenTAP / 轻量 TestStand 的执行能力

- [ ] **TODO-2.1** 类型化参数系统
  - `Params` 从 `Dictionary<string,string>` 扩展为 `Dictionary<string, object>`
  - RunbookEditor 根据参数类型提供不同的编辑控件

- [ ] **TODO-2.2** 并行执行组
  - `RunbookStepDefinition` 增加 `ParallelGroupId` 字段
  - 同一 GroupId 的 steps 用 `Task.WhenAll` 并行执行

- [ ] **TODO-2.3** 子序列 / 嵌套 Runbook
  - 特殊 step 类型 `SubRunbook`，引用另一个 .runbook.json
  - 递归执行，支持参数传入/传出

- [ ] **TODO-2.4** Cleanup 序列
  - `RunbookDefinition` 增加 `CleanupSteps` 列表
  - 无论主序列成功失败，Cleanup 始终执行

- [ ] **TODO-2.5** 报告生成器
  - `IReportGenerator` 接口
  - 实现 `JsonReportGenerator`, `HtmlReportGenerator`, `CsvReportGenerator`
  - 报告包含：总体评分、每 step 详情、时间线图、失败项汇总

- [ ] **TODO-2.6** 历史记录与趋势
  - 每次执行结果持久化到本地 SQLite / JSON 文件
  - 支持按日期/工位查询历史
  - 支持同一 step 的趋势对比

- [ ] **TODO-2.7** 执行中暂停/恢复
  - 在 step 间检查 `PauseRequested` 标志
  - UI 增加 Pause/Resume 按钮

- [ ] **TODO-2.8** 用户交互步骤
  - 特殊 step 类型 `UserPrompt`
  - 执行时弹窗等待用户操作/确认/输入

- [ ] **TODO-2.9** 插件热加载
  - `ExecutorRegistry` 改为非 static，支持重建
  - 文件监控 `plugins/` 目录变化，自动重新扫描

- [ ] **TODO-2.10** 多 Runbook 管理
  - `RunbookProvider` 支持列表、切换、加载任意 ID 的 Runbook
  - UI 增加 Runbook 选择下拉框

### Phase 3: 高级特性 (P2) — 按需实施

> 目标：达到生产级 ATE 执行引擎的水准

- [ ] **TODO-3.1** Native DLL 适配器 (P/Invoke)
  - `NativeMethodAdapter` 通过 `DllImport` 或 `Marshal` 动态调用非托管 DLL
  - 适配仪器驱动 (NI-VISA, Keysight, etc.)

- [ ] **TODO-3.2** 脚本步骤适配器
  - `ScriptStepAdapter` 支持内嵌 Python / PowerShell 脚本
  - 脚本通过 stdin/stdout 与引擎交互

- [ ] **TODO-3.3** 可视化流程编辑器
  - 从 Grid 表格式 → 流程图拖拽式编辑器
  - 可视化条件分支、并行路径

- [ ] **TODO-3.4** 断点与单步调试
  - Step 级别设置断点
  - 单步执行 (Step Over / Step Into SubRunbook)

- [ ] **TODO-3.5** 插件沙箱隔离
  - 使用 `AssemblyLoadContext` (.NET Core) 或 `AppDomain` (.NET Framework) 隔离插件
  - 插件崩溃不影响主进程

- [ ] **TODO-3.6** 远程执行 / 分布式
  - 支持将 Runbook 下发到远程工位执行
  - 集中收集执行结果

- [ ] **TODO-3.7** 插件开发 SDK
  - NuGet 包形式的 SDK
  - dotnet new 模板项目
  - 完整的开发文档与示例

---

## 六、Phase 1 核心改造详细设计参考

### 6.1 通用方法适配器 — 核心类设计

```csharp
// 新增: IMethodAdapter 接口
public interface IMethodAdapter
{
    bool CanHandle(RunbookStepDefinition step);
    Task<StepExecutionResult> InvokeAsync(
        RunbookStepDefinition step,
        DiagnosticRunContext context,
        CancellationToken ct);
}

// 新增: 通用 .NET 反射适配器
public class DotNetReflectionAdapter : IMethodAdapter
{
    public bool CanHandle(RunbookStepDefinition step)
        => !string.IsNullOrEmpty(step.BindDll) && !string.IsNullOrEmpty(step.BindMethod);

    public async Task<StepExecutionResult> InvokeAsync(
        RunbookStepDefinition step,
        DiagnosticRunContext context,
        CancellationToken ct)
    {
        // 1. 按需加载 Assembly (step.BindDll)
        // 2. 定位 Type + Method (step.BindMethod = "Namespace.Type.Method")
        // 3. 从 step.Params 映射方法参数 (自动类型转换)
        // 4. MethodInfo.Invoke
        // 5. 适配返回值为 StepExecutionResult
    }
}
```

### 6.2 增强后的 RunbookStepDefinition

```json
{
  "CheckId": "TP_04",
  "DisplayName": "HW/FW 版本符合性",
  "Category": "HwSwFwCheck",
  "BindDll": "SelfDiagnostic.Checks.Tp.dll",
  "BindMethod": "SelfDiagnostic.Services.Executors.Tp.TpCheckExecutors.CheckTpVersionComplianceAsync",
  "TimeoutMs": 7000,
  "Enabled": true,
  "Params": {
    "stationId": "${RunVar.StationId}",
    "lineId": "LINE-001"
  },

  "_comment_below": "=== Phase 1 新增字段 ===",
  "OnFail": "retry",
  "RetryCount": 2,
  "RetryDelayMs": 1000,
  "Condition": "${SYS_01.Success} == true",
  "ParallelGroupId": null
}
```

### 6.3 状态机执行引擎核心循环 (伪代码)

```
stepPointer = 0
while stepPointer < steps.Count:
    step = steps[stepPointer]

    if step.Condition is set and EvaluateCondition(step.Condition, runVariables) == false:
        record Skip
        stepPointer++
        continue

    for attempt = 1 to (step.RetryCount + 1):
        record = StartRecord(step)
        result = await ExecuteStepAsync(step, context, ct)
        FinishRecord(record, result)
        MergeOutputs(result.Outputs → runVariables)

        if result.Success:
            break
        if attempt < step.RetryCount + 1:
            await Task.Delay(step.RetryDelayMs)

    switch step.OnFail when not result.Success:
        case Continue:  stepPointer++
        case SkipNext:  stepPointer += 2
        case Abort:     return AbortResult
        case Goto(id):  stepPointer = FindStepIndex(id)
        default:        stepPointer++
```

---

## 七、关键指标：演进后的目标能力

| 能力 | Phase 1 后 | Phase 2 后 | Phase 3 后 | TestStand 对等 |
|------|-----------|-----------|-----------|---------------|
| 绑定任意 .NET 方法 | 是 | 是 | 是 | 是 |
| 条件/重试/跳转 | 是 | 是 | 是 | 是 |
| 数据传递 | 基础 | 完善 | 完善 | 是 |
| 执行报告 | JSON | JSON+HTML+CSV | 全格式 | 是 |
| 并行执行 | 否 | 是 | 是 | 是 |
| Native DLL | 否 | 否 | 是 | 是 |
| 可视化编排 | 表格 | 表格增强 | 流程图 | 是 |
| 断点调试 | 否 | 否 | 是 | 是 |

**Phase 1 完成后，系统将具备 OpenTAP 约 60% 的核心能力，足以覆盖绝大多数产线诊断场景。**

---

## 八、附录

### A. 对标系统参考文档

| 系统 | 类型 | 官方文档 |
|------|------|---------|
| NI TestStand | ATE 序列执行器 | https://www.ni.com/docs/en-US/bundle/teststand/ |
| OpenTAP | 开源 ATE 框架 | https://opentap.io/docs/ |
| Keysight PathWave | ATE 平台 | https://www.keysight.com/find/pathwave |
| UiPath | RPA 平台 | https://docs.uipath.com/ |
| Automation Anywhere | RPA 平台 | https://docs.automationanywhere.com/ |

### B. 现有代码关键文件索引

| 文件 | 职责 |
|------|------|
| `SelfDiagnostic.Abstractions/Models/RunbookModels.cs` | Runbook + Step 数据模型 |
| `SelfDiagnostic.Abstractions/Models/CheckExecutorInfo.cs` | Executor 元信息 |
| `SelfDiagnostic.Abstractions/Services/Abstractions/CheckExecutorAttribute.cs` | 方法标注 Attribute |
| `SelfDiagnostic.Abstractions/Services/Abstractions/ICheckExecutor.cs` | Executor 接口 |
| `SelfDiagnostic/Services/DiagnosticEngine.cs` | 核心引擎 (发现/注册/执行) |
| `SelfDiagnostic/Services/CheckExecutorRegistry.cs` | Executor 注册表 |
| `SelfDiagnostic/Services/RunbookProvider.cs` | Runbook 加载 |
| `SelfDiagnostic/Services/RunbookFileService.cs` | Runbook 持久化 |
| `SelfDiagnostic/UI/DiagnosticMainControl.cs` | 主诊断界面 |
| `SelfDiagnostic/UI/RunbookEditorForm.cs` | Runbook 编辑器 |
| `SelfDiagnostic.Checks.*/` | 各域插件 DLL |
