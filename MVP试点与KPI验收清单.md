# AutoDiagnosis MVP 试点与KPI验收清单

## 试点工位准备
- 工位类型：`default-station`（可复制为真实工位类型）。
- 工位配置：系统会优先读取 Test Program 的 `config` 目录（`system.xml` + `devicecfg.ini` + `Configure.ini`），并与 `config/baselines/default-station.json` 合并。
- TP 配置根目录默认值：`C:\Users\menghl2\WorkSpace\Projects\Test Program\cal_fts_fvs_fqc\RELEASE\config`。
- 如需切换到其他 TP 目录，设置环境变量 `AUTO_DIAG_TP_CONFIG_ROOT`。
- Runbook：更新 `config/runbooks/default.yaml` 的检查步骤与分支逻辑。
- 凭据：首版使用本地加密配置（当前以占位方式实现，后续替换为正式加密存储）。

## 联调流程
1. 启动 `src/AutoDiagnosis.App`。
2. 输入 `Station ID`、`Station Type`、`Product Family`，角色选择 `Operator`。
3. 点击 `Run Health Check`，确认体检结果与健康分数。
4. 点击 `Run Full Diagnosis`，确认 Runbook 执行路径、失败分支、自愈记录。
5. 在 `artifacts/evidence/` 核对证据 zip、会话目录、审计日志、mock 上传请求。

## MVP KPI 验收项
- 开机体检 <= 10 秒（在目标工位测量）。
- 扫码/触发诊断到结果 <= 20 秒（标准 runbook）。
- 证据包完整度 >= 90%（基线、健康、runbook、截图占位、审计、上传请求）。
- 覆盖典型故障 >= 3 类：
  - 依赖系统不可达（MES/TMS/TAS/File Server）
  - 串口硬件通信失败
  - 必需进程缺失

## 下一阶段（V1）建议
- 对接真实 TP SDK（替换 `TpReadonlyMeasureCheck` 占位逻辑）。
- 接入真实证据上传 API（替换 `MockEvidenceUploader`）。
- 增加 RBAC 写操作白名单（仅 Debugger/Admin）。
- 将依赖系统凭据切换到统一下发 + 本地加密缓存模式。
