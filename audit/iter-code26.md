# 迭代26 审计（Signature 维度隔离）

## 摘要
- 测试：194 通过 0 失败（`dotnet test` 实跑核实，net10.0，1 个测试文件）
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）
- 终止判定：**可终止**

## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）

| 项 | 测试行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 三桶独立 | BucketIsolationTests.cs L17-44 `Signature_ThreeBucketsIndependent` | Objects.cs L62-64 `ReadClaims/WriteClaims/OccupyClaims` + L95-101 `Add` 按 kind 分流 | §3.1.4b / DO-7 | 真 | 否 | 同资源 Read/Write/Occupy 三 claim ⇒ 各桶 `Single` 且 `Contains` 正确 + 对另两桶 `DoesNotContain`。若 `Add` 误分流（如 Occupy 进 Read 桶），`DoesNotContain`/`Single` 必红。可证伪。 |
| 2 net 仅 occupy | BucketIsolationTests.cs L46-63 `NetTable_OnlyOccupiesResource` | Algebra.cs L53-54 `if (c.Kind != Kind.Occupy) continue;` | §3.3.1 / DO-7 | 真 | 否 | 同资源 Read([1,1])+Write([1,1])+Occupy(Create,[5,5]) ⇒ net==[5,5]。若去掉 L54 `continue`，read/write 经 `ToSigned([1,1])=[1,1]` 被累加 ⇒ net 变 [7,7]，`Assert.Equal(5L,...)` 必红。可证伪。 |
| 3 跨资源不串 | BucketIsolationTests.cs L65-85 `NetTable_CrossResourceNotMixed` | Algebra.cs L55-59 按 `ResourceId.Normalize(c.Resource)` 键分组 + Objects.cs L49-66 `Normalize` 结构区分 | §3.1.4a(DO-6) | 真 | 否 | Tree("x")[5,5] 与 Memory(42)[3,3] 两键独立存在、值各自对应。若 `Normalize` 误将二者归一到同一键（如 Memory 与 Tree 合并），`Get` 值冲突必红。可证伪。 |
| 4 Union 幂等/交换 | BucketIsolationTests.cs L87-115 `Signature_UnionIdempotentAndCommutative` | Objects.cs L116-128 `Union` 按 kind 遍历 b 三桶 | §3.2.1 / §3.1.4b | 真 | 否 | `Union(s,s)` 三桶 `SetEquals(s)`；`Union(a,b)` 与 `Union(b,a)` 三桶 `SetEquals` + `Count` 相等。若 `Union` 重复添加或顺序依赖，集不等 ⇒ 必红。可证伪。 |
| 5 守恒判据 | BucketIsolationTests.cs L117-139 `NetTable_ConservationCriterion` | Algebra.cs L73-91 `IsConserved`（`ContainsZero` 区间含 0 / fail-closed false） | §3.3.1 DO-9 | 真 | 否 | 仅 create[5,5]⇒false；create[5,5]+release[5,5]⇒净 [-5,5] `ContainsZero`=true ⇒ true，并显式断言 `v.Lo=-5 / v.Hi=5`（锁 iter05 `ZStar` 有符号 net 修復）。若回到 ℕ* 截断（net=[1,1] 不含 0）⇒ 误判 false，必红。可证伪。 |
| 6 可证伪链 | （见 2） | Algebra.cs L54 | §3.3.1 | 真 | 否 | 经验证：去掉 L54 `continue` ⇒ 项 2 断言必红（net 由 [5,5] 变 [7,7]）。维度隔离非注释吹，可被回归捕获。 |
| 7 假绿扫描 | 全局 6 测试 | — | — | — | 否 | 无 `[Fact]` 无断言、无 `Assert.True(true)`、无 trivial 单断言。每断言对应可证伪不变量；`BucketIsolation_DoubleGuarantee`(L141-151) 以「仅 Read ⇒ 无净效应记录 ⇒ fail-closed false」二次角度加固，非恒真。 |
| 8 出处注释 | 各方法签名注释 L1/L17/L46/L65/L87/L117/L141 | Objects.cs/Algebra.cs 同级 § 标注 | §3.1.4b/§3.3.1/§8.2 | 真 | 否 | 类级 L1 引 §3.1.4b/§3.3.1/§8.2；6 方法各带 §3.1.4b/§3.3.1(DO-7)/§3.1.4a(DO-6)/§3.2.1/§3.3.1(DO-9)。满足。 |

## open 项清单
（无）

## 结论
- 6 个测试**真锁**三桶维度隔离 + net 仅 occupy：项 1 三桶互不串（§3.1.4b）、项 2 read/write 不进 net（§3.3.1 DO-7）、项 3 跨资源键不混（DO-6）、项 4 Union 幂等/交换（§3.2.1）、项 5 守恒含 0 判据（DO-9，并锁 iter05 有符号 net）、项 6 可证伪链确认「去 `continue` ⇒ 必红」。
- **无假绿**：无空断言、无恒真；`BucketIsolation_DoubleGuarantee` 为 fail-closed 二次角度，非充数。
- **可证伪充分**：每个被测定律若未在实现中落实（误分流/误纳入 read/write/乱归 key/重复添加/截断有符号），对应断言必红或 `IsConserved` 必错。
- 唯一可记（非 open，低）：`BucketIsolation_DoubleGuarantee` 与项 2 语义重叠（仅 Read 角度），属加固冗余而非缺覆盖。
- 终止判定：**可终止**（0 open；维度隔离真锁 + 可证伪 + 无假绿 + 出处精准）。
