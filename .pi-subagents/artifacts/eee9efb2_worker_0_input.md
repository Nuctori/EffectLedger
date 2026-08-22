# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计员，做最终终止判定审计。任务：复审计 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（当前版本 v3.0-FINAL-rA5）。

**硬性约束（违反即作废）：**
1. 只读 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` 这一个文件。绝对禁止读取 `D:/Godot/Cosmos/audit/` 目录下任何文件（iter01-iter52、stability01、README）。必须独立判断。
2. 只产出你自己的 `D:/Godot/Cosmos/audit/iter53.md`，不得修改 PDR 或其他文件。

**背景（不要信任其结论，仅告知）：** 经过 iter51（rA3 上 8 open）→ rA4（父进程闭合 8 项）→ iter52（复核称 6/8 闭，报 #1 双标题未落盘 + #5 术语表重复 + #N1 历史虚假收敛 共 3 open）→ rA5（父进程用 ctx_edit 真正删 `## 15.文档历史`，并复核 #5/#N1 为误报：术语表现仅一行 `**PDR**`）。现在请你独立验证当前 rA5 全文是否还有任何 open 项。

**审计目标：**
1. 全文档独立扫描，找出一切「悬空/未定义/逃逸通道/自相矛盾/不可机械执行/虚假收敛」项。重点：
   - 文档结构：是否还有重复标题、章节号倒挂、`## 15` 之类残留（grep `## 15` 应零命中）。
   - 术语表（§13）：`**PDR**` 是否仅一行、有无其他重复词条。
   - §3.1.5c DeviationVal 定义体是否真实存在且与 §9.1 引用对齐。
   - §3.1.2 构造子（Occupancy/Callback/Input）与 §3.1.4a 裸名映射表是否都在。
   - §9.1 代码 `if (deviation > 0.2f)` 是否已消除（应改为先判 ⊤ 再比 double）。
   - §3.2.5 Peak 两式是否只剩 §3.3.2 的 size-求和为准。
   - §8.1 release-class 清单与 §7 映射表是否一致。
   - §3.4 MA 栏（107 条）、§12.2、§14 测试矩阵：行号引用是否真实、有无悬空、判据是否可证。
   - 全文 `TODO|FIXME|悬空|逃逸|留口|依赖实现` 命中是否均为「已收口叙述」而非存活缺陷。
2. 给出终止判定：若全文无新文档级 open（仅剩 godot-csharp 工程落地 §14 测试矩阵的实现类缺口，且文档已如实标注 out-of-scope）→ 明确写「可终止」。

**产出格式 `audit/iter53.md`（严格）：**
```
# iter53 终止判定审计（v3.0-FINAL-rA5）
## 摘要
- 文档版本/行数
- 新 open 项总数：X（可闭 K / 实现类 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 全文档 open 项清单（若有，每条回指 Lxxx）
| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
## 关键项复核（独立确认）
- `## 15` 残留：grep 结果（应零命中）
- 术语表 `**PDR**` 行数（应 1）
- §3.1.5c 定义存在性
- §3.1.2/§3.1.4a 构造子+映射
- §9.1 代码
- §3.2.5 Peak
- MA 栏/§12.2/§14 引用真实性抽查
## 结论
```

**要求：** 每条必须回指真实行号（先 grep/read 确认）。独立判断，不引用任何历史审计文件结论。若确实无 open，明确「可终止」并说明仅剩实现类 out-of-scope。

完成后回复：iter53.md 已写入；新 open 项 X（可闭 K/实现 M）；终止判定=？（一行摘要）。

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