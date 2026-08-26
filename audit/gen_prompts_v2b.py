# -*- coding: utf-8 -*-
import os
DOC = "D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md"
OUT_DIR = "D:/Godot/Cosmos/audit"
PROMPT_DIR = os.path.join(OUT_DIR, "prompts")

TEMPLATE = """【任务模式：单轮强制执行】你是独立的 PDR 形式审计员，全新隔离进程。禁止寒暄、禁止复述任务、禁止请求澄清。你的第一条回复就必须是工具调用。

第1步：用 read 工具读取 {doc}。
纪律：除该文档外禁止读其他项目文件；结论仅基于该文档磁盘内容并引用 Lxxx 行号；每条命题标注 discharged/asserted/open。

第2步：立即针对以下范围做形式审计：
{scope}
要求：(a) 挖掘证明缺口；(b) 对可证者给条件证明并写明前提；(c) 给出明确数学性质；(d) 发现文档内部矛盾时明确指出。

第3步：用 write 工具把中文 Markdown 审计报告写入 {out}（必须非空，控制在 90 行以内，精炼）。结构：
# Iter{tag} 独立审计
- 范围 / 结论摘要
- 逐命题小节（命题|数学性质|状态|论证或反例|行号）
- Proof Obligation 账本表
- 新发现缺口清单

最后一条回复必须是恰好一行：DONE_ITER_{tag}"""

scopes = {
    "10a": ("Iter10 前半：§8.1 白名单/默认规则 + §8.2 中 ED-001..ED-004。核查：白名单「约100个核心API」声称与 §7 实际条目数的一致性；默认规则对未映射 API 的双向误差（是否漏报 occupy/release？是否给纯读 API 强加 write？）；unknown 资源标识的冲突放大效应；[EffectOverride] 类属性的无校验信任边界。",
            "D:/Godot/Cosmos/audit/iter10.md"),
    "11a": ("Iter11 前半：§9.1 Deviation 公式的良定义性（range=0 除零风险；公式载体区间 vs §3 size 单值/区间的载体一致性；Σ 索引对齐规则）+ §9.2 Release 零开销（条件编译 #if 与 [Conditional] 双机制的差异；Mono.Cecil IL 扫描能否证明剥离）。",
            "D:/Godot/Cosmos/audit/iter11.md"),
}
for tag, (scope, out) in scopes.items():
    path = os.path.join(PROMPT_DIR, f"v2_iter{tag}.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write(TEMPLATE.format(doc=DOC, scope=scope, out=out, tag=tag))
    print("wrote", path)
