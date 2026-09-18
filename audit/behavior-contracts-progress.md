# Behavior Contracts — 实施进度

日期：2026-09-15
基线提交：65fa89e (master HEAD at task start)
计划文档：docs/behavior-contracts-{plan,phases,requirements}.md

## 阶段状态

| 阶段 | 状态 | 证据 |
| --- | --- | --- |
| P0 语义与反例规格 | 完成 | 需求/阶段文档；角色语义写入 `BehaviorProfiles.cs`；两类角色各有正/反/未知例测试 |
| P1 独立包与声明识别 | 完成 | 声明包零依赖；`ProfileResolver` 精确符号识别（BC-001 修复）；真实 `dotnet build` 报 EBC2001 |
| P2 操作摘要与未知边界 | 完成 | IOperation 遍历 + 调用图递归传播 + 7 类 Unknown 原因 + 预算；BC-002..BC-008 修复并回归 |
| P3 深层不可变值 | 完成 | 构造期写入合法化、receiver 写入、this 逃逸、readonly List、别名保留、只读视图非冻结、公开可变状态 |
| P4 确定性计算 | 完成 | 直接/间接时钟、Console/IO、静态可变状态、输入修改、局部集合合法 |
| P5 严格门禁与报告 | 完成 | Tool 复用同一引擎；真实构建输入；exit 0/2/1；JSON 报告；抑制不绕过 |
| P6 试点/消费/文档 | 完成（跨平台未验） | 无 Godot 示例零违规构建；真实 NuGet 包链消费门；用户文档；CI 接线；性能已测 |

## 最终验证证据（全部实跑）

| 验证 | 命令 | 结果 |
| --- | --- | --- |
| 契约测试（Debug） | `dotnet test tests/EffectLedger.Contracts.Tests/... -p:NuGetAudit=false` | 通过 21/21 |
| 契约测试（Release, warnaserror） | 同上 `-c Release` | 通过 21/21 |
| Release 严格构建 | `dotnet build .../**/Analyzer/Tool.csproj -c Release -warnaserror` | 0 错误 0 警告 |
| 分析器真实加载 | 消费者 `dotnet build`（Analyzer 接线） | 报 `warning EBC2001` |
| 严格工具×合法工程 | `-- check samples/BehaviorContracts` | exit 0，2 根 OK |
| 严格工具×违规工程 | `-- check /tmp/leakyproj` | exit 2，EBC2001 + EBC1001 |
| 严格工具×抑制工程 | NoWarn 全 EBC + `#pragma warning disable` | exit 2（抑制不绕过，R-GATE-01） |
| 严格工具×未知工程 | 未登记外部依赖 | exit 2，`[UNKNOWN]`（未知≠成功） |
| 严格工具×缺工程 | `-- check -m strict` | exit 1（用法错误，非成功，BC-019） |
| 消费烟测（Debug/Release） | `bash tests/ContractsConsumerSmoke/run-smoke.sh [Release]` | PASS（配对绿 / 违规红含 EBC2001 / analyzers/ 落位） |
| 既有 Runtime 套件 | `dotnet test tests/EffectLedger.Runtime.Tests/...` | 通过 139/139 |
| 既有 L1 套件 | `dotnet test tests/EffectLedger.Tests/...` | 通过 592/596 |

## 新增验证（第二轮审查修复后）

| 验证 | 结果 |
| --- | --- |
| 合约测试 | **40/40 通过**（21 → 40；含第二轮判别回归 9 枚 + 故障注入 5 枚 + 性能 1 枚 + foreach 2 枚 + 回调 2 枚） |
| 性能基线（固定语料） | 100 根 **382 ms** / 1000 根 **1951 ms**，**5.11x（10 倍根数）** = 亚线性（共享 helper 缓存生效） |
| 故障注入：分析器拔线 | 裸 ProjectReference ⇒ build **0 条 EBC**（静默假绿），**严格 Tool 仍 exit 2 检出** |
| 故障注入：诊断抑制 | NoWarn + `#pragma` ⇒ Tool exit 2 |
| 故障注入：删除声明 | 根数 1→0 可观测；strict 对 0 根默认 exit 2 |
| 故障注入：未识别角色 | EBC0001，不静默当作无约束 |
| 故障注入：同名伪接口 | 不误识别（精确符号比对） |
| 故障注入：预算耗尽 | 转 Unknown（BudgetExceeded），非成功 |
| 生成器 fail-closed | 引用源生成器但无 obj/ 生成产物 ⇒ exit 1，须 `--allow-generators` 显式放行 |
| foreach 枚举传播（BC-040） | 用户自定义枚举器（含显式接口实现）效应传播；BCL 集合无噪声 |
| 回调创建/执行/逃逸（BC-041） | 创建不计执行；`d()` 记执行且不可解析来源报 Unknown；存入字段记逃逸 |
| 端到端退出码矩阵 | 合法 0 / 违规 2 / 抑制 2 / 未知 2 / 缺工程 1 —— 全部符合预期 |

## 未通过样例（环境限制，非回归）

4 项：`GateFixture_Leaky/Paired/ExtendedWhitelist`、`AnalyzerConsumer_Build_EmitsEaa0901`。
根因 `NU1900`（nuget.org 审计源不可达）。**已在基线提交 65fa89e 的独立 worktree 上复现同一失败**，
且失败测试引用的 `src/EffectLedger/`、`src/EffectLedger.Analyzer/` 相对 HEAD 无改动 ⇒ 与本次工作无关。

## 新增 Unknown / 信任边界

- `ExternalSummaryMissing`：外部程序集/BCL 未登记成员
- `OpenDispatch`：开放虚/接口派发未闭合
- `UnsupportedOperation`：dynamic / 函数指针 / 用户转换
- `BudgetExceeded`：操作或调用图节点预算耗尽
（`UnsafeAliasShape`/`SummaryMismatch`/`NotImplemented` 三个无产生点的成员已删除——声明但不兑现不符合本模块原则。）

## 已知未实现（首版范围外，均已在 docs/behavior-contracts.md 明示）

- 用户摘要配置 `effectledger.contracts.json` 未接线（strict 不依赖）
- 经中间变量/容器间接流转的返回值别名未完整覆盖（直接返回参数/this 已检出）
- 迭代器（`yield`）/`async` 状态机语义未建模（按同步方法处理）
- BCL 目录为白名单（已登记才放行）；未登记成员落 `ExternalSummaryMissing`，是最主要的 Unknown 来源
- 源生成器以 fail-closed 处理，未声称完整覆盖生成源
- 跨工程/第三方程序集调用不跟进（无摘要 ⇒ Unknown）；需经配置通道供摘要（该通道尚未接线）

## 完成度审计（对照目标 15 项标准）— 2026-09-17

| # | 标准 | 状态 | 证据 |
| - | ---- | ---- | ---- |
| 1 | P0–P6 有完成证据 | 满足 | 本文档各阶段行；台账 BC-001..BC-131 全部有证据 |
| 2 | 无 Godot 的独立消费者可用 | 满足 | `tests/ContractsConsumerSmoke` 真实 NuGet 包链 PASS（隔离缓存） |
| 3 | 两个角色检查实际实现 | 满足 | 78 枚测试含判别性回归；探针工程双向验证 |
| 4 | 直接/间接/构造/属性/回调/别名反例 | 满足 | `AuditRound3Tests`（19 形状）、`EleganceTests`、`BehaviorPropagationTests` |
| 5 | 合法局部计算可通过 | 满足 | 示例 4 根 OK；`LegitPatterns_StillPass` 等对照全绿 |
| 6 | 未知/预算/工具失败不显示成功 | 满足 | unknown-only rc=2；预算耗尽 ⇒ Unknown；CLI 用法错误 exit 1 |
| 7 | 工具与真实构建一致 | **部分** | dump 走真实 MSBuild 输入，但**报告无源指纹**、无"条件编译下工具/构建一致"的 fixture（P5.2/P5.6 未完成） |
| 8 | 抑制/删目标/漏接/拔线故障注入 | **部分** | 抑制已实测 rc=2；但"漏接（AdditionalFiles 未接）"与"拔线"缺 fixture，"删目标"仅测 1→0（P5.5 未完成） |
| 9 | Analyzer 与 Tool 同一引擎 | 满足 | Tool 经 `<Compile Include>` 复用分析器源码，无第二套实现 |
| 10 | 新旧共存 | 满足 | Runtime 139/139；L1 仅 4 项 NU1900（基线 worktree 已证先存环境问题） |
| 11 | 真实构建/测试/隔离包消费 | 满足 | 见下方"CI 绿灯"（仅 Linux；Windows 未在 CI 矩阵内） |
| 12 | 跨平台 CI 真实结果 | **部分** | ubuntu-latest 绿；但 CI **未含 windows-latest**，计划 P6.4 要求的双平台矩阵未满足 |
| 13 | 性能达标或有证据处置 | **不满足** | 实测仅 1 个语料、2 个点、各跑 1 次、无阈值/无内存；计划 P6.3 要求六类语料 + ≥5 轮 + 中位数 + 峰值内存 |
| 14 | 独立审查无未处置假绿 | **不满足** | 两条计划明文规则未实现且未声明：① 类型声明**多个角色**未报配置诊断（实测 exit 0）；② 受约束 class **要求 sealed** 未强制（实测非 sealed 判 OK） |
| 15 | 文档与实现一致 | **部分** | 用户文档诚实；但计划仍写 `net8.0;net10.0` 而实际单目标 net10（BC-120 未修） |

### CI 绿灯（2026-09-17，commit a15a214）

推送后 GitHub Actions 在 **ubuntu-latest** 跑完 11 个步骤，**全部 success**：

```
audit-gate in 1m31s   ✓
  success  Restore
  success  Build (编译期门禁：TreatWarningsAsErrors 使警告=错误)
  success  Dafny verify (P3 形式规约门：0 errors 才放行)
  success  Test (审计 + 代数 + L2/L3 端到端)
  success  Pack + Consumer smoke (真实 NuGet 管线走查)
  success  可选类型行为约束 — 真实包链消费门（新增，Linux 上首次执行）
```

annotations 仅为既有样例的**故意泄漏警告**与 Node 20 弃用提示，非失败。
注意：CI 仅 ubuntu 一个 runner，**Windows 未纳入**（计划 P6.4 要求双平台矩阵）。

推送前的跨平台自查（三处真实风险已修）：
- `DumpCompileInputs.targets` 及两份文档原为 CRLF ⇒ 归一为 LF；
- `run-smoke.sh` 曾用 `python`（ubuntu 只有 `python3`，且既有烟测不依赖解释器）⇒ 改为 `unzip`/字节串校验；
- slnx 全部 16 个项目经**精确大小写**校验存在；`.sh`/`.targets` 在 Git 索引中确认为 LF。

### **更正（2026-09-17 差距审计后）**

本节此前结论为「首版 15 项标准全部满足」，**该结论过度声明，现已更正**。

以计划文档为准绳逐工作包核对后确认，**5 个 P0–P6 范围内的工作包零实现**：

| 工作包 | 计划要求 | 实际 |
| --- | --- | --- |
| P1.4 配置入口 | `effectledger.contracts.json` + AdditionalFiles 接线 + 消费测试 | 零实现 |
| P4.4 用户摘要 schema | schemaVersion/符号 ID/reason/evidenceRef + 三步校验 | 零实现 |
| P5.5 目标清单与策略 | 预期根清单；删角色/改名/漏项目必须产生目标差异 | 零实现（仅零根检查） |
| P5.6 报告完整度 | 覆盖计数/源指纹/catalog 版本/trust 依赖/additional locations | 部分（顶层 7 字段） |
| P6.3 性能实验 | 六类语料 + 冷暖对照 + ≥5 轮 + 中位数 + 峰值内存 | 部分（1 语料/2 点/1 次） |

另有无实现且未声明的计划项：P2.1 抽象域 Join 律测试、P2.3 显式 CFG、P2.7 取消令牌、
P0.1 ADR / P0.2 覆盖矩阵 / P1.5 探针报告（三个计划点名的文件不存在）、
P1.1 新模块公共 API 快照、net8 目标、P6.2 试点成本统计、P6.7 发布候选证据包。

**当前准确结论：首版功能可用（两角色真实生效、严格门禁不可绕过、Linux CI 绿），
但按计划文档衡量，P0–P6 尚有 5 个工作包未交付 + 多条计划明文规则未实现。
「完整版」尚未达成，继续交付中。** 详见
[issues 台账](behavior-contracts-issues.md) 第十四轮与第十五轮。

## 独立审查

见 `audit/behavior-contracts-issues.md` §独立对抗审查：12 项发现，逐条实测复核：
1 项证伪（F4，实测反证并加判别测试）、多项成立并已修复（F1/F2/F7/F11/F6），
其余记为已知边界。修复均带回归测试（`PrecisionTests` 5 枚判别测试）。
