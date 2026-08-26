# Iter10 审计 — §8 效应推导层：白名单/默认规则/override 的完备性与可靠性（独立审计 #10，hy3 单独进程，本轮重跑）

- **审计视角**：推导层 soundness / 覆盖率 / 信任边界（独立 pass #10，全新上下文）
- **范围**：§8.1（L511-518 白名单/默认规则）、§8.2 ED-001..008（L520-531）；邻接 §7 映射、§3.2 组合律、Iter09（scope 不统一）、Iter01（Unknown⊤）、Iter07（L2/L3 完备性未证）
- **结论摘要**：§8 的推导层用「白名单精确映射 + 未映射 API 默认最大效应」的二分策略，但**默认规则同时是漏报源与误报源**，且白名单覆盖率极低（声称 ~100 个 vs §7 实际列 ~50 个 vs Godot 数千效应 API）。结论：(1) 默认规则 `read(unknown,use) ∪ write(unknown,use)` 漏报未映射 API 的 occupy/release（释放效应静默消失 ⇒ DO-8/DO-9 失效，高 open）；(2) 对纯读 API 误报 write（违背 DO-7 量纲隔离，open）；(3) 所有未映射 API 共享 `unknown` 资源 ⇒ Unknown⊤ 大规模保守冲突（MA-010，open 精度）；(4) `[EffectOverride]` 无任何校验 ⇒ 开发者可关闭审计（信任边界，安全 open）；(5) ED-001..008 一律「已收敛」但建立在保守估计/override/静态假设上，**无 soundness 证明** ⇒ 实际为 asserted。结构性成立（白名单完备+准确 ⇒ 默认规则不触发 ⇒ sound）给条件证明。

---

## J1. §8.1 默认规则的双向误差源（核心缺口）

**命题** §8.1（L514）：`默认规则：未映射 API 默认 { read(unknown, use), write(unknown, use) }`。

**数学性质 / 证明状态**：
- **(PO-I10-a) 漏报 occupy/release（open，高，阻断 DO-8/DO-9）**：默认规则只产出 read/write，不产出 occupy/release。任何「未映射但真实占用/释放资源」的 API（如未列入白名单的 `Instantiate`/`QueueFree`/`Connect`/`Disconnect`/`Play`/`Stop` 变体、第三方/自定义 Godot 方法）的占用/释放效应被**静默丢弃** ⇒ net 与 Peak 漏算占用 ⇒ 泄漏检测（DO-9）与峰值检测（DO-8）失效。文档声称「默认最大效应」，但「最大效应」不含 occupy/release（维度缺失，交叉 DO-7 量纲隔离缺失），实为**最大但缺维** ⇒ 非真保守。
  - 状态 = open（高）。
- **(PO-I10-b) 误报 write 于纯读 API（open，违背 DO-7）**：对纯读 API（如 `GetNode`/`IsActionPressed`/`GetSlideCollisionCount`，见 Iter09 H5）默认附 `write(unknown,use)` ⇒ 与真实读行为冲突 ⇒ 误报写入、污染读/写量纲隔离（DO-7）。状态 = open。
- **(PO-I10-c) 共享 unknown 资源致 Unknown⊤ 大规模冲突（open，精度，R-3）**：所有未映射 API 的 Claim 共享 `resource=unknown`（§3.1.2 无 `unknown` 显式构造子，MA-010 `Unknown⊤` 保守策略）。按 §3.2.3 `Compatible` 第1析取 `use∧use 同资源兼容` ⇒ 不同未映射 API 之间 `read(unknown)` 互兼容，但 `write(unknown)∧write(unknown)` 同资源 ⇒ 任意两个未映射写 API 被判「同一资源写冲突」⇒ 大规模误报（R-3）。状态 = open（精度，MA-010 同根）。
- **(PO-I10-d) 默认规则精度无界（open）**：默认规则使「映射层精度」完全取决于白名单覆盖率；覆盖率低 ⇒ 偏差无界（交叉 Iter09 PO-I9-b / Iter11 RT-002）。

**文档行号**：§8.1（L511-518）、§3.1.2（L88-93）、§3.2.3（L133-141）、§1 DO-7/DO-8/DO-9（L19-21）、MA-010（L189）。

---

## J2. 白名单覆盖率缺口（open）

**命题** §8.1（L513）：「白名单映射：核心 Godot API（约 100 个方法）」。

**数学性质 / 证明状态**：
- **(PO-I10-e) 白名单规模自相矛盾（open，事实）**：文档声称「约 100 个」，但 §7.1-7.10 实际逐条列出的 API 仅约 50 个（GetNode/GetTree/AddChild/RemoveChild/QueueFree/MoveChild/Position×2/GlobalPosition×2/Rotation×2/Scale×2/MoveAndSlide/ApplyForce/ApplyImpulse/GetSlideCollisionCount/GetSlideCollision/Load/LoadInteractive/Instantiate/Preload/EmitSignal/Connect/Disconnect/IsConnected/DrawMesh/DrawRect/SetMaterialOverride/Play/Stop/SetVolumeDb/IsActionPressed/JustPressed/GetMousePosition/Rpc/RpcId/动画Play/Stop/Seek 等约 50）。声称 100 而实列 50 ⇒ 至少一半白名单条目未在文档中给出映射 ⇒ 不可核验。
- **(PO-I10-f) Godot 效应 API 数千（open，覆盖率极低）**：Godot 的 `Node`/`Object`/`PhysicsBody`/`AudioStreamPlayer`/`MultiplayerAPI` 等暴露的效应性方法数以千计，白名单 ~100 覆盖 <5% ⇒ 默认规则主导绝大多数 API 的推导 ⇒ §8 推导层在真实工程中**退化**为默认规则（双向误差主导）。状态 = open。

**文档行号**：§8.1（L513）、§7 全文（L421-507）。

---

## J3. [EffectOverride] 信任边界（安全 open）

**命题** §8.2 ED-001 收敛方案：「允许 [EffectOverride] 修正」。

**数学性质 / 证明状态**：
- **(PO-I10-g) [EffectOverride] 无校验（open，安全）**：文档允许 `[EffectOverride]` 覆盖某 API 的默认/白名单 Claim，但**未定义任何校验** —— 开发者可写 `[EffectOverride]` 将 `QueueFree` 标注为空 Signature（零效应），从而**静默关闭**泄漏/峰值检测。该 override 的「正确性」无任何静态/运行时证明机制 ⇒ 审计信任链在 override 处被人为切断。状态 = open（安全相关，阻断 DO-9/DO-8 数学良定义）。
- 交叉：ED-001 的「已收敛」建立在「允许 override 修正」机制存在上，而非「override 被证明正确」⇒ 实为 asserted（同 Iter07 I7-07 模式）。

**文档行号**：§8.2 ED-001（L520-522）、§8.1（L514）。

---

## J4. ED-001..008 收敛真伪（表）

| ID | 发现 | 文档状态 | 实际审计状态 | 说明 |
|----|------|---------|-------------|------|
| ED-001 API 白/黑名单 | 已收敛 | **asserted** | 机制存在（白名单+默认+override），但覆盖率低（PO-I10-e/f）、override 无校验（PO-I10-g）、默认规则漏报 occupy（PO-I10-a）⇒ 实际未收敛，仅机制层成立。 |
| ED-002 属性访问效应模糊 | 已收敛 | **asserted** | 依赖 L2 Generator 区分 getter/setter（Iter07 PO-I7-c 不完备 ⇒ 漏决策式控制流）；且 §7.2 属性 setter 标 `write(..,use)` 的 kind×mode 耦合未定义（Iter08 I8-06）⇒ 属性效应精度未证。 |
| ED-003 回调/委托推导 | 已收敛 | **asserted** | 信号连接分析右侧方法 + 动态委托保守估计；保守估计退化为 Unknown⊤（PO-I10-c）⇒ 精度无界；未证 soundness。 |
| ED-004 场景实例化动态性 | 已收敛 | **asserted** | 动态 Instantiate 标 ∞（Iter09 I9-06）；但 ∞ 的 Peak 语义依赖未定义的 S×ω（Iter02 I2-05 / Iter04 PO-I4-a）⇒ 收敛不实。 |
| ED-005 资源共享去重 | 已收敛 | **asserted** | L2 解析 .tscn 按 uid 去重；但 .tscn 外资源（运行时加载、网络 resource）未按 uid（Iter09 I9-05）⇒ 去重范围未证完备；依赖 Claim 相等（Iter01 I1-02 未立）。 |
| ED-006 _Process 频率累加 | 已收敛 | **asserted** | 静态 60fps 假设 + 运行时采样校准；校准偏差 >20% 仅报警（Iter11 RT-002/004）⇒ 长期偏差窗口存在，峰值安全非严格保证。 |
| ED-007 yield/await 时序 | 已收敛 | **asserted** | AsyncEffect<T> + 保守假设 async_scope 效应持续到方法结束；「保守假设」无形式证明（可能过估或过低估，取决于 await 点），soundness 未证。 |
| ED-008 CallDeferred 延迟 | 已收敛 | **asserted** | 保守假设 deferred 效应立即发生以保证峰值安全；「保守」成立需证「立即发生 ⊇ 实际时序」——但 deferred 效应可能**永远不发生**（条件未触发）⇒ 过估峰值（安全但浪费预算），且无证明。 |

**结论**：ED-001..008 全部为 **asserted（机制成立，soundness 未证）**，无一真正 discharged。文档 §8.2 一律「已收敛」是**过度声称**（交叉 Iter07 I7-01 / Iter04 I4-03）。

---

## J5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若白名单**完备**（覆盖全部效应 API）且**准确**（每条映射与真实 Godot 效应一致，含 occupy/release 不漏），则默认规则永不触发 ⇒ 推导整体 sound（漏报/误报均为 0）。证明：默认规则仅在未映射时触发，完备 ⇒ 无未映射 ⇒ 不触发。前提 PO-I10-e/f（覆盖率）未立 ⇒ 条件证明，实际未消解。
- **P2（discharged，条件）**：给定 `[EffectOverride]` 受静态校验（override 的 Signature 必须等于该 API 经 §7 规则推导的真实 Claim，否则编译错），则 override 不破坏 soundness。证明：校验保证 override = 真实 ⇒ 链不切断。前提 PO-I10-g（无校验）未立 ⇒ 条件。
- **P3（discharged，条件）**：给定 `unknown` 资源被显式建模为 `Unknown⊤` 且与 `resource` 字段互斥（不进入正常 `Compatible` 比较），则默认规则仅贡献 Unknown⊤ 保守冲突、不污染正常维度。证明：构造分离。前提 MA-010 的 `Unknown⊤` 数学对象未定义（Iter01 I1-03）⇒ 条件。

---

## Proof Obligation 账本（Iter10）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I10-a | 默认规则漏报 occupy/release | open(高) | 默认规则含 occupy/release 或强制映射 | L514, L19-21 |
| PO-I10-b | 默认规则误报 write 于纯读 | open | 区分读/写默认或穷举白名单 | L514, L19 |
| PO-I10-c | unknown 共享致 Unknown⊤ 冲突 | open(精度) | 定义 unknown 处理/分离 | L514, L88-93, L189 |
| PO-I10-d | 默认规则精度无界 | open | 见覆盖率+MA-005 | L514, L184 |
| PO-I10-e | 白名单 100≠实列 50 | open(事实) | 补齐 100 映射或修正声称 | L513, L421-507 |
| PO-I10-f | Godot 数千 API 覆盖率<5% | open | 提覆盖率或降级默认规则 | L513 |
| PO-I10-g | [EffectOverride] 无校验 | open(安全) | 加 override 校验/受限白名单 | L520-522, L514 |
| PO-I10-h | ED-001..008 soundness 未证 | open | 逐条补 soundness 证明 | L520-531 |

---

## 本轮新发现未消解缺口（I10- 前缀，全局唯一）

- **I10-01（高）**：默认规则只产 read/write、不含 occupy/release ⇒ 未映射 API 的占用/释放效应静默漏报，DO-8/DO-9 失效（与 Iter08 I8-01 QueueFree mode=move 叠加，泄漏检测两端失守）。
- **I10-02**：默认规则对纯读 API 误报 write ⇒ 违背 DO-7 量纲隔离。
- **I10-03**：所有未映射 API 共享 `unknown` 资源 ⇒ Unknown⊤ 大规模保守冲突（R-3），MA-010 同根。
- **I10-04**：白名单声称「约 100 个」但 §7 实列约 50 个，且 Godot 效应 API 数千 ⇒ 覆盖率极低，推导层退化默认规则主导。
- **I10-05（安全）**：`[EffectOverride]` 无任何校验，开发者可静默关闭审计 ⇒ 信任边界切断。
- **I10-06**：ED-001..008 一律「已收敛」但无 soundness 证明，实为 asserted（过度声称，交叉 Iter07 I7-01）。
- **I10-07**：ED-008 的「deferred 效应立即发生」过估峰值（安全但浪费预算），且「立即 ⊇ 实际」无证明；deferred 可能永不触发 ⇒ 保守方向成立但精度无界。

---

一句话摘要：§8 推导层用「白名单+默认最大效应」二分，但默认规则既漏报 occupy/release（DO-8/DO-9 失效，高）又误报纯读 write（违 DO-7），白名单覆盖率<5%，`[EffectOverride]` 无校验可关审计，ED-001..008 全为 asserted 无 soundness —— 无一真正收敛。
