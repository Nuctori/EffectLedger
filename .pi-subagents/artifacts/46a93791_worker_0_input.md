# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立终止判定审计。只读 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（当前 v3.0-FINAL-rA6），禁止读取 `D:/Godot/Cosmos/audit/` 任何文件，只产出 `D:/Godot/Cosmos/audit/iter54.md`。

**目的：** 确认全文是否还有任何「open」项（悬空定义/矛盾/逃逸通道/不可机械执行/虚假收敛/重复结构/行号引用失效）。此前多轮审计依次收口了 iter51 的 8 项、iter52/iter53 复核项，最后一项（术语表 PDR 重复行）已在 rA6 用 ctx_edit 删除。现在请你独立、不信任任何历史结论地确认。

**必须逐项亲自 read/grep 核验（凭记忆/信任历史=作废）：**
1. 文档结构：grep `## 15` 或 `## 14` 仅应出现一次 `## 14. 文档历史`，无 `## 15` 残留；无章节号倒挂。
2. 术语表（§13）：逐行 read 术语表，确认 `**PDR**` 仅一行、无任何重复词条、无 `**X** | ...` 连续两行完全相同。
3. §3.1.5c DeviationVal 定义体：grep `定义 3.1.5c` 应有真实命中，且 §9.1 引用 `§3.1.5c` 的行存在并对齐。
4. §3.1.2 构造子：read 列出 Occupancy/Callback/Input 是否在 ResourceId 构造子中。
5. §3.1.4a 裸名映射：read 确认有 memory/disk/audio_channel/animation_state/callback/... → 构造子缩写映射表。
6. §9.1 代码：grep `deviation > 0.2f` 应零命中；应有 `deviation is double d && d > 0.2` 形式。
7. §3.2.5 Peak：read 确认 cardinality 旧形式标注为废弃、以 §3.3.2 size-求和为准。
8. §8.1 release-class 清单：read 确认 = { queue_free, free, remove_child, disconnect, remove_from_group, cancel_free, free_children_in_group }，且与 §7 映射表 release 类 API 一致。
9. §3.4 MA 栏（107 条）、§12.2 收口措施、§14 测试矩阵：抽查行号引用是否真实存在、有无悬空、判据是否可证。
10. 全文扫描 `悬空|逃逸|留口|依赖实现|TODO`：命中若均为「已收口叙述」则不计为 open。

**产出 `audit/iter54.md`：**
```
# iter54 终止判定审计（v3.0-FINAL-rA6）
## 摘要
- 版本/行数
- 新 open 项总数：X（可闭 K / 实现 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 关键项独立核验（每条附 read/grep 证据行号）
| 检查项 | 方法 | 结果 | 行号证据 |
...
## 残留 open 项（若有，回指 Lxxx）
| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
## 结论
```

若全文无新文档级 open（仅剩 godot-csharp 工程落地 §14 测试矩阵的实现类缺口且文档已如实标注）→ 明确写「可终止」。

完成后回复：iter54.md 已写入；新 open 项 X（可闭 K/实现 M）；终止判定=？（一行）。

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