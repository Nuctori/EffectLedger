# Rich Hickey 视角 — Round 07 文档示例可运行性（doc 是第一用户输入）

> 审计员：hickey-auditor（subagent 后台持续 503/失败，本地直发；前 6 轮修复面 + 综合表 + 当前 doc 实测）· 透镜：doc 即可执行 — "用户复制文档示例跑 = 第一次接触" · 轮次：R7/10
> 判据：doc 是用户的第一次 contact，所有示例必须是可证伪的——"文档即测试"是诚实性而非装饰。

## 核实矩阵（R1-R6 修复面 + synthesis）

| 历史项 | 来源 | 本轮裁决 | 行号证据 |
| --- | --- | --- | --- |
| EFFECT_SCRIPT.md §4 旗舰示例 Parse 失败 | R1 F1 / f378b1d | **已修** | `EffectScriptContract.cs:75-86` ParseEvent 要求事件级 scope；EFFECT_SCRIPT.md:138-149 文档已对齐（含事件级 scope） |
| 事件/claim 层未知键白名单 | R1 F2 / f378b1d | **已修** | `EffectScriptContract.cs:80` 事件层 + `:165` claim 层；EFFECT_SCRIPT.md §4 文档已与白名单一致 |
| R6 S06-001 claim scope=event scope 校验 | R6 / 90b3be8 | **已修** | `EffectScriptContract.cs:160-164` 单一真相校验；文档示例各 claim scope=event scope 正确 |
| R6 S06-002 Budget 归一化 | R6 / 90b3be8 | **已修** | `EffectScript.cs:357-365` 构造期归一键 |
| EFFECT_SCRIPT.md §3.1.5a 缺省 | synthesis K (锁死) | **锁死** | 文档明示"Size 缺省 = [1,1]（精确 1）"，与代码一致；本轮检查示例是否一致 |
| R5 V5-001 Sequence/Join L1 警告 | R5 / e0b966b | **已修** | `DerivedMetrics.cs:67-70` Sequence + `:72-75` Parallel + `Objects.cs:207` Join 含 L1 警告 |
| README §4 文档示例 | 本轮 R7 / 90b3be8 | **本轮修复** | 见新发现 D07-001/002/003/004 |

## 新发现（doc-as-input 透镜）

### D07-001 — MED — `EFFECT_SCRIPT.md` 旗舰示例与 README 重写示例的"看似可跑"漏洞：放在测试夹具里却未被任何 test 守护

- **位置**：`EFFECT_SCRIPT.md:138-149` JSON + `README.md:84-99` C# 示例 + `tests/Cosmos.EffectAlgebra.Tests/Round1Hickey2Tests.cs:36-50` 已钉 §4 doc
- **判词**：把"文档示例可跑"寄望于 Round1 唯一测试——若 EFFECT_SCRIPT.md §4 改一字（即使新内容合法），该测试立刻红；但 README §4 演示（90b3be8 新写）无任何测试守护，相当于"用户复制 README 跑 = 第一次接触"无护城河。
- **证据**：`tests/Cosmos.EffectAlgebra.Tests/Round1Hickey2Tests.cs:35-50` `DocSection4_ExampleParses` 与 `DocSection4_RoundTrip_Idempotent` 用 `ExtractSection4Json` 读 EFFECT_SCRIPT.md §4；README §4 演示无钉。
- **最小修复**：在 Round1 测试中加一个 `Readme_Example_ParsesAndAudits`：把 README §4 那段 C#（含 JSON 内联串）抽成 `ExtractReadmeSection4Json` 工具方法，对读到的 JSON 走 Parse + Audit，断言 Passed=true 且无违例——使"用户复制 README 跑"路径完整守护。
- **severity**：MED
- **testHint**：`var json = ExtractReadmeSection4Json(); var s = EffectScriptContract.Parse(json); var aud = s.Audit(s.Budget); Assert.True(aud.Passed);`
- **verdict**：fixable

### D07-002 — MED — `EFFECT_SCRIPT.md` §4 示例的 resource 形状 `{"gpu": "mesh1"}` 与 code comment 注释 `{"gpu": {"bufferId":"mesh1"}}` 仍在不同位置并存，新人易混

- **位置**：`EFFECT_SCRIPT.md:140-148` 文档示例用扁平字符串形态（与 Parse 路径 `Rid(ReqStr(gpu))` 一致） + `EffectScript.cs` 头部 `## 2.1` 注释或老 doc 仍写 `{"bufferId":"mesh1"}` 形态
- **判词**：让"扁平字符串"是契约而"嵌套对象"是注释——是让"用户的肉眼"读两套说法。
- **证据**：实际探针在 90b3be8 后 `Parse(扁平)` ✓，`Parse(嵌套)` 抛 `FormatException`。
- **最小修复**：在 EFFECT_SCRIPT.md §4 顶部加显式 "resource 必须是扁平字符串形态：`{"gpu":"mesh1"}` 而非 `{"gpu":{"bufferId":"mesh1"}}`——后者是历史契约（R1 前的 spec-drift）已废弃。" 防止新 doc 误抄。
- **severity**：MED — 文档 vs 文档分裂
- **testHint**：`var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse("""{"events":[{"lifetime":[0,1],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"gpu":{"bufferId":"m"}},"mode":"use","scope":{"scene":"S"}}]}]}""")); Assert.Contains("resource",ex.Message);`
- **verdict**：fixable

### D07-003 — LOW — `DELIVERABLE.md`/`PDR_Effect_Cost_Algebra_v3_FINAL.md` 中提到的"可证伪测试编号"（如 T-#N）未与 tests/ 中命名一致，新人难对位

- **位置**：`DELIVERABLE.md` 提及的 `T-001`/`T-002` 等编号与 tests/* 测试方法名（`*Tests.cs` 中 `Method_Scenario`）未做映射表
- **判词**：让"交付编号"是项目自语言，让"测试名"是 xUnit 自语言，二者无映射——是让"审计/交付"与"测试"两套索引在"理解项目全貌"时各说各话。
- **最小修复**：在 `tests/` 顶部加 `tests/T-MAP.md` 一表：左列交付编号（T-001…），右列 `[Class.Method]`，CI 时检验同名/不存在时红。属加法性投资。
- **severity**：LOW
- **testHint**：n/a
- **verdict**：fixable

### D07-004 — MED — README §4 演示 `var at5 = script.At(NatStar.Of(5));` 后未断言语义；用户复制跑只能看到 `at5.OccupyClaims.First().Scope`，无法对"是否被 budget 抑制"形成闭环

- **位置**：`README.md:97` 演示仅 `if (!audit.Passed) Console.WriteLine(...)` 提示打印违例，未给"Passed==true 的不可变断言"
- **判词**：让"用户读 README 复制跑"的第一次接触是"看打印"，而不是"看断言"——是错过"doc 即测试"的最佳时机。
- **最小修复**：在 README §4 演示下方加 "**单点投影 vs 全集审计**" 一段：`Assert.Equal(1, at5.OccupyClaims.Count());` 与 `Assert.True(audit.Passed); Assert.Equal(1, audit.CapsChecked);`——把"doc 演示"升级为"可剪贴运行的 xUnit 用法"。
- **severity**：MED
- **testHint**：与 D07-001 同型测试覆盖 README §4 演示
- **verdict**：fixable

### D07-005 — LOW — `EffectScriptContract` XML doc 注释中的"四参位置记录"等位置参数描述与 EFFECT_SCRIPT.md §3.1.1 措辞略不同步：`"四参位置记录"`在 EFFECT_SCRIPT.md 写作"五参位置记录"

- **位置**：`EffectScript.cs:119-127` Claim 写"五参位置记录"（kind/resource/mode/scope/size），但 `Objects.cs` 实际位置 record 5 字段与 §3.1.1 标号一致——疑似无差异，但 EFFECT_SCRIPT.md §3.1.1 描述写"`(kind, resource, mode, scope, size)` 五元组"——一致，无 bug。
- **判词**：经人工核查无误；保留 LOW 仅为防"未来改动"漂移。
- **最小修复**：保持现状 + 在 `tests/` 加 `PublicApi_DocSays_5Tuple_Not4` 钉一致。
- **severity**：LOW — 锐边留档
- **verdict**：already-fixed（无差异） + doc-only

### D07-006 — LOW — `Budget` 默认值 `new Budget()`（`default(Budget)`）在 EFFECT_SCRIPT.md §3 中说"未设 cap = 通过"，但未在主文档明确"调用方应检查 `IsPeakChecked`"（R4 引入）

- **位置**：`EFFECT_SCRIPT.md §3.2/§3.3` 说峰值门语义，但 `README.md` 提"Passed=true"却不强调"零预算时 `IsPeakChecked==false`"
- **判词**：让"零预算过 = 全绿"在 doc 上等同于"有预算过 = 全绿"——是 R4 引入 `IsPeakChecked` 派生属性后未在 doc 同步诚实化。
- **最小修复**：在 README "能做什么" 表"峰值预算"行加 `**注意**：Budget.None 时 gate(2) 未运行，`IsPeakChecked==false`（R4 引入）——别把"没查"当"全绿"。EFFECT_SCRIPT.md §3.3 同型。
- **severity**：LOW
- **testHint**：已有（R4 测试）。
- **verdict**：fixable

## TOP-3（本轮最值得修）

1. **D07-001** — README §4 演示无可证伪守护（"用户复制 README 跑"=第一次接触无护城河）
2. **D07-002** — EFFECT_SCRIPT.md §4 扁平 vs 嵌套 resource 形态的 doc 自分裂
3. **D07-004** — README §4 演示只有"打印"无"断言"，错过"doc 即测试"升级时机

## 证据清单

- `tests/Cosmos.EffectAlgebra.Tests/Round1Hickey2Tests.cs:35-50` DocSection4 测试钉
- `EFFECT_SCRIPT.md:138-149` §4 JSON 旗舰示例
- `README.md:84-99` 90b3be8 §4 演示（与 EFFECT_SCRIPT.md 措辞）
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` 全文
- `src/Cosmos.EffectAlgebra/EffectScript.cs:357-365` Budget 归一 + `:341-346` IsPeakChecked 派生
- `audit/rich-hickey-round10-synthesis.md` 锁死项
- `audit/rich-hickey2-round01..06-*.md` 6 轮修复面核验
- `git log --oneline -7` 与 `git show --stat HEAD~5..HEAD`
