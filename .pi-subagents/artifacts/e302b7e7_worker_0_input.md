# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代01 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：**
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Numeric.cs`
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Objects.cs`
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Algebra.cs`
- 对照参考：`D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` 的 §3.1（L79 起）、§3.2（L247 起）、§3.3（L293 起）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**用户铁律（审计标准）：** 每个符号都要有明确数学边界定义 + 明确语义；类型系统能约束的用类型，类型约束不了的写在注释上（带 §x.y 出处）。审计就是核对这条有没有真做到，且类型是否真的「约束」了声称的边界（不靠注释吹）。

**审计目标（逐条，回指代码行号）：**
1. `NatStar`（§3.1.5a）：运算符是否真内嵌 ⊤ 律（加/乘/Max/Min），还是只写在注释里没进类型？`0×⊤=⊤` 是否真实现？`CompareToFinite` 是否真先判 IsTop？
2. `Interval`（§3.1.5/§3.1.5b）：构造子 lo≤hi 校验是否真存在？`Default`/`Dynamic`/`Exact` 值是否正确？`Merge` 是否真用 Min/Max（内嵌 ⊤ 律）？
3. `DeviationVal`（§3.1.5c）：`ExceedsThreshold` 是否真先判 IsTop 再比数值（§9.1 修正）？
4. `ResourceId` 判别联合 + `Normalize`（§3.1.2/§3.1.4a）：结构相等是否真给「构造子标签+字段」？`Normalize` 是否真实现 signal_bus/"signal_"+s/Self("signal_"+s)⇒SignalBus(s) 与 gpu/command_buffer⇒CommandBuffer("gpu")？注释映射表是否完整？
5. `ScopeId.IncludedIn`（§3.1.3b）：是否真实现 ⊆*（自反 + Global 最大元 + 跨标签 false）？还是只注释？
6. `Claim.Normalize`（§3.1.1/§3.1.4a）：是否真归一 resource + 缺省 size⇒Default？
7. `Signature`（§3.1.4b）：三桶是否真不相交（类型层）？`Union` 是否真按 Normalize 去重（幂等）？
8. `Compatible`（§3.2.3）：是否真全函数 + 对称？Unknown⇒Use？CONFLICT 集是否真排除 (C,C)/(M,M)/(R,R)？
9. `Weight`（§3.3.2b）：跨 kind 是否真返回 NaN（⊥ 编码）？注释是否声明这是 ⊥ 约定？
10. `NetTable`（§3.3.1）：`Compute` 是否真按 ⊆* 过滤 + 归一资源分组 + release 取负向？`IsConserved` 守恒逻辑是否正确（缺省 [1,1]⇒不守恒）？
11. `Peak`（§3.3.2/§3.2.5）：是否真 size 求和 + 任一 ⊤⇒⊤（旧 cardinality 形式是否已废弃）？
12. **注释完整性**：每个公共类型/运算符是否有 (§x.y) 出处注释 + 一句不变式？有无「裸类型无注释」或「注释声称已约束但代码没做」的虚假收敛？
13. **L1 零 Godot 依赖**：文件是否真无 `using Godot`、`Rid`/`StringName` 是否为内部原语（非 Godot 命名空间）？

**产出 `D:/Godot/Cosmos/audit/iter-code01.md`（严格）：**
```
# 迭代01 审计（L1 代数核心）
## 摘要
- 构建状态：0 错误 0 警告（已核实 dotnet build）
- open 项总数：X（可闭 K / 设计层 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐符号核对（每条回指代码行号 + PDR § + 结论）
| 符号 | 代码行 | PDR § | 类型是否真约束 | 注释是否完整 | 结论(OK/OPEN) |
...
## open 项清单（若有）
| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
## 结论
```
**要求：** 每条回指真实行号（先 read 确认）。独立判断，不信任任何历史审计。若全文无 open（仅剩后续迭代才做的内容，如 L2/L3/测试，属 out-of-scope 不计入）则写「可终止」。

完成后回复：iter-code01.md 已写入；open 项 X（可闭 K/设计 M）；终止判定=？（一行）。

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