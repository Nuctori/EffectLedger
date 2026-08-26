# -*- coding: utf-8 -*-
"""Generate per-iteration task prompts for independent PDR audits via pi CLI."""

import os

DOC = "D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md"
OUT_DIR = "D:/Godot/Cosmos/audit"
PROMPT_DIR = os.path.join(OUT_DIR, "prompts")
os.makedirs(PROMPT_DIR, exist_ok=True)

SCOPES = {
    12: "§10 风险评估 R-1..R-12：逐一核查每条风险的缓解措施是否命中根因（而非症状层/TODO 兜底），识别高概率×高影响风险的缓解薄弱处（尤其 R-3 误报率、R-4/R-11 团队抗拒、R-8/R-9 性能），指出仍开放的 proof obligation。",
    13: "§11 工作量估算、§12 社区发布策略（含 AUDIT001/002/003 示例）、§13 术语表、§14 文档历史：核查它们与正文数学声明的一致性（如「21 问题全收敛/0 阻塞」是否与正文证据冲突；术语表 ResourceId 与 §3.1.2 是否一致；示例数字能否由 §7 映射导出）。",
    14: "跨章：DO-7「read/write/occupy 不可混算，编译期报错」与 §3.1 单一 Set<Claim> 混合 kind 做 ∪ 组合的矛盾；§3.3.2 peak 跨 kind 求和是否混算；MA-007 权重函数是否定义；DO-7 有无编译期执行机制。",
    15: "跨章：ScopeId（§3.1.3 七构造子）上的包含关系 ⊆ 从未定义，而 Peak/peak 的核心谓词 c.scope⊆scope 依赖它——自反性/传递性/跨构造子包含矩阵/t∈scope 域是否良定义；对 DO-8 峰值检测的影响。",
    16: "跨章：mode={use,create,release,move} 共 16 种有序组合，Compatible（§3.2.3）仅列 4 个析取——穷举 16 对判定矩阵，核查完备性（create∧release 是否漏列）与正确性（move∧use 在 QueueFree 场景是否错误兼容）；用 §7 真实映射找反例。",
    17: "跨章：MA-004「occupy 峰值与净变化混淆已解决」——Set<Claim> 使 net/peak 成为派生度量是否真消解混淆？结构层 vs 使用层分开论证；结合 §7 QueueFree mode=move 与 net 公式检查泄漏检测链是否成立。",
    18: "跨章：两个 ∞ 的语义一致性——MA-002「∞ 作为 ScopeId 的循环标记」vs §3.2.5「ω=循环次数，静态未知时为 ∞」；ScopeId 构造子 Loop(id:String) 是否承载 ∞；ω=∞ 时 Peak 取值是否取决于未定义的 S×ω 语义。",
    19: "跨章综合：为每一种 (kind∈{read,write,occupy} × mode∈{use,create,release,move}) 组合给出显式数学性质主表（幂等性/抵消性/单调性/可组合性），每格标注证明状态（discharged/asserted/open）及所依赖的前提；整合可从全文推导的证据。",
    20: "终局综合：汇总你能在本文档中发现的全部 proof obligation，编制消解状态账本（discharged/asserted/open 三栏），列出 Top 10 最关键缺口（按严重度×影响面排序），并对 §14「0 个阻塞/21 问题全部收敛」的声明给出基于证据的修正建议。",
}

TEMPLATE = """你是独立的 PDR 形式审计员（本轮为全新隔离进程，与其他轮次无共享上下文）。

第一步：用 read 工具完整读取设计文档 {doc}。
纪律：除该文档外禁止读取任何其他项目文件（包括 D:/Godot/Cosmos/audit/ 下的既有文件，防止上下文污染）；禁止凭记忆捏造文档内容。

第二步：针对以下范围做独立形式审计：
{scope}

审计要求：
1) 挖掘证明缺口（proof gap）：找出文档声称但未证明/未定义之处；
2) 尝试履行证明义务：对结构性成立的给出条件证明并写明所需前提；对需补充定义的标注 open 并给出最小补充方案；
3) 为涉及的效应/对象/组合子给出明确数学性质；
4) 每条结论标注状态：discharged（已证）/ asserted（仅声明未证）/ open（缺口），并引用文档 Lxxx 行号；
5) 所有结论必须与文档事实一致，发现文档内部矛盾时明确指出。

第三步：用 write 工具把中文 Markdown 审计报告写入 {out}（必须非空）。报告结构：
# Iter{nn:02d} 独立审计
- 范围 / 结论摘要
- 逐命题小节：命题 | 数学性质 | 状态 | 论证或反例 | 行号
- Proof Obligation 账本表：ID | 命题 | 状态 | 消解所需最小补充 | 行号
- 本轮新发现缺口清单

完成后仅回复：DONE_ITER_{nn:02d}
"""

for nn, scope in SCOPES.items():
    path = os.path.join(PROMPT_DIR, f"iter{nn:02d}.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write(
            TEMPLATE.format(
                doc=DOC,
                scope=scope,
                out=os.path.join(OUT_DIR, f"iter{nn:02d}.md"),
                nn=nn,
            )
        )
    print("wrote", path)
