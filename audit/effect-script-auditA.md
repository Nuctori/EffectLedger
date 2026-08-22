# 对抗审计 A — EFFECT_SCRIPT.md

> 审计对象：`D:/Godot/Cosmos/EFFECT_SCRIPT.md`（设计冻结稿）
> 审计者：独立对抗性数学家 subagent #1/3
> 方法：对照真实 L1 源码（`Objects.cs`/`Algebra.cs`/`DerivedMetrics.cs`/`Numeric.cs`）与 PDR §3.1–§3.3，逐项质问并找反例。
> 立场：**过度质疑**；宁可误报 open，不漏判。

---

## 逐条焦点结论

### 焦点 1 — 端点采样完备性定理（§3）｜**成立（附 1 个采样细节 note）**

`At(t) = ⋃_{e : lifetime_e ∋ t} contribution(e)`。每个 `contribution(e)` 在 e 存活期内是**常量 Signature**（Footprint 固定、Loop 缩放因子 ω 固定），故 `At(t)` 仅在「存活事件集合」改变时跳变，即仅在各 `Lifetime` 的 `lo`/`hi` 处跳变。时间域是离散 ℕ*（帧索引），端点是精确 ulong 值，采样即精确命中。两相邻区间 `[a,b]`、`[b,c]` 在点 `b` 处两者皆存活（`t≤hi` 含端点），并集与内部一致。→ 收集全部有限 `lo_i`/`hi_i` 采样即完备、精确、非近似。**定理成立。**

- **note（低）**：若某 event `hi = ⊤`（上界开放），无有限 hi 可采样，但其贡献自 `lo` 起恒定，故无需额外采样点。设计 §3 写「收集所有 Event 的 distinct Lo/Hi 端点（有限集）」应显式说明 `hi=⊤` 不计端点（否则实现者会尝试采样一个不存在的有限 hi）。不影响完备性。

### 焦点 2 — `At(t)` 的 scope / loopScope 语义缺口｜**缺口（HIGH）**

设计 §2.2 伪码：
> 「遍历 Events，对满足 `e.Lifetime ∋ t` 者取 `Combination.Loop(e.Footprint, e.Loop, loopScope)` 后 `Signature.Union` 累积。」

`loopScope` 是**自由变量**，设计全文未定义其来源。`EffectEvent`（§2.1）**不携带 scope**，`Footprint`（Signature）内部各 Claim 已自带 `Scope`，但 `Combination.Loop(body, ω, loopScope)`（DerivedMetrics.cs:37-47）把 body 内**每个 Claim 的 Scope 改写为 `loopScope`**，即丢弃了 Footprint 原有的逐元素 Scope。

后果：
1. `At(t)` 产出 Signature 中所有 Claim 的 scope 都被压成同一个 `loopScope`（或实现者随意填的某值），原 scene/element 级 scope 信息丢失。
2. 紧接着 §3.1 `Net(At(t), scope)` / `Peak(At(t), scope)` 的 `scope` 参数在脚本级 `Audit(Budget)` 中**从未定义**——是 Global？是 loopScope？设计表格与正文均未给。
3. 若 `scope = Global`（screen-wide，最合理），则 `ScopeId.IncludedIn(Global)` 恒真（Objects.cs:104），scope 过滤退化为空操作——那 `At(t)` 把 Claim 改写为 loopScope 的举动纯属多余且破坏「按原 scope 分桶做 per-scene 审计」的可能。

**最小修正（不改代码，仅描述）**：
- `At(t)` **不应调用 `Combination.Loop` 来改写 scope**；直接 `Signature.Union(acc, e.Footprint)`（Footprint 内 Claim 已带正确 Scope）。
- 脚本级 `Audit` 明确以 `ScopeId.Global`（屏幕级）为 net/Peak 的 scope 参数；如需 per-scene 审计，则在 Event 上增可选 `ScopeId ElementScope` 字段，由 `At` 保留而不改写。
- 删除 §2.2/§5 中 `Combination.Loop(e.Footprint, e.Loop, loopScope)` 的误用（见 OPEN-3，Loop 在此完全用错）。

### 焦点 3 — 守恒判定的时间窗口（瞬时 vs 累积）｜**缺口（HIGH）**

`Net(At(t), scope).IsConserved(r)`（Algebra.cs:94-102）是**瞬时**判定：对归一化资源 r，若净区间含 0 则守恒。**但守恒的语义应是「整个脚本时间轴上的 create 与 release 配对闭合」，不是「某一瞬间净占用为 0」。**

反例：一个纹理在 t=10 被 create（`size=[1,1]`），在 t=70 被 release。则在 (10,70) 的**每一个采样点**，`At(t)` 的 net 对该资源 = `[+1,+1]`（仅 create），`ContainsZero`=false ⇒ `IsConserved`=**false**。设计 §3.1 把此当作「泄漏」报警。

然而这只是一个**合法的临时占用**（持 60 帧后释放），根本不是泄漏。→ **设计 §3.1 守恒检查若按逐点 `IsConserved` 实现，会拒绝一切非零时长的合法临时效应**（每个 sprite/纹理/音频Clip 都持有资源一段非零时间）。这是核心正确性缺陷，不是边界误判。

**最小修正**：
- 守恒应为**脚本级累积 net**：对每资源 r，`Net_script(r) = Σ_e (LoopCount 缩放的) [create(+size) − release(−size)]`，跨**全部事件、全部时刻**求和（即把 `At(t)` 的「瞬时并集」换成「时间聚合求和」）。`Net_script(r).ContainsZero` ⇒ 闭合（可能守恒）。
- ω=⊤ 且本就无 release 的常驻资源（背景层）⇒ 累积 net = +size ≠0 ⇒ 正确标为非守恒（与 §2.1 意图一致）。
- 瞬时 `IsConserved(At(t))` 仅适用于「单帧内自洽」场景（如单帧内 create 与 release 配对），不应作为脚本级泄漏判据。

### 焦点 4 — `Budget` 软约束的 ⊤ 交互（常驻 vs 峰值预算）｜**缺口（MEDIUM，需澄清）**

`Peak.Compute`（Algebra.cs:112-124）对任何 `size.Hi.IsTop` 直接 `return NatStar.Top`。`Combination.Loop` 在 ω=⊤ 时把 size 拉成 `[lo, ⊤]`（DerivedMetrics.cs:58-60）。故任意含 ω=⊤ 元素的 `At(t)` 的 Peak = ⊤。而 `Budget.Caps[r]` 若填了有限值，`NatStar.CompareToFinite(⊤, finite)` 返回「⊤ 更大」⇒ **超限 ⇒ Audit 必然拒绝**（Numeric.cs:53-59）。

设计 §2.1 称 ω=⊤「正确标为非守恒（除非本就是常驻背景层）」——但那是**守恒**检查；**峰值预算**是 §3.1 表格里**独立**的另一道 gate。一个合法的「常驻背景粒子层」(ω=⊤) 会被峰值预算 gate **无条件拒绝**，与 §2.1「常驻背景层」的预期（应通过）矛盾。

设计 §2.3 说 Budget 缺省 = 该资源无上限（⊤）；`CompareToFinite(⊤,⊤)` 返回 0（相等）⇒ 通过。所以「不写该资源 cap」则通过、「写有限 cap」则拒绝——这本身合理，但设计**未说清此权衡**，也未按此意图给常驻资源豁免。

**最小修正**：在 §3.1 明确「峰值预算 gate 对 ω=⊤ 资源：若 `Budget.Caps[r]` 未设（默认 ⊤）则通过；若显式设有限 cap 则拒绝（即用户主动要求限制常驻资源并发）——此为预期语义，非 bug」。并建议对「声明为 perpetual（无配对 release）的资源」在 UI/契约层标记豁免，避免误读。

### 焦点 5 — `Compatible` 在剧本层的使用（配对未定义 + 正文/表格歧义）｜**缺口（MEDIUM）**

§3.1 表格写「同资源两 create 冲突」，正文写「对 At(t) 内同 scope 兄弟 Claim，`Compatible.IsCompatible` 全通过」。**两者口径不一致**，且 `Compatible.IsCompatible(a,b)`（Algebra.cs:17-28）**只接受两个 `Mode`，不感知 resource 也不感知 scope**——它根本不知道「哪些 Claim 该被配对」。

正确语义应是：**对每个归一化资源 r，取 t 时刻同时存活的所有 Claim on r，两两 `Compatible` 其 mode；冲突对 (create,create)/(move,move)/(release,release) 即报警**。设计未指定「先按资源分组」这一步，若直接对所有存活 Claim 两两配对（跨资源），则 `Compatible` 会因跨资源误配对而错误或漏报。

**最小修正**：§3.1 改写为「对 `At(t)` 内按归一化 `ResourceId` 分组，每组同时存活 Claim 两两 `Compatible.IsCompatible`；冲突即报（CONFLICT 集见 Algebra.cs:27）」。并在注释明确 `Compatible` 自身不含资源/scope 维度，分组由审计层负责。

### 焦点 6 — 确定性声称（纯函数 ⇒ 同剧本同 t 同签名）｜**成立**

`Signature.Union`（Objects.cs:180-188）以 `ImmutableHashSet<Claim>` 为存储（146-148 行），集合无序；并集由结构相等（Claim 为 position record，Objects.cs:126）去重。故 `At(t)` 两次调用对同输入产出**内容相等**的 Signature（同集合 Claims），审计读取内容 ⇒ 结果确定。`ImmutableHashSet` 确实无序可交换，`Union` 幂等/交换/结合（iter19 已证）。**确定性成立。** 仅 note：`Signature` 是 `sealed class`（非 record），引用不等但内容相等——审计逻辑以内容为准，无碍。

---

## 开放项（OPEN）

### OPEN-1（焦点2，HIGH）
`At(t)` 中 `loopScope` 自由变量；`Combination.Loop` 改写 Claim scope 丢弃逐元素 scope；脚本级 `Audit` 的 net/Peak `scope` 参数未定义。
- 反例：设计 §2.2 伪码无法直接落地——`loopScope` 无来源；若填 Global 则 scope 过滤塌缩。
- 最小修正：删 `At(t)` 内 `Combination.Loop` 调用，直接 union `e.Footprint`；`Audit` 以 `ScopeId.Global` 为 scope；per-scene 审计改由 Event 新增可选 `ElementScope`。

### OPEN-2（焦点3，HIGH）
脚本级守恒误用瞬时 `IsConserved(At(t))`，会拒绝一切合法临时占用。
- 反例：纹理 t∈[10,70] create→release，在 (10,70) 每采样点 net=[+1,+1]，IsConserved=false ⇒ 误报泄漏。
- 最小修正：守恒改为脚本级**时间聚合累积 net**（跨全部事件求和 create−release per 资源），`ContainsZero` ⇒ 闭合；瞬时判定仅用于单帧自洽校验。

### OPEN-3（焦点2/3 衍生，HIGH）— `Combination.Loop` 在 `At(t)` 中被误用
`Combination.Loop(body, ω, loopScope)` 将 size 乘 ω（DerivedMetrics.cs:56-61）。但 ω 是**时间维重复次数**（某元素存活 ω 帧），不是**同一瞬间的并发副本数**。在 `At(t)`（瞬时并集）里对存活元素用 Loop 缩放 size ⇒ 把「60 帧动画」算成「单帧占 60 倍资源」，**Peak 预算彻底失真**；ω=⊤ 更使单存活副本 size 变 `[lo,⊤]` ⇒ Peak=⊤（与 OPEN-4 叠加）。
- 反例：1 个 sprite 存活 60 帧，ω=60，`At(t)` 峰值计为 60×size，而非 1×size。
- 最小修正：瞬时 `At(t)` 对存活元素只 union 其 `Footprint` **一次**（不缩放）；ω 仅影响（a）lifetime 跨度（存活多久）与（b）累积 net/conservation 的 ×ω（时间聚合时用）。即「瞬时 Peak 用 1 份并发，累积 net 用 ω 份」——两个用途绝不能混为单个 Loop 算子。

### OPEN-4（焦点4，MEDIUM）
`Budget` ⊤ 与常驻（ω=⊤）资源的交互未澄清：常驻资源 + 有限 cap ⇒ 峰值 gate 必拒，与 §2.1「常驻背景层应通过」意图冲突。
- 最小修正：明确「未设 cap（默认 ⊤）= 通过；显式有限 cap = 拒绝（用户主动限制）」；perpetual 资源契约层标记豁免。

### OPEN-5（焦点5，MEDIUM）
`Compatible` 剧本层配对未定义（分组缺位）+ 正文「同 scope」vs 表格「同资源」歧义。
- 最小修正：明确「按归一化 ResourceId 分组后两两 `Compatible`」；`Compatible` 不含资源/scope 维度，分组由审计层负责。

### OPEN-6（LOW，示例瑕疵）
§4 JSON 示例中 `"loop": 0` 置于 release 事件（LoopCount=0 ⇒ size×0 ⇒ 该 release 贡献 0，语义为空），`"loop": 1` 置于 create（无重复）；与 `LoopCount` 语义（重复次数）不符。且 `"budget": { "commandBuffer:gpu": 64 }` 的自由字符串键与 `ResourceId.CommandBuffer("gpu")` 的归一形式不一致，契约需定义 JSON↔ResourceId 的映射。
- 最小修正：示例改为 `loop` 仅在「重复出现的元素」上 >1；预算键用规范 ResourceId 序列化（`CommandBuffer("gpu")`）。

---

## 总评

设计**代数上不成立，需修订**。核心结构（Endpoint 采样完备性 F1、确定性 F6）成立且优雅，但**三个 HIGH 级 open 直击可落地性**：

1. scope/loopScope 自由变量（OPEN-1）—— `At(t)` 无法实现；
2. 瞬时守恒误判临时占用为泄漏（OPEN-2）—— 会拒绝一切合法特效；
3. `Combination.Loop` 把时间维 ω 误当瞬时并发倍数（OPEN-3）—— Peak 失真。

三处修正方向一致且互不冲突：**`At(t)` 只做「存活事件 Footprint 瞬时并集」（不缩放、不改写 scope），守恒改为「全时间轴累积 net」，Peak 用「每存活元素 1 份并发」**。这恰好回到设计 §1.1 的初衷（剧本 = `Time → Signature`，Union 表达瞬时总占用），只是把「时间聚合」与「瞬时快照」两件事分清。MEDIUM/LOW 项（OPEN-4/5/6）为澄清与示例瑕疵，不阻塞。

**结论：需修订（6 项 open：3 HIGH / 2 MEDIUM / 1 LOW）。**
