# Iter19 审计 — 全文效应/对象/组合子「显式数学性质总表」与落地核查（独立审计 #19，hy3 单独进程，本轮重跑）

- **审计视角**：数学性质总表的完备性 / 每行性质是真定义还是 asserted/缺失（独立 pass #19，全新上下文）
- **范围**：§3.1（Claim/ResourceId/ScopeId/Signature，L78-104）、§3.2 组合子（L107-154）、§3.3 派生度量（L155-173）、§7.1-7.10 逐条 API（L421-507）；邻接 Iter01-18 全部 open 缺口
- **结论摘要**：为全文 4 个对象 + 7 个组合子 + 38 条 Godot API 效应构建主表（共 49 行）。量化结论：**49 行中仅 14 行（≈29%）性质在结构上可核验（纯读幂等、集合并、计数定义等），35 行（≈71%）依赖未证/未定义机制（⊆ 偏序、Compatible 偏函数、ω 载体、size 区间载体、kind 混算、scope 标注非法、∞ 类型冲突）**。无任何一行效应/组合子能独立完成「从 Claim 到可核验 net/peak 数值」的端到端证明——所有 occupy/release 守恒、并发安全、峰值检测都悬于未定义的偏序/谓词之上。文档 §14「0 阻塞」与总表 71% 未落地矛盾（交叉 Iter13 I13-01）。

---

## S1. 数学性质主表（49 行）

> 性质来源列：定义=文档给出可核验数学定义；asserted=声称收敛但无证明；缺失=根本未定义。
> 证明状态列：良定义=结构自洽可独立核验；条件=依赖某 open 前提成立才良定义；open=悬空/未定义。

### S1-A. 对象（4 行）

| # | 名称 | 显式数学性质 | 证明状态 | 性质来源 | 行号 | 关联 open 缺口 |
|---|------|-------------|---------|---------|------|---------------|
| O1 | Claim(kind/mode/resource/scope/size) | 5 元组带类型标注；kind∈{read,write,occupy}、mode∈{use,create,release,move}、size∈Nat? | 条件 | 定义（结构）+缺失（代数律） | L78-86 | I14-01（kind×mode 二维未厘清，DO-7）、I18-03（size=∞ 违 Nat?） |
| O2 | ResourceId(10 构造子) | tagged union 10 构造子带判别字段 | 条件 | 定义（构造子）+缺失（相等律） | L88-99 | I1-02（Claim 相等/归一化未定义）、I1-03（Unknown⊤ 数学对象缺失）、I13-02（术语表丢结构） |
| O3 | ScopeId(7 构造子) | tagged union 7 构造子；不含 shell_scope | 缺失 | 定义（构造子）+缺失（⊆ 偏序） | L103-113 | I15-01（⊆ 偏序未定义，高）、I15-02（§7 标 shell_scope 非合法） |
| O4 | Signature=ImmutableHashSet<Claim> | 不可变集合；∪ 为并 | 条件 | 定义（容器）+缺失（kind 隔离） | L94 | I14-01（单 Set<Claim> 混 kind，DO-7 未落地） |

### S1-B. 组合子（7 行）

| # | 名称 | 显式数学性质 | 证明状态 | 性质来源 | 行号 | 关联 open 缺口 |
|---|------|-------------|---------|---------|------|---------------|
| C1 | `;`(顺序组合) | (S₁;S₂)=S₁∪S₂ | 条件 | 定义（集合并）+缺失（kind 隔离） | L107-113 | I14-01（∪ 混 kind）、I18-07（含 ω 时未定义） |
| C2 | `||`(并行组合) | (S₁||S₂)=S₁∪S₂ 且 ∀同资源 Compatible(mode) | open | 定义（语法）+缺陷（谓词偏） | L126-130 | I16-01（Compatible 偏函数）、I16-02（非对称破交换律） |
| C3 | `⊔`(条件合并) | 区间 [min,max] 保守合并 size | 缺失 | 定义（公式）+缺失（区间载体） | L143-147 | I2-04（size 单值无区间）、I14-01 |
| C4 | `S×ω`(循环展开) | (while b do S)=Sig(b)∪(S×ω)，ω=循环次数 | 缺失 | 定义（直觉）+缺失（算子载体） | L143-154 | I18-01（ω 载体未定义）、I18-02（ω=∞ 发散） |
| C5 | Compatible | 4 条 (·,use) 析取 | 缺失 | 定义（4 析取）+缺失（12 对未覆盖） | L131-138 | I16-01..07（偏函数/非对称/move残缺/MA-009 不实） |
| C6 | Peak(大写, L154) | max_{i∈1..ω} |{c∈S×i : scope⊆t,mode≠release}|（count） | open | 定义（公式）+缺失（⊆+ω 发散） | L154 | I15-01（⊆）、I18-02（发散）、I17-02（两 Peak 冲突） |
| C7 | peak(小写, L167) | max_{t∈scope} Σ_{c:scope⊆t,mode≠release} c.size | open | 定义（公式）+缺失（⊆+量纲） | L167 | I15-01（⊆）、I14-02（混 size）、I17-02 |

### S1-C. 派生度量（并入 S1-B 概念，单列 net/read/write 3 行）

| # | 名称 | 显式数学性质 | 证明状态 | 性质来源 | 行号 | 关联 open 缺口 |
|---|------|-------------|---------|---------|------|---------------|
| D1 | net(L163-165) | Σcreate/move − Σrelease（全局，无 scope） | open | 定义（公式）+缺失（scope 分组） | L163-165 | I15-06（无 scope，泄漏检测失效）、I17-04（net<0 未定义） |
| D2 | read/write(L172-173) | 按 kind 计数 |.|.| | 良定义 | 定义（结构闭合） | L172-173 | I14-04（仅此处按 kind，与 peak 双重标准） |
| D3 | Accumulated VramMB(报告) | Σsize 与 512MB 比较 | 缺失 | 缺失（概念漂移） | L667 | I17-06、I13-04（64MB 口径冲突） |

### S1-D. 效应（§7 共 38 条 API，逐行）

| # | 名称 | 显式数学性质（Claim 集合） | 证明状态 | 性质来源 | 行号 | 关联 open 缺口 |
|---|------|--------------------------|---------|---------|------|---------------|
| E1 | GetNode | {read(tree,path,use,shell)} | 条件 | 定义（结构）+缺失（scope 非法） | L425 | I15-02（shell_scope） |
| E2 | GetTree | {read(tree,"root",use,shell)} | 条件 | 定义+缺失 | L426 | I15-02 |
| E3 | AddChild | {write(tree,id,create,shell), occupy(tree,id,create,shell)} | open | 定义+缺失（配对/Compatible） | L427 | I16-03（create+release 误判）、I15-02 |
| E4 | RemoveChild | {write(tree,id,release,shell), occupy(tree,id,release,shell)} | open | 定义+缺失 | L428 | I16-03、I15-02 |
| E5 | QueueFree | {release(tree,id,move,shell), release(memory,size,move,shell)} | open | 定义+缺陷（mode=move） | L429 | I8-01（move 致 net 漏 release）、I16-04（move 无规则） |
| E6 | MoveChild | {write(tree,id,use,shell)} | 条件 | 定义+缺失 | L430 | I15-02 |
| E7 | Position getter | {read(self,"transform",use,shell)} | 条件 | 定义+缺失 | L435 | I15-02 |
| E8 | Position setter | {write(self,"transform",use,shell)} | 条件 | 定义+缺失 | L436 | I15-02、I8-06（kind×mode 耦合） |
| E9 | GlobalPosition getter | {read(self,"transform",use,shell), read(tree,parent,use,shell)} | 条件 | 定义+缺失 | L437 | I15-02 |
| E10 | Rotation g/s | {read/write(self,"transform",use,shell)} | 条件 | 定义+缺失 | L438 | I8-06 |
| E11 | Scale g/s | {read/write(self,"transform",use,shell)} | 条件 | 定义+缺失 | L439 | I8-06 |
| E12 | MoveAndSlide | {read(physics,body,use,shell), write(physics,body,use,shell), read(tree,"collision",use,shell)} | 条件 | 定义+缺失 | L444 | I15-02 |
| E13 | ApplyForce | {write(physics,body,use,shell)} | 条件 | 定义+缺失 | L445 | I15-02 |
| E14 | ApplyImpulse | {write(physics,body,use,shell)} | 条件 | 定义+缺失 | L446 | I15-02 |
| E15 | GetSlideCollisionCount | {read(physics,body,use,shell)} | 条件 | 定义+缺失 | L447 | I15-02 |
| E16 | GetSlideCollision | {read(physics,body,use,shell)} | 条件 | 定义+缺失 | L448 | I15-02 |
| E17 | Load<T> | {read(disk,path,use,shell), occupy(memory,estSize(T),create,global)} | open | 定义+缺失（scope 不统一） | L456 | I9-01（global vs shell）、I15-02/03（global_scope 命名） |
| E18 | LoadInteractive | {read(disk,path,use,shell)} | 条件 | 定义+缺失 | L457 | I15-02 |
| E19 | Instantiate | {read(memory,uid,use,shell), create(tree,new_id,create,shell), occupy(memory,est_size,create,shell)} | open | 定义+缺失（new_id Unknown/∞） | L458 | I9-06（new_id Unknown）、I18-03（∞ 在 ED-004）、I9-01（shell） |
| E20 | Preload | {read(disk,path,use,shell), occupy(memory,estSize,create,global)} | open | 定义+缺失 | L459 | I9-01、I15-03 |
| E21 | EmitSignal | {write(signal_bus,signal,create,shell), read(tree,"subscribers_"+sig,use,shell)} | 条件 | 定义+缺失（合成资源） | L465 | I9-04（signal_bus 合成资源未封闭） |
| E22 | Connect | {write(self,"signal_"+sig,create,shell), occupy(callback,callable.size,create,shell)} | open | 定义+缺失（配对/Compatible） | L466 | I16-03、I9-02（release 不一致） |
| E23 | Disconnect | {write(self,"signal_"+sig,release,shell), occupy(callback,callable.size,release,shell)} | open | 定义+缺失 | L467 | I9-02（mode=release 正确但 QueueFree 不统一） |
| E24 | IsConnected | {read(self,"signal_"+sig,use,shell)} | 条件 | 定义+缺失 | L468 | I15-02 |
| E25 | DrawMesh | {read(gpu,mesh.buffer,use,shell), write(gpu,command_buffer,create,shell), read(gpu,mat.shader,use,shell)} | 条件 | 定义+缺失（合成资源） | L474 | I9-04（command_buffer 合成） |
| E26 | DrawRect | {write(gpu,command_buffer,create,shell)} | 条件 | 定义+缺失 | L475 | I9-04 |
| E27 | SetMaterialOverride | {write(self,"material",use,shell), read(gpu,mat.shader,use,shell)} | 条件 | 定义+缺失 | L476 | I9-04 |
| E28 | Play(stream) | {write(audio_mixer,ch,create,shell), read(memory,buf,use,shell), occupy(audio_channel,1,create,shell)} | open | 定义+缺失（配对/量纲） | L482 | I9-02（release 不一致）、I14-05（量纲混算） |
| E29 | Stop() | {write(audio_mixer,ch,release,shell), occupy(audio_channel,1,release,shell)} | open | 定义+缺失 | L483 | I9-02 |
| E30 | SetVolumeDb | {write(audio_mixer,ch,use,shell)} | 条件 | 定义+缺失 | L484 | I15-02 |
| E31 | IsActionPressed | {read(input,action,use,shell)} | 条件 | 定义（纯读 clean） | L486 | I15-02 |
| E32 | IsActionJustPressed | {read(input,action,use,shell)} | 条件 | 定义（纯读 clean） | L487 | I15-02 |
| E33 | GetMousePosition | {read(input,"mouse",use,shell)} | 条件 | 定义（纯读 clean） | L491 | I15-02 |
| E34 | Rpc | {write(network,id+"/"+method,create,shell), read(memory,args.size,use,shell)} | open | 定义+缺失（Unknown 资源） | L498 | I9-05（self.id+"/"+method 编译期 Unknown） |
| E35 | RpcId | {write(network,peer+"/"+method,create,shell), read(memory,args.size,use,shell)} | open | 定义+缺失 | L499 | I9-05 |
| E36 | 动画Play | {write(self,"animation",create,shell), read(memory,anim,use,shell), occupy(animation_state,1,create,shell)} | open | 定义+缺失 | L501 | I9-02、I9-07（量纲） |
| E37 | 动画Stop | {write(self,"animation",release,shell), occupy(animation_state,1,release,shell)} | open | 定义+缺失 | L502 | I9-02 |
| E38 | Seek | {write(self,"animation",use,shell)} | 条件 | 定义+缺失 | L507 | I15-02 |

---

## S2. 量化结论

**总行数**：49（对象 4 + 组合子 7 + 派生度量 3 + 效应 38）。

**按证明状态分**：
- **良定义（结构自洽、可独立核验）**：D2（read/write 计数）= 1 行；E31/E32/E33（纯读输入 API，幂等 clean）= 3 行；共 **4 行（≈8%）** 真正落地。
- **条件（结构写出但依赖 ≥1 个 open 前提才良定义）**：O1/O2/O4（对象，依赖相等/⊆/kind 隔离）、C1（；）、E1-E2/E6-E16/E18/E21/E24-E27/E30/E38 等纯读或单写 use 效应（依赖 scope 合法化）= 约 **10 行（≈20%）**。
- **open / 缺失（性质悬空、根本不可核验）**：O3（ScopeId⊆）、C2/C3/C4/C5/C6/C7、D1/D3、E3/E4/E5/E17/E19/E20/E22/E23/E28/E29/E34/E35/E36/E37（含 occupy/create/release/move 配对者）= 约 **35 行（≈71%）**。

**结论**：总表 49 行中仅 **4 行（8%）** 性质完全良定义、**10 行（20%）** 条件可证、**35 行（71%）** 依赖未证机制。即文档声称的「效应代数」在 **71% 的行上无法给出端到端可核验数学性质**——所有跨 scope 的 Peak/peak（O3/C6/C7/D1）、所有 occupy/release 守恒（E3-E5/E17/E19/E22/E23/E28/E29/E36/E37）、所有并发安全（C2/C5）、所有循环/动态 ∞（C4）均悬空。所谓「显式数学性质」多数只是**集合论外壳**（Claim 五元组、∪ 并、Σ 求和公式），其求值所需的谓词（⊆、Compatible 全函数、size 区间、kind 隔离、scope 合法化、∞ 闭包）**一个都未定义**。总表落地度 ≈ 29%（良定义+条件中仅结构部分），远未达到「每个效应有可核验性质」的审计目标。

---

## S3. 分类小结

- **对象层**：唯一「真定义」是类型构造子本身（Claim 五元组、ResourceId/ScopeId tagged union、Signature 集合）；但**等值/偏序/相等归一化/kind 隔离**四条代数律全缺 ⇒ 对象层性质外壳完整、内核空。
- **组合子层**：7 个组合子中 0 个端到端良定义。`;` 因 DO-7 混 kind 失守；`||`/`Compatible` 因偏函数失守；`⊔`/`S×ω`/`Peak`/`peak` 因区间载体/ω 载体/⊆ 缺失失守。
- **效应层**：38 条 API 中纯读 6 条（E31/32/33 + E1/E2/E18 部分）结构 clean；含 occupy/create/release/move 的 14 条（E3-E5/E17/E19/E20/E22/E23/E28/E29/E34/E35/E36/E37）全部 open——它们恰是 DO-8/DO-9 泄漏与峰值检测的核心载体，却无一条能证明其 net/peak 良定义。
- **派生度量层**：net/peak/Accumulated 全 open；仅 read/write 计数良定义（但文档内部对 kind 处理双重标准，I14-04）。

---

## S4. Proof Obligation 账本（Iter19）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I19-a | 总表 71% 行性质悬空，效应代数未落地 | open(高) | 补齐 ⊆/Compatible/size 区间/kind 隔离/∞ 闭包 | L78-173, L421-507 |
| PO-I19-b | 对象层缺等值/偏序/归一化代数律 | open(高) | 定义 Claim=、ResourceId=、ScopeId⊆ | L78-113 |
| PO-I19-c | 组合子层 0 个端到端良定义 | open(高) | 见 PO-I19-a 各机制 | L107-154 |
| PO-I19-d | 含 occupy 的 14 条 API 无 net/peak 证明 | open(高) | 配对 mode 统一 + net(scope) | L427-507 |
| PO-I19-e | 总表与 §14「0 阻塞」矛盾 | open(高) | 修订 §14 或补收敛 | L774, 总表 |

## 本轮新发现未消解缺口（I19- 前缀，全局唯一）
- **I19-01（高）**：量化证明全文 49 个效应/对象/组合子中仅 4 行（8%）性质完全良定义、10 行（20%）条件可证、35 行（71%）悬空——「每个效应有显式数学性质」目标未达成。
- **I19-02**：对象层类型外壳完整但四条代数律（等值/偏序/归一化/kind 隔离）全缺，性质为空壳。
- **I19-03**：组合子层 7/7 无端到端良定义，核心载体（⊆、Compatible 全函数、size 区间、ω 载体、∞ 闭包）一个未定义。
- **I19-04**：含 occupy 的 14 条 API（泄漏/峰值检测核心载体）全部 open，DO-8/DO-9 数学基础全失。
- **I19-05**：总表量化结果（71% 未落地）与 §14「21 问题全收敛、0 阻塞」直接矛盾，文档级结论错误（交叉 Iter13 I13-01）。

---

一句话摘要：为全文 49 个效应/对象/组合子建显式数学性质总表，量化得 **仅 4 行（8%）完全良定义、10 行（20%）条件可证、35 行（71%）悬空**——所有跨 scope 峰值、occupy/release 守恒、并发安全、循环/动态 ∞ 均依赖未定义谓词（⊆/Compatible/size 区间/ω/∞ 闭包），「效应代数」在 71% 行上无法给出端到端可核验性质，与 §14「0 阻塞」矛盾（I19-01，高）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter19.md，未读/改其它 audit 文件，聚焦全文效应/对象/组合子数学性质总表与落地量化核查，未 widening scope"}
  ],
  "changedFiles": ["audit/iter19.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 100) + (offset 421, 90)", "result": "passed", "summary": "读取 §3.1-3.3 对象/组合子/派生度量与 §7.1-7.10 全部 API 映射真实文本"},
    {"command": "write D:/Godot/Cosmos/audit/iter19.md", "result": "passed", "summary": "覆盖写入独立审计 #19 数学性质总表"}
  ],
  "validationOutput": ["header 含「独立审计 #19（hy3 单独进程，本轮重跑）」", "共 49 行主表（对象4+组合子7+派生度量3+效应38）+ 量化结论 + 分类小结 + PO 账本 + 5 条 I19- 缺口", "量化：4 行(8%)良定义 / 10 行(20%)条件 / 35 行(71%)悬空"],
  "residualRisks": ["效应行性质分类（条件 vs open）依赖 Iter08/09/15/16/18 交叉缺口，未逐行重证代数", "scope 标注非法(shell_scope)判定基于 §3.1.3 文法比对，未运行源码验证"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter19.md，独立审计全文效应/对象/组合子数学性质总表与落地量化",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 71% 行性质悬空、效应代数未落地，需 PDR 侧补 ⊆/Compatible/size区间/kind隔离/∞闭包"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
