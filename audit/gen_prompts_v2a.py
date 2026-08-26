# -*- coding: utf-8 -*-
import os
DOC = "D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md"
OUT_DIR = "D:/Godot/Cosmos/audit"
PROMPT_DIR = os.path.join(OUT_DIR, "prompts")
os.makedirs(PROMPT_DIR, exist_ok=True)

SCOPES = {
    1: "§3.1 基本对象：Claim/ResourceId/ScopeId/Signature 的定义。Set<Claim> 上 ∪ 运算的结合律/交换律/幂等律/中性元；Claim 相等规则是否定义（含 size 缺省归一化）；ResourceId 跨构造子等价与 Unknown⊤ 策略；ScopeId 上 ⊆ 是否定义。",
    2: "§3.2 组合律：顺序 ; 定义为 ∪ 是否丢失执行序；并行 || 在 Compatible 不满足时是否有错误语义；Compatible 对称性与 || 交换性；条件组合 ⊔ 的配对键是否自吞（size 参与相等判定）；循环 S×ω 中 ω 是否实际进入度量。",
    3: "§3.3 派生度量：net/Peak/read/write 的良定义性；net 对 ∪ 是否线性/同态；Peak 与 net 的关系；peak 过滤谓词 c.mode≠release 的量纲问题；net 值域（区间减法/负值）是否定义。",
    4: "§3.4 数学层审计发现表 MA-001..MA-010：逐条核查「已解决/已收敛」声明是否有数学论证支撑还是仅断言；列出每条的实际状态（discharged/asserted/open）及依据行号。",
    5: "§4 Entity-as-Data：EntityId 单调性/不可伪造的可证条件；Component 不可变性与递归约束完全性；Entity 完整性不变量的强制点；World 版本单调与寿命估算数字正确性；System 纯函数性的可证机制；EA-001..007 收敛真伪。",
    6: "§5 Shell 同态映射：「Shell 作为函子」的范畴结构/函子律是否成立还是比喻；薄层五约束的可证机制；Delta Sync 的增量一致性判据与前条件；Spawned/Modified/Destroyed 三集合不相交性与幂等性；SH-001..005 收敛真伪。",
    7: "§6 三层类型系统：L1/L2/L3 各层的检测完备性/soundness 是否被证明（写集分析、AST 检查、引用检测）；哪些「已收敛」实际依赖未证工具；TS-001..012 逐条收敛真伪；Benchmark 类声明的证据存在性。",
    8: "§7.1-7.3 场景树/属性/物理 API 映射：逐条给出 Claim 集合的数学性质；QueueFree 的 mode 选择与 Compatible/net 的一致性；self 资源标识统一性；重复 AddChild 等 Godot 合法操作是否误报；transform 资源粒度。",
    9: "§7.4-7.10 资源加载/信号/渲染/音频/输入/网络/动画映射：occupy 的 scope 一致性（global vs shell）；size 估算值来源；合成 resource 名（command_buffer/signal_bus 等）是否在 ResourceId 枚举内；Connect/Disconnect、Play/Stop 配对的 net 守恒。",
    10: "§8 效应推导层：白名单覆盖率声称与 §7 实际条目数的一致性；默认规则对未映射 API 的双向误差（漏报 occupy/release？误报 write？）；[EffectOverride]/suppress 类属性的无校验信任边界；ED-001..008 收敛真伪。",
    11: "§9 运行时层：Deviation 公式在 range=0 时是否除零；公式载体（区间 vs 单值 size）与 §3 定义一致性；Σ 索引对齐规则；Release 零开销的条件编译与 [Conditional] 双机制矛盾；Mono.Cecil IL 扫描能否证明剥离；RT-001..006 收敛真伪。",
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
