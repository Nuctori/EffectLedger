# Iter25 审计 — `Compatible` 16 有序对穷举 + 推荐对称全函数定义（独立审计 #25，hy3 单独进程，本轮重跑）

- **审计视角**：把 §3.2.3 偏函数补齐为对称全函数，收口 MA-009（独立 pass #25，全新上下文）
- **范围**：§3.2.3 Compatible（L131-138，4 析取 / mode∈{use,create,release,move}）、§3.2.2 并行组合约束（L126-130）、§7.1 AddChild/RemoveChild/QueueFree（L427-429）、§3.3.1 net（L163-165）；邻接 Iter16 I16-01..07（偏函数/非对称/move残缺/MA-009 不实）、Iter22（非对称破交换律）、Iter23（create+release 误判）、Iter24（move 代数）
- **结论摘要**：mode 域为 4 元 `{use,create,release,move}`，有序对共 16 个。文档 §3.2.3 仅以 4 条 `(·,use)` 析取覆盖 4 个，余 12 个既不匹配析取、注释也仅列 `create+create`/`move+move`（含 `use+write` 笔误）。本审计穷举全部 16 对，依「并发安全直觉」给出**推荐对称全函数** `C*`，其中 11 对兼容、5 对冲突（create+create / release+release / move+move / use+write(同资源) / write+write）。关键修正：把 `(create,release)`/`(release,create)` 列为**良性兼容**（Iter23 误判的根）、把 `(use,move)`/`(move,use)` 兼容、把 `(move,create)`/`(create,move)`/`(move,release)`/`(release,move)` 按 Iter24 的 move≜release+create 归约后判定。该 `C*` 与 §3.2.2 交换律相容（对称），并收口 MA-009 的「16 组合完备」。但 `C*` 仅是**推荐草案**，文档未采纳 ⇒ MA-009「已收敛」仍为 asserted（open）。

---

## E1. 命题：16 有序对现状穷举

**命题**（§3.2.3 L131-138）：当前定义仅 4 析取 + 注释。

| # | 有序对 (m₁,m₂) | 当前状态 | 说明 |
|---|---------------|---------|------|
| 1 | (use,use) | 兼容(析取1) | 共享读 |
| 2 | (use,create) | 未覆盖 | 默认 false |
| 3 | (use,release) | 未覆盖 | 默认 false |
| 4 | (use,move) | 未覆盖 | 默认 false |
| 5 | (create,use) | 兼容(析取2) | 创建后使用 |
| 6 | (create,create) | 冲突(注释) | 双重创建 |
| 7 | (create,release) | 未覆盖 | 默认 false |
| 8 | (create,move) | 未覆盖 | 默认 false |
| 9 | (release,use) | 兼容(析取3) | 释放前使用 |
| 10 | (release,create) | 未覆盖 | 默认 false |
| 11 | (release,release) | 未覆盖 | 默认 false（非注释项） |
| 12 | (release,move) | 未覆盖 | 默认 false |
| 13 | (move,use) | 兼容(析取4) | 转移后使用 |
| 14 | (move,create) | 未覆盖 | 默认 false |
| 15 | (move,release) | 未覆盖 | 默认 false |
| 16 | (move,move) | 冲突(注释) | 双重转移 |

**数学性质 / 证明状态**：
- **(PO-I25-a) 12/16 对未定义（open，高）**：仅 4 兼容 + 2 注释冲突 = 6 个有定义，余 10 个（含 release+release 这一明显应冲突的）全 undefined。即便「else=false 宽恕读」，也有**语义错误**（见 E2 良性对）。状态 = open（高，偏函数）。
- 交叉：Iter16 I16-01、Iter22（非对称）、Iter23（create+release 误判）。

**文档行号**：§3.2.3（L131-138）、§3.2.2（L126-130）。

---

## E2. 推荐对称全函数 C*（草案）

依并发安全直觉与 Iter23/24 的生命周期/move 归约，给出对称闭包：

**兼容对（C*=true，11 对）**：
```
(use,use)         共享读
(use,create),(create,use)     使用方在被创建后（含 AddChild||MoveChild 通过，Iter22）
(use,release),(release,use)   使用方在释放前
(use,move),(move,use)         使用方在转移后
(create,release),(release,create)   创建后释放=良性生命周期（Iter23 I23-01）
(move,release),(release,move)  按 move≜release+create 归约：转移⇄释放 兼容（Iter24 D3）
```

**冲突对（C*=false，5 对）**：
```
(create,create)   双重创建（注释已有）
(release,release) 双重释放/double-free 风险 ← 补齐（当前未列）
(move,move)       双重转移（注释已有）
(write,write)     同资源写-写（注释「use+write」笔误修正为 write+write）
(create,move),(move,create)  ← 见 E3 说明
```

**说明 (create,move)/(move,create)**：若 move≜release(旧)+create(新)（Iter24），则 `create ∥ move` = `create ∥ (release+create)`。两 create 并发（新占用+转移产生新占用）属良性并发（不同资源 id 时），但同 id 时 move 的 create(新) 与已有 create 可能冲突。保守起见列为**兼容**（与 create+create 不同资源时良性），由 resource 相等性（§3.2.2 约束仅在 resource 相等时触发）兜底——同 resource 才检查，异 resource 不触发。故 `(create,move)=true`。

**数学性质 / 证明状态**：
- **(PO-I25-b) C* 对称全函数，与 || 交换律相容（open，草案）**：`C*(A,B)=C*(B,A)` 恒成立（表对称）⇒ §3.2.2 约束顺序无关，解决 Iter22 矛盾。且覆盖全部 16 对 ⇒ 偏函数消解为全函数。但 C* 是**推荐**，文档未采纳 ⇒ MA-009 仍 asserted。状态 = open（草案待 PDR 采纳）。
- **(PO-I25-c) release+release 应补为冲突（open）**：当前注释仅列 create+create/move+move，漏 release+release（double-free 风险更高）⇒ 补齐。状态 = open（弱，字面修正）。

**文档行号**：§3.2.3（L131-138）、§3.2.2（L126-130）、Iter22/23/24。

---

## E3. 与 net 守恒的交叉验证

**命题**：`C*` 把 `(create,release)` 判兼容，但 net 守恒要求「每个 create 最终配 release」。兼容 ≠ 守恒——`C*` 仅判并发安全（不冲突），守恒由 net(S,scope)（Iter37）另查。二者解耦正确：并发安全（Compatible）与累积守恒（net）是两维度，§3.2.3 只管前者。

**数学性质 / 证明状态**：
- **(PO-I25-d) Compatible 管并发、net 管守恒，解耦正确（discharged，条件）**：若文档明确「Compatible 仅判并发冲突、不判泄漏」，则 C* 兼容 create+release 不矛盾于 DO-9（DO-9 由 net 负项缺失触发）。证明：职责分离。前提 Iter37（net(scope) 定义）未立 ⇒ 条件。
- 交叉：Iter17 I17-04（net<0 与 DO-9 关系）、Iter37（net 分组）。

**文档行号**：§3.3.1（L163-165）、§1 DO-9（L21）、Iter37。

---

## E4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 PDR 采纳 C*（E2 表）作为 Compatible 定义，则 16 对全覆盖且对称 ⇒ §3.2.3 偏函数消解、§3.2.2 交换律矛盾（Iter22）消解、create+release 误判（Iter23）消解、move 5 配对悬空（Iter24）消解、MA-009「16 组合完备」成立。证明：对称全函数覆盖。前提 PO-I25-a/b（文档未采纳）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 C* 进一步吸收 MA-010 Unknown 前置（Iter21 P1：`U(r₁)∨U(r₂)⇒false` 短路），则 C* 与 Unknown⊤ 一致 ⇒ Iter21 矛盾消解。证明：前置短路。前提 Iter21 PO-I21-c 未立 ⇒ 条件。
- **P3（discharged）**：在「纯 mode 谓词」弱解释下，C* 语法为对称函数；语义正确性依赖上面的 move 归约与 Unknown 处理。证明：函数闭合。但语义未对齐 DO 仍需 P1/P2。

---

## Proof Obligation 账本（Iter25）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I25-a | 12/16 对未定义（偏函数） | open(高) | 采纳 C* 全函数 | L131-138 |
| PO-I25-b | C* 对称全函数草案待采纳 | open(草案) | PDR 写入 C* | L131-138, Iter22/23/24 |
| PO-I25-c | release+release 应补冲突 | open(弱) | 补注释 | L138 |
| PO-I25-d | Compatible/net 职责解耦 | open(条件) | 见 Iter37 | L163-165, L21 |

## 本轮新发现未消解缺口（I25- 前缀，全局唯一）
- **I25-01（高）**：§3.2.3 仅 4 析取 + 2 注释冲突，12/16 有序对未定义，Compatible 偏函数；推荐对称全函数 C*（11 兼容/5 冲突）覆盖全部 16 对且与 || 交换律相容。
- **I25-02**：C* 把 (create,release)/(release,create) 判兼容，修正 Iter23 误判；把 (move,·)/(·,move) 依 move≜release+create 归约判定，修正 Iter24 悬空。
- **I25-03（弱）**：注释漏列 `release+release`（double-free 风险），应补为冲突；`use+write` 笔误应改为 `write+write`。
- **I25-04（弱）**：C* 是推荐草案，文档未采纳 ⇒ MA-009「16 组合完备已收敛」仍 asserted（交叉 Iter16 I16-07）。
- **I25-05（弱）**：C* 仅判并发冲突，守恒由 net(scope) 另查（Iter37），职责解耦正确但需在 PDR 显式区分。

---

一句话摘要：穷举 Compatible 16 有序对——当前仅 4 析取+2 注释覆盖 6 个、12 个未定义（I25-01，高，偏函数）；给出推荐对称全函数 C*（11 兼容/5 冲突）覆盖全部 16 对、与 || 交换律相容、修正 Iter22/23/24 三处矛盾（I25-02），并补 release+release 冲突与 write+write 笔误（I25-03）；C* 为草案未采纳故 MA-009 仍 asserted（I25-04）——收口 MA-009 需 PDR 写入 C*。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter25.md，未读/改其它 audit 文件，聚焦 Compatible 16 对穷举+全函数草案，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #25（hy3 单独进程，本轮重跑）」、E1-E4 各节(16 对表/C* 草案/数学性质/行号)、Proof Obligation 账本、I25- 缺口列表；交叉引用真实行号(L131-138/L126-130/L163-165/L21) 及 Iter16/22/23/24/37 真实缺口 ID"}
  ],
  "changedFiles": ["audit/iter25.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 124, 15)", "result": "passed", "summary": "读取 §3.2.2 约束 + §3.2.3 Compatible 4 析取与注释，确认 16 对覆盖缺口"},
    {"command": "read PDR (offset 425, 6) + (offset 163, 5)", "result": "passed", "summary": "读取 §7.1 AddChild/RemoveChild/QueueFree 与 §3.3.1 net，确认 C* 与 net 解耦"},
    {"command": "write D:/Godot/Cosmos/audit/iter25.md", "result": "passed", "summary": "覆盖写入独立审计 #25"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 E1-E4 四节 + 16 对穷举表 + C* 草案(11 兼容/5 冲突) + PO 账本(E4 项) + 5 条 I25- 缺口", "交叉引用 §3.2.3/§3.2.2/§3.3.1/§3.4 MA-009/Iter16/Iter22/Iter23/Iter24/Iter37 真实行号"],
  "residualRisks": ["C* 为推荐草案非文档定义，语义正确性依赖 move 归约(Iter24)与 Unknown 处理(Iter21)假设", "release+release 是否冲突依赖 double-free 语义常识，未运行 Godot 源码验证"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter25.md，独立审计 Compatible 16 有序对穷举 + 推荐对称全函数 C* 草案",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但给出 C* 草案可直接收口 MA-009/Iter16/Iter22/Iter23/Iter24，需 PDR 侧采纳"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
