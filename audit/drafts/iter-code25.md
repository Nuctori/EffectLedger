# 迭代25 审计（ResourceId 归一锁）

## 摘要
- 全解构建：0 错误 0 警告（`dotnet build Cosmos.EffectAlgebra.slnx` 已核实：0 错误 0 警告），测试 `dotnet test --filter FullyQualifiedName~ResourceNormalizationTests` → **19 通过 0 失败**（已核实）。
- open 项总数：0 可闭 / 2 低严重度观察（命名误导 + Custom 样本缺失，均非 false-green、非阻断）。
- 终止判定：**可终止**（§3.1.4a signal_* 等价类真锁 + 幂等全集 + 跨类不等价 + 可证伪 + 无假绿；Gpu/CommandBuffer 残差已在测试与实现双处诚实声明，属 PDR-vs-实现已知分歧，非测试缺陷）。

## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）

| 项 | 测试行号 | 被测实现行号 | PDR § | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 Self("signal_x")≡SignalBus("x") | L19-25 (L23/L24) | Objects.cs Normalize L67-76：`Self s when startsWith("signal_") => SignalBus(s.Component[7..])` | §3.1.4a / ST-02 | 真 | 否 | `self=Normalize(Self("signal_x"))=SignalBus("x")=bus`；两 `Assert.Equal` 必真。若 arm 被删/前缀逻辑坏则必红。✓ |
| 2 Signal("signal_x")≡SignalBus("x") | L27-33 (L31/L32) | Objects.cs L68-71：`Signal sig when Name.Value startsWith("signal_") => SignalBus(...)` | §3.1.4a "signal_"+s ≡ SignalBus(s) | 真 | 否 | 回指实现确认 SignalBus 前缀剥离 bug 已修：Signal→SignalBus 与 SignalBus 内部 strip 两 arm 齐全。✓ |
| 3 Self("x")≡SignalBus("x")（实为不塌缩） | L35-41 (L40) | Objects.cs L75 `_ => r`：Self("x") 无 signal_ 前缀 → 原样返回 Self("x") | §3.1.4a（仅 Self("signal_"+s) 归 SignalBus） | 真（断言正确） | 否 | 断言 `Assert.Equal(Self("x"), self)` 与实现一致；注释 L36-38 明确「Self 裸名不剥前缀、不塌缩到 SignalBus」。⚠ 测试名 `Self_Bare_Component_Equiv_SignalBus_X` 措辞误导（名称 Equiv 但断言不等价）；体+注释正确，非 false-green。 |
| 4 SignalBus("signal_x")==SignalBus("x") 自洽 | L43-49 (L46/L47) | Objects.cs L72-74：`SignalBus bus when Name.Value startsWith("signal_") => SignalBus(signal_.Length..)` | §3.1.4a | 真 | 否 | 内部也剥 signal_ 前缀；双重归一相等。✓ |
| 5 Gpu/CommandBuffer 真实语义 | L51-63 (L61/L62/L63) | Objects.cs L75 `_ => r`：Gpu/CommandBuffer 各自原样返回，为独立判别联合构造子 | §3.1.4a（PDR 表列 gpu/command_buffer ≡ CommandBuffer("gpu")） | 真（诚实残差） | 否 | 按**真实实现语义**断言：各幂等 + `NotEqual`（字段类型不同结构不等）。注释 L52-57 明言 PDR §3.1.4a 拟同形、本实现保留独立构造子、运行时别名由 §7 映射层对齐。断言 NonEqual 而非硬套相等 → 非 false-green；属 PDR-vs-实现已知分歧，双处诚实声明。 |
| 6 Memory 幂等 | L65-69 (L68) | Objects.cs L75 `_ => r` | §3.1.4a | 真 | 否 | ✓ |
| 7 Occupancy 幂等 | L71-75 (L74) | 同上 | §3.1.4a | 真 | 否 | ✓ |
| 8 Callback 幂等 | L77-81 (L80) | 同上 | §3.1.4a | 真 | 否 | ✓ |
| 9 AudioMixer 幂等 | L83-87 (L86) | 同上 | §3.1.4a | 真 | 否 | ✓ |
| 10 Input 幂等 | L89-93 (L92) | 同上 | §3.1.4a | 真 | 否 | ✓ |
| 11 Network 幂等 | L95-99 (L98) | 同上 | §3.1.4a | 真 | 否 | ✓ |
| 12 幂等性全集 | L101-126 (L122/L123/L124) | Objects.cs L75 `_ => r` + 三 strip arm | §3.1.4a（单点真相） | 真 | 否 | 17 样本（Self/Signal/SignalBus/Gpu/CommandBuffer/Memory/Occupancy×2/Callback/AudioMixer/Input/Network/Tree/Physics/Disk）循环 `Normalize(Normalize(r))==Normalize(r)`；除 `Custom` 外覆盖全部构造子。✓（见观察-2） |
| 13 跨类不等价 | L128-146 (L145) + L148-160 | Objects.cs Normalize（保留构造子标签） | §3.1.4a（归一保留规范形标签） | 真 | 否 | Theory 6 标签（tree/memory/gpu/audio/callback/input）均 `NotEqual(signalNorm)`；显式 L150-156 `Tree("x")≠SignalBus("x")`、`Memory≠Gpu`。证明未全塌缩到 SignalBus。✓ |
| 14 可证伪 | 全局 | 全局 | — | 是 | 否 | 等价 arm 删除/前缀逻辑改坏 → 对应 `Assert.Equal` 必红；跨类 `NotEqual` 在塌缩时必红；幂等 arm 破坏 → L124 必红。 |
| 15 假绿扫描 | 全局 | — | — | 无 | 否 | 15 个 `[Fact]`/`[Theory]` 全含 `Assert.Equal`/`Assert.NotEqual` 实断言；无空 `[Fact]`、无 `Assert.True(true)`、无恒真充数。 |
| 16 出处注释 | 文件头 L1-3 + 各方法签名 | — | §3.1.4a / DO-8 | 齐备 | 否 | 文件头引 §3.1.4a + DO-8；各测试体注释带 §3.1.4a / ST-02 / ST-03。✓ |

## 观察项（非 open，低严重度）
- **观察-1（命名误导，非缺陷）**：`Self_Bare_Component_Equiv_SignalBus_X`（L35）测试名称「Equiv」但断言与注释均为「不塌缩到 SignalBus」。断言与注释正确，仅命名与语义反直觉；审阅者若只读名会误判。建议改名 `Self_Bare_Component_NotEquiv_SignalBus_X` 以免名实不符。不影响正确性、不阻断终止。
- **观察-2（样本覆盖，低）**：幂等全集（L102-120）未含 `Custom` 构造子。`Custom` 为 §3.1.2 自定义逃逸构造子，`_ => r` 平凡幂等；遗漏仅理论完整性缺口，无 SUT 风险。若欲全构造子覆盖可补一行。

## Gpu/CommandBuffer 残差说明（非 open）
PDR §3.1.4a 映射表列 `gpu/command_buffer ≡ CommandBuffer("gpu")`（同形），但本实现（Objects.cs L75）将 `Gpu(Rid)` 与 `CommandBuffer(string)` 保留为独立判别联合构造子（字段类型不同，结构相等不可同形）。该分歧在：
(a) 实现侧 `ResourceId` 注释（Objects.cs L30-42 映射表）仅注「gpu/command_buffer ⇒ CommandBuffer("gpu")」而未同步声明「本实现保留独立构造子」；
(b) 测试侧 L52-57 已诚实声明「运行时别名由 §7 白名单维度处理」。
=> 测试无 false-green、无技术债；属 PDR 文档与实现的已知分歧，建议后续在 PDR §3.1.4a 或 LANDING_PLAN §3.11 显式记录此实现取舍（out-of-scope 于本测试审计）。

## 结论
- §3.1.4a 的 **signal_* 归一等价类（Self("signal_"+s) ≡ SignalBus(s)、Signal("signal_"+s) ≡ SignalBus(s)、SignalBus("signal_"+s) ⇒ SignalBus(s)）** 三向均**真锁**：断言 `Assert.Equal` 直比实现 strip 后规范形，可证伪、无假绿。
- **幂等全集**（17 样本 Loop 断言 `Normalize∘Normalize==Normalize`）与 **跨类不等价**（Theory 6 类 + 显式 2 对 `NotEqual`）共同证明归一是「规范形如实、不同类不塌缩」的单点真相函数（DO-8）。
- **可证伪性**：每等价/不等价断言对应实现 `Normalize` switch 的特定 arm；arm 删除或前缀逻辑破坏必红，控制流近似无隐藏恒真路径。
- **无假绿**：15 测试全含实断言，无 `Assert.True(true)`。
- **出处齐备**：文件头 + 各测试体均引 §3.1.4a / DO-8 / ST-02/ST-03。
- 仅 2 低严重度观察（命名误导、Custom 样本缺失），均非 false-green、非阻断；1 项 PDR-vs-实现已知分歧已诚实声明（Gpu/CommandBuffer）。
- 终止判定：**可终止** —— §3.1.4a 等价类真锁 + 幂等 + 跨类不等价 + 可证伪 + 无假绿，达成用户铁律。
