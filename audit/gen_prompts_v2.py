# -*- coding: utf-8 -*-
import os
DOC = "D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md"
OUT_DIR = "D:/Godot/Cosmos/audit"
PROMPT_DIR = os.path.join(OUT_DIR, "prompts")
os.makedirs(PROMPT_DIR, exist_ok=True)

SCOPES = {
    13: "§11 工作量估算、§12 社区发布策略（含 AUDIT001/002/003 示例）、§13 术语表、§14 文档历史：核查它们与正文数学声明的一致性（如收敛声明是否与正文证据冲突；术语表 ResourceId/ScopeId 行是否与 §3.1.2/3.1.3 构造子一致；示例数字能否由 §7 映射导出）。",
    14: "跨章：DO-7「read/write/occupy 不可混算，编译期报错」与 §3.1 单一 Set<Claim> 混合 kind 做 ∪ 组合的矛盾；§3.3.2 peak 跨 kind 求和是否混算；weight 权重函数是否定义；DO-7 有无编译期执行机制。",
    15: "跨章：ScopeId（§3.1.3 全部构造子）上的包含关系 ⊆ 从未定义或自相矛盾，而 Peak/peak 的核心谓词 c.scope⊆scope 依赖它——自反性/反对称/传递性/跨构造子包含矩阵/t∈scope 域/Shell 构造子在表中的覆盖，对 DO-8 峰值检测的影响。",
    16: "跨章：mode 组合的 Compatible（§3.2.3）——穷举全部有序对判定矩阵，核查完备性与正确性；用 §7 真实映射找反例（如 write 操作标 mode=use 是否使 CONFLICT 集在真实映射上不可实例化）。",
    17: "跨章：MA-004「occupy 峰值与净变化混淆已解决」——Set<Claim> 使 net/peak 成为派生度量是否真消解混淆？结构层 vs 使用层分开论证；集合幂等 vs 多重计数矛盾对 net/Peak 的冲击；§7 QueueFree 与 Instantiate 的资源标识配对检查泄漏检测链。",
    18: "跨章：∞/ω 语义一致性——MA-002「∞ 作为 ScopeId 循环标记」vs §3.2.5「ω=循环次数」；S×ω 定义中 ω 是否实际进入任何度量（copy_i 同构副本使 max 退化）；ω=∞ 时 Peak 取值；DO-8 循环峰值检测是否成立。",
    19: "跨章综合：为每一种 (kind×mode) 组合给出显式数学性质主表（幂等性/抵消性/单调性/可组合性），每格标注 discharged/asserted/open 及前提依赖；结合 §7 真实映射实例化每格。",
    20: "终局综合：汇总你在本文档中发现的全部 proof obligation，编制消解状态账本（discharged/asserted/open 三栏统计+明细），列出 Top 10 最关键缺口（按严重度×影响面排序），并对文档的收敛声明给出基于证据的裁定。",
}

TEMPLATE = """【任务模式：单轮强制执行】你是独立的 PDR 形式审计员，全新隔离进程。禁止寒暄、禁止复述任务、禁止请求澄清、禁止「先对齐理解」。你的第一条回复就必须是工具调用。

第1步：用 read 工具读取 {doc}。
纪律：除该文档外禁止读其他项目文件（含 audit/ 下任何文件）；结论仅基于该文档磁盘内容并引用 Lxxx 行号；每条命题标注 discharged/asserted/open。

第2步：立即针对以下范围做形式审计：
{scope}
要求：(a) 挖掘证明缺口；(b) 对可证者给条件证明并写明前提；(c) 给出明确数学性质；(d) 发现文档内部矛盾时明确指出。

第3步：用 write 工具把中文 Markdown 审计报告写入 {out}（必须非空）。结构：
# Iter{nn:02d} 独立审计
- 范围 / 结论摘要
- 逐命题小节（命题|数学性质|状态|论证或反例|行号）
- Proof Obligation 账本表（ID|命题|状态|消解所需最小补充|行号）
- 新发现缺口清单

最后一条回复必须是恰好一行：DONE_ITER_{nn:02d}"""

for nn, scope in SCOPES.items():
    path = os.path.join(PROMPT_DIR, f"v2_iter{nn:02d}.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write(TEMPLATE.format(doc=DOC, scope=scope, out=os.path.join(OUT_DIR, f"iter{nn:02d}.md"), nn=nn))
    print("wrote", path)
