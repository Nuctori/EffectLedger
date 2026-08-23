# 三轮独立对抗审计 — 符号良定义性总收口

> 3 轮独立 subagent 对抗审计（视角 A 代数 / B 形式逻辑 / C 工程正确性），各写独立文件、互不读 audit 历史：
>
> - `audit/effect-script-auditR1.md`（code-analysis.ct-expert，代数视角）
> - `audit/effect-script-auditR2.md`（code-analysis.formal-convergence，定义-discharge 视角）
> - `audit/effect-script-auditR3.md`（code-analysis.jeffdean-auditor，工程正确性/fail-open 视角）
>
> 审计对象：`src/Cosmos.EffectAlgebra/EffectScript.cs` + `EffectScriptContract.cs` + `EffectScriptIo.cs`，
> 依赖 L1：`Objects/Numeric/Algebra/SignedNet/DerivedMetrics.cs`。
> 立场：三轮各自「宁误报 open，不漏判」。

## 结论（一轮一句话）

**本子系统「每个符号都被良性定义」= 否。** 三轮独立审计在**两条 HIGH 级阻塞**上完全收敛、互证：

1. `EffectScript.Audit` 的守恒/泄漏核心用 `Merge`（包络）而非 `Add`（带符号求和）——`NegativeDip`/`Leak` **fail-open 静默漏报**；
2. `EffectScriptContract` 与 `EffectScriptIo` 两个 JSON 解析器 **schema 互斥**（资源集/`memory` 归一/budget 形状/claim-scope 来源），且 `Io` 对 `memory` uid 与 budget 有静默误处理。

## 跨轮逐符号良定义性表

| 符号 | R1 | R2 | R3 | 合并判定 | 关键证据 |
| --- | --- | --- | --- | --- | --- |
| `EffectEvent` 4 字段 + 2 构造子 | OK | 是 | OK | **良定义** | 位置记录全必填，数学边界由 L1 承载 |
| `EffectEvent.Scope` | OK/LOW | 部分(LOW) | OK数据/缺陷 | 数据良定义，语义旁支未定义 | JD-2 根因（见 `Audit`） |
| `EffectScript.Events` + 2 构造子 | OK | 是 | OK | **良定义** | |
| `EffectScript.At(t)` | 条件(MED) | 部分(LOW) | OK自身/缺陷 | **自洽但 scope 被改写，与 Audit 矛盾** | `:79` 用 `Combination.Loop(.., e.Scope)` 改写每 claim scope |
| `EffectScript.Audit(Budget)` | **未定义(HIGH)** | **否(HIGH)** | **NO(BLOCKER)** | **未良定义** | gate(1) `:155`、闭包 `:254` 用 `Merge` 非 `Add`（对照 `Algebra.cs:65`） |
| `EffectScript.Budget` / `Audit()` | OK | 是 | OK/缺口(MED) | 数据良定义；仅 Contract 填充 | Io 管线恒 `None` ⇒ gate(2) 空转 (JD-7) |
| `Budget` / `AuditResult` / `Violation` 记录体 | OK | 是 | OK | **良定义** | `Violation.Scope` 在 JD-2 下指向错误作用域（误导，非隔离失败） |
| `EffectScriptContract.Parse` | 条件(MED) | 部分(HIGH/MED) | 部分(MED) | **部分良定义** | `global` scope 因缺 `scene` 不可达（JD-6, `:88-89`） |
| `EffectScriptContract.ToJson` | 条件(MED) | 是(LOW) | OK/缺口(LOW) | 与自身 `Parse` 自洽；与 Io 不互通 | 仅序列化 `OccupyClaims`，丢 read/write (`:181`) |
| `EffectScriptContract.ParseBudget` | 条件(HIGH) | 部分(HIGH) | — | **与 Io 形状冲突** | 扁平 `{key:N}` vs Io `caps[]` |
| `EffectScriptIo.Parse` | 条件(MED) | **否(HIGH)** | **NO(HIGH)** | **未良定义** | `memory→Memory(0)` 清零 uid(`:124`)；资源集与 Contract 互斥 |
| `EffectScriptIo.ParseBudget` | 条件(HIGH) | **否(HIGH)** | 缺口(MED) | **未良定义（脱钩）** | 找不到 `caps` ⇒ **静默返回 `Budget.None`** 不报错 |
| gate(1) 累积 net（NegativeDip） | 未定义(HIGH) | 否(HIGH) | BLOCKER | **未良定义** | `Merge` 吞掉求解和，反例 A/B 漏报 |
| gate(2) 峰值 Peak | 条件(OK) | discharged | OK | **良定义** | `peakSum` 真求和，与 `Peak.Compute` 一致 |
| gate(3) 兼容 Compat | 条件(MED) | discharged | OK数学/设计(MED) | **数学良定义，scope 源矛盾** | 按键 `(res, c.Scope, mode)` 与 `At` 的 `e.Scope` 不一致 (JD-2) |
| 闭包 Leak 块 | 未定义(HIGH) | 否(HIGH) | BLOCKER | **未良定义** | 复用 `Merge` (`:254`) |
| ω=⊤ 居民豁免 | OK | discharged | MED(JD-3) | 部分 | 按 `ω=⊤` 而非 `Hi=⊤` 键控 ⇒ 永久有限ω资源误报 Leak |
| 端点采样 | MED | — | MED(JD-4) | 部分 | `Lo>0` 纯常驻(无有限端点) 退化为 t=0 漏采；闭包纳入未来事件 |
| `ResourceId.Normalize` 幂等 | (未重点) | **是(已证)** | — | **良定义** | `SignalBus bus => bus` 分支真保证幂等 |
| `ScaleSize` ≅ `Combination.Scale` | OK | **是(已证)** | OK | **良定义** | |
| `peakSum` 裸 `ulong*` 回卷 | MED | — | LOW(JD-10) | 风险 | `:171/:185` 绕开 `NatStar` 保守 ⊤ |

## 三轮收敛的 HIGH 阻塞项（必须修）

1. **H-1「Merge 非 Add」**（R1·HIGH / R2·HIGH-1 / R3·JD-1 BLOCKER）
   `EffectScript.cs:155` 与 `:254` 守恒/闭包 net 用 `SignedInterval.Merge`（包络），而 L1 `NetTable.Compute`（`Algebra.cs:65`）刻意用 `Add`（真求和）且其注释警告「Merge 会吞掉守恒」。
   - 反例 Leak：`create[10,10]` + `release[5,5]` ⇒ `Merge=[-5,10]` 含 0 ⇒ 不报；真 `Add=[5,5]` 应报。
   - 反例 NegativeDip：`create[3,3]` + `release[10,10]` 重叠 ⇒ `Merge=[-10,3]` Hi≥0 ⇒ 不报；真 `Add=[-7,-7]` 应报。
   - **修复**：两处 `Merge` 改 `Add`（与 `NetTable` 对齐）+ 反例 A/B 回归测试。

2. **H-2「两解析器 schema 互斥 + Io 静默误处理」**（R1·MED(剖) / R2·HIGH-2/3/4 / R3·JD-8/2/9）
   - `EffectScriptIo.cs:124` `memory→Memory(0)` 无条件清零 uid ⇒ 不同显存块被当同资源，且与 Contract budget key `Memory(uid)` 永不匹配 ⇒ 峰值检查失效（fail-open）。
   - `EffectScriptIo.ParseBudget` 找不到 `caps` 数组 ⇒ **静默返回 `Budget.None`**（不报错）。
   - 资源集：Contract 仅 5 种 vs Io 14 种；`budget` 形状 `{key:N}` vs `{caps:[]}`；claim-scope：Contract 强制 vs Io 用事件 scope。
   - 二者在 `src/` 内**均无调用方**（孤儿代码，R3 已 grep 确认）→ 典型「第二产品」。
   - **修复**：二选一解析器（建议保留 `Contract`，修 JD-6 global 可达、JD-2 scope 统一），砍掉 `EffectScriptIo`。

## 三轮收敛的 MEDIUM 项（建议修）

- **M-1 scope 源矛盾（JD-2 / R2 §1.3）**：`At`(`:79`) 经 `Combination.Loop` 把 claim scope 改写为 `Event.Scope`，`Audit` gate(3)(`:162`) 却用 claim 原 scope ⇒ 同一脚本两视图矛盾；`Contract` 强制 per-claim scope ⇒ 系统性触发。统一为同一 scope 来源。
- **M-2 居民豁免键控（JD-3）**：按 `ω=⊤` 而非 `Hi=⊤` ⇒ `ω=1,Hi=⊤` 永久资源误报 Leak。
- **M-3 采样/闭包越界（R1 反例2 / JD-4）**：`Lo>0` 纯常驻漏采；闭包纳入 `Lo>maxFinite` 的未来事件（当前被 H-1 包络掩盖，修 H-1 后会暴露误报）。
- **M-4 性能契约不符（JD-5）**：注释声称 `O(E·K·log E)`，实测 `AuditAtSample` 每采样点全表扫 `net+grp+caps` ⇒ `O(E²·K)`。对大脚本平方恶化（与 iter-effect26 想消灭的同类问题）。
- **M-5 `EffectScriptContract` `global` scope 不可达（JD-6）**：缺 `scene` 即抛，违反 L1 `ScopeId.Global` 最大元语义。

## 三轮一致认定的「已真 discharge（良定义）」

`ResourceId.Normalize` 幂等性、`ScaleSize`≅`Combination.Scale`、ω=⊤ 居民层豁免（gate1/2 同一定义）、gate(2) 峰值求和、gate(3) 兼容分组数学、所有 leaf 记录体（`EffectEvent` 字段/ctor、`Budget`、`AuditResult`、`Violation`）、`Alive`/`ScaleSize`/`ToZ`/`Negate`、扫换线 enter/exit 时序。

## 修复优先级建议（来自 R3）

1. `EffectScript.cs:155`/`:254` `Merge`→`Add`（配反例回归）。—— 否则「没跑游戏就审计」核心卖点失效。
2. 统一 `At` 与 `Audit` gate(3) 的 scope 来源。
3. 削掉 `EffectScriptIo.cs`（第二产品），保留并修 `EffectScriptContract`。

---
*本文仅汇总 3 份独立审计报告，未改动任何 .cs 源码。三轮均严格未读 `audit/` 历史，判定互证。*
