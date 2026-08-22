# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计员。任务：审计 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（PDR：Effect Cost Algebra v3.0，当前版本 v3.0-FINAL-rA3）。

**硬性约束（违反即作废）：**
1. 只读 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` 这一个文件。绝对禁止读取 `D:/Godot/Cosmos/audit/` 目录下任何文件（包括 iter01-iter50、stability01、README）。本审计必须独立，不依赖历史审计结论。
2. 只产出你自己的 `D:/Godot/Cosmos/audit/iter51.md`，不得修改 PDR 或其他任何文件。

**审计目标（挖出所有仍 open 的项）：**
逐节精读全文，找出一切「悬空/未定义/逃逸通道/自相矛盾/不可机械执行/收口声明但实则未闭」的项。重点检查但不仅限：
- 数学层（§3.1–§3.3）：Claim=、ScopeId⊆*、SizeVal/⊤、net(S,scope)、Peak、weight、Compatible 全函数+对称、ω/S×ω——是否还有未定义符号、未覆盖 case、自相矛盾（与 §3.4/§9/§12.2 引用不一致）。
- 派生度量（§3.3）：peak/Peak 是否仍两套定义；Peak 公式是否与 §3.1.5a ⊤ 律一致；read/write/peak 何时用哪个。
- §3.4 MA 状态栏（107 条 MATH-AUDIT）是否每条都收敛、引用行号真实存在、无悬空引用。
- §6 执行模型、§7 Godot API 映射表（100 个）、§7.1 QueueFree=release、§8.1 默认 Unknown 规则、release-class 白名单、§8.3 EffectOverride/AcceptDeviation 校验。
- §9.1 Deviation 公式分母下界、§9.3–§9.4 RT/MA 状态栏。
- §12.2 收口措施 AUDIT002/AUDIT003、§14（L2/L3 规范+测试矩阵）判据是否可证、测试是否真能证明（spec 级）。
- 全文是否有残留旧定义块（如 stability01 发现的 L349-382 重复块是否已删、是否还有其他重复/矛盾）。
- 原始 50 审计发现的 ~108 PO-I* 证明义务、~40 I- 实现缺口：在 rA3 中哪些已被收口、哪些仍 open（注意：实现类缺口本就不在文档内可闭，但若文档声称「已收口」而实则未闭，记为矛盾）。

**产出格式 `audit/iter51.md`（严格）：**
```
# iter51 复审计（v3.0-FINAL-rA3）

## 摘要
- 文档版本：v3.0-FINAL-rA3（行数 N）
- 仍 open 项总数：X（其中 文档内可闭 K 项 / 实现类 out-of-scope M 项）
- 新矛盾（rA3 引入或旧未闭）：Y 条
- 终止判定：若 X=0 或仅剩实现类 out-of-scope 且无新文档问题 → "可终止"；否则 "需继续"

## 仍 open 项清单（每条回指 Lxxx）
| # | 位置(回指Lxxx) | 问题 | 类别(可闭/实现缺口) | 严重度 | 建议闭合方式 |
| 1 | Lyyy | ... | 可闭 | 高 | ... |
...

## 新审计意见（不重复 50 审计已记、只记 rA3 新发现或旧未闭的精确项）
- ...

## 一致性核对（行号引用是否真实）
- 抽查 §3.4 引用的 Lxxx / §12.2 引用的 Lxxx，列出哪些行号在文件中不存在或指向错误内容。
```

**要求：**
- 每条 open 必须回指真实行号（先 ctx_read/ctx_grep 确认行号存在，别凭记忆编造）。
- 若全文确无新文档级 open（仅剩实现类缺口且文档已如实标注），明确写「可终止」，并列出实现类缺口作为已知 out-of-scope（不计入需继续）。
- 独立判断，不引用、不信任任何历史审计文件。

完成后回复：iter51.md 已写入，仍 open 项总数=X（可闭 K / 实现 M），终止判定=？（仅一行摘要）。

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