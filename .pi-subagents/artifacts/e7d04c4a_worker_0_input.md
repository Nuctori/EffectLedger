# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计员。任务：复审计 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（PDR：Effect Cost Algebra v3.0，当前版本 v3.0-FINAL-rA4）。

**硬性约束（违反即作废）：**
1. 只读 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` 这一个文件。绝对禁止读取 `D:/Godot/Cosmos/audit/` 目录下任何文件（包括 iter01-iter51、stability01、README）。本审计必须独立，不依赖历史审计结论。
2. 只产出你自己的 `D:/Godot/Cosmos/audit/iter52.md`，不得修改 PDR 或其他任何文件。

**背景（仅告知上下文，不要信任其结论）：** 上轮 iter51 在 rA3 上发现 8 个 open 项（双标题、§3.1.5c 幽灵定义、§7 资源未入 ResourceId、§9.1 代码类型不符、术语表重复、Peak 两式并存、merge_I ⊤ 律、裸资源名映射），父进程已在 rA4 中全部闭合。你的任务是验证这些是否真的闭合了，并挖出 rA4 是否有**任何新**的 open 项。

**审计目标：**
1. 逐条核对 iter51 的 8 项在 rA4 中是否真的闭合（回指当前真实行号确认）：
   - #1 双标题 `## 15.文档历史` 是否已删（应只剩 `## 14.文档历史`）
   - #2 §3.1.5c DeviationVal 定义体是否真实存在（grep `定义 3.1.5c` 应有命中，且 §9.1 L 引用对齐）
   - #3 §7 的 audio_channel/animation_state/callback 是否已在 §3.1.2 有构造子（Occupancy/Callback），且 §3.1.4a 有裸名映射
   - #4 §9.1 代码 `if (deviation > 0.2f)` 是否已改为先判 ⊤ 再比 double
   - #5 术语表 PDR 词条是否仍重复两行
   - #6 §3.2.5 Peak cardinality 形式是否标注为废弃、以 §3.3.2 为准
   - #7 §3.1.5b merge_I 是否显式套用 §3.1.5a ⊤ 律
   - #8 §3.1.4a 是否有 §7 裸资源名→构造子缩写映射表
2. 全文档再扫一遍，挖出任何**新**的 open 项（悬空定义/矛盾/行号引用失效/不可机械执行/未收口逃逸通道）。重点：§3.4 MA 栏（107 条）行号引用是否真实、§12.2 收口措施引用、§14 测试矩阵判据是否可证、§8.1 release-class 清单是否与 §7 映射表一致。
3. 特别注意：是否存在「文档声称已收口但实则未闭」的虚假收敛（iter51 报告的 rA3 状态）。

**产出格式 `audit/iter52.md`（严格）：**
```
# iter52 复审计（v3.0-FINAL-rA4）

## 摘要
- 文档版本：v3.0-FINAL-rA4（行数 N）
- iter51 的 8 项闭合确认：X/8 已闭（逐条列出确认/未闭）
- 新 open 项总数：Y（可闭 K / 实现类 out-of-scope M）
- 终止判定：
   若 Y=0（或仅剩实现类 out-of-scope 且文档已如实标注无新文档问题）→ "可终止"
   若有新文档级 open → "需继续(N条)"

## iter51 闭合核对（逐条）
| iter51# | rA4 状态 | 当前行号 | 证据 |
| #1 | 已闭/未闭 | Lxxx | ... |
...

## 新 open 项清单（若有）
| # | 位置(回指Lxxx) | 问题 | 类别 | 严重度 | 建议 |

## 一致性核对（行号引用真实性抽查）
- ...

## 结论
```

**要求：** 每条必须回指真实行号（先 grep/read 确认存在，别凭记忆）。独立判断。

完成后回复：iter52.md 已写入；iter51 闭合 X/8；新 open 项 Y（可闭 K/实现 M）；终止判定=？（仅一行摘要）。

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