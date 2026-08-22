# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立对抗性数学家 subagent（第 1/3 轮）。任务：对 `D:/Godot/Cosmos/EFFECT_SCRIPT.md` 做**代数对抗性审计**，目标是找出设计中的数学漏洞/不成立断言/边界误判。你不是实现者，是审查者。

**只读（不要写代码）：**
- `D:/Godot/Cosmos/EFFECT_SCRIPT.md`（待审设计）
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/{Numeric,Objects,Algebra,SignedNet,DerivedMetrics}.cs`（L1 真实 API，核对设计援引是否准确）
- `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（§3.1 / §3.2 / §3.3 数学定义，核对设计声称）
- `D:/Godot/Cosmos/LANDING_PLAN.md`（§3 架构约束）

**对抗性焦点（逐项质问，找反例）：**
1. **端点采样完备性定理**（§3）：`At(t)` 真的是「仅在区间端点改变」的分段常数吗？`LoopCount.Of(ω)` 的 `Combination.Loop` 把 size 乘 ω，但 lifetime 仍是原区间——多个不同 ω 的 Event 叠加，Union 是否仍分段常数？端点是否仍是 lo/hi？有没有采样点之间出现「内部突变」的情况（例如两个区间 [a,b]、[c,d] 的并，采样必须含 a,b,c,d 全部，是否遗漏 b/c 之间的内部点？）。给出严格反例或证明。
2. **`At(t)` 的 t 域**：设计把 `Lifetime` 当 `[NatStar, NatStar]`，但 `At(NatStar t)` 用 `Lo ≤ t ≤ Hi`。然而 `Signature` / `Peak` / `Net` 的 scope 过滤用 `ScopeId.IncludedIn`——`EffectEvent` 没显式 scope，`At` 内部用什么 scope？`Combination.Loop(body, ω, loopScope)` 的 loopScope 从哪来？若 Event 不携带 scope，跨 Event 的 `Union` 后 `Net(sig, scope)` 的 scope 参数从哪传？找出 scope 语义缺口。
3. **守恒判定的时间窗口**：`IsConserved` 是**瞬时** net 含 0。但一个资源的 create 在 t1、release 在 t2（t1<t2），在 (t1,t2) 之间 `Net` 含 create 不含 release ⇒ `IsConserved=false`。这是否会把「合法临时占用」误判为泄漏？设计 §3.1 的「逐采样点 IsConserved」是否对瞬时 net 过度严格？给出修正方案或证明设计已处理。
4. **`Budget` 软约束的 ⊤ 交互**：`Peak(At(t),scope)` 若含 ω=⊤ 的 Event ⇒ ⊤（§3.3.2）。`Budget.Caps[r]` 是有限值 ⇒ `⊤ ≤ finite` 经 `CompareToFinite` 返回 ⊤ 更大 ⇒ 超限。这是否导致**任何**含 ω=⊤ 的剧本（如常驻背景粒子）必被 `Audit` 拒？与 §2.1「ω=⊤ 常驻背景层」的「正确标为非守恒」是否矛盾（守恒与峰值预算是两套检查，需分别论证）？
5. **`Compatible` 在剧本层的使用**：§3.1 说「同 scope 兄弟 Claim `Compatible.IsCompatible` 全通过」。但 `Compatible` 只接受两个 `Mode`，不感知 scope/资源。`At(t)` 内同资源同 scope 的多个 Claim 如何枚举成对检查？`create+create` 冲突是否只对「同资源」成立（设计 §3.1 表格写「同资源两 create 冲突」但正文只说「同 scope 兄弟」）？找出歧义。
6. **确定性声称**：§5 称 `At(t)` 纯函数 ⇒ 同剧本同 t 同签名。但 `ImmutableArray` 枚举顺序不影响 `Signature`（基于 `ImmutableHashSet`）⇒ 真成立。请确认 `Signature.Union` 的 `ImmutableHashSet` 是否真无序可交换（读 Objects.cs 验证），否则确定性不成立。

**产出 `D:/Godot/Cosmos/audit/effect-script-auditA.md`（严格对抗格式）：**
```
# 对抗审计 A
## 逐条焦点结论（# 焦点 | 成立/缺口 | 数学论证/反例（含文件:行））
## 确定性（焦点6）：是否成立
## 开放项（OPEN-#，每条：数学描述 + 反例 + 严重度 + 最小修正）
## 总评：设计是否代数上成立（可关闭 / 需修订）
```
独立判断，宁可过度质疑。若发现真缺口 ⇒ 列最小修正（不改代码，只描述）。完成后回复一行：auditA.md 已写入；结论=？（成立/需修订 N 项 open）。

## Acceptance Contract
Acceptance level: checked
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope
- criterion-2: Return evidence sufficient for an independent acceptance review

Required evidence: changed-files, tests-added, commands-run, residual-risks, no-staged-files

Review gate: required by reviewer.

Finish with a fenced JSON block tagged `acceptance-report` in this shape:
Use empty arrays when no items apply; array fields contain strings unless object entries are shown.
`criteriaSatisfied[].status` must be exactly one of: satisfied, not-satisfied, not-applicable.
`commandsRun[].result` must be exactly one of: passed, failed, not-run.
`manualNotes` and `notes` are optional strings; an empty string means no note and does not satisfy `manual-notes` evidence.
```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "specific proof"
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "specific proof"
    }
  ],
  "changedFiles": [
    "src/file.ts"
  ],
  "testsAddedOrUpdated": [
    "test/file.test.ts"
  ],
  "commandsRun": [
    {
      "command": "command",
      "result": "passed",
      "summary": "short result"
    }
  ],
  "validationOutput": [
    "validation output or concise summary"
  ],
  "residualRisks": [
    "none"
  ],
  "noStagedFiles": true,
  "diffSummary": "short description of the diff",
  "reviewFindings": [
    "blocker: file.ts:12 - issue found, or no blockers"
  ],
  "manualNotes": "anything else the parent should know"
}
```