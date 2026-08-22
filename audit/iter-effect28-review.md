# iter-effect28-review — 四相位扫换线等价性核查

判定：**PASS**

## 1. 四相位 ⇔ alive ⇔ Lo≤t≤Hi（含端点）
- `Alive` 定义：`EffectScript.cs:267` `Lo≤t ∧ (Hi=⊤ ∨ t≤Hi)`，`At` 注释 `:76` 同义，含端点。
- 扫换线 `sweep` 构造：`120-126` enter@Lo / exit@Hi（Lo=⊤ 跳过，永不存活）。
- 采样点 `samplePoints`：`97-112` 仅含全部有限 Lo/Hi 端点 + 开放尾代表点 + 空脚本 t=0。
- 四相位 `:228-235`：相位1 `time<tv`（早解析）⇒ 相位2 `time==tv && enter`（t=Lo 进活动集）⇒ 相位3 审计 `:232` ⇒ 相位4 `time==tv && !enter`（t=Hi 仍存活，之后移除）。
- 因 `sweep.Sort :130` 保证同时间 enter 先于 exit，且 At 分段常数仅于有限端点跳变，故在每采样点 tv 处：活动集 ⇔ {e | Lo≤tv≤Hi}，端点 t=Lo、t=Hi 均被精确包含。✓ 等价于 alive 定义。

## 2. ReferenceAudit 为独立 public-API 重写 + 集合相等断言
- `ReferenceAudit :583` 仅用 public API（`s.Events`、`s.At(t)`、`Combination.Loop`、`ResourceId.Normalize`、`Compatible.IsCompatible`、`:631-674`），逐点全算 + 两两枚举（`:667-672` 双重 for 枚举同组异事件对），语义即旧 `At`/逐点全算版。
- `Iter26_SweepLine_EqualsBruteForce_Reference_Random100 :688`：100 随机脚本，`ViolationKeys`（t,resource,scope,kind，忽略 Detail，`684`）用 `Assert.Equal` 验证 `s.Audit(cap)` 与 `ReferenceAudit(s,cap)` 集合相等。✓

## 3. 一致性细节核验（无歧义）
- gate(3) 简化：扫换线按 `(res,scope,mode)` 仅对 create/move/release 同 mode ≥2 报冲突（`:210-219`）；参考实现按 `(res,scope)` 做全 `IsCompatible` 两两枚举（`:667-672`）。但 `Compatible.IsCompatible`（Algebra.cs:18-34）仅 3 类 CONFLICT：create×create / move×move / release×release，`Use` 恒兼容且跨 mode 良性配对均兼容 ⇒ 两实现冲突集一致。✓
- gate(1) closure Leak：闭包段 `:238-256` 与 ReferenceAudit `:616-626` 同为「全部 Lo≤closureT 的有限 ω 累积净含 0」。✓
- 居民层豁免（ω=⊤ 跳过 net）两侧一致（`:145` vs `:618`/`:639`）。✓

## 4. 测试执行
- 本机 `dotnet test` 因 MSBuild/NuGet 任务无法加载（`NuGet.Build.Tasks.WarnForInvalidProjectsTask` 缺 `Microsoft.Build.Utilities.v4.0`）而失败——属环境问题，非代码缺陷，**未能实际跑测**。等价性结论基于静态逐行核对。

## 结论
PASS — 四相位精确等价于 alive ⇔ Lo≤t≤Hi（含端点）；ReferenceAudit 为独立 public-API 逐点重写，等价测试以集合相等断言覆盖。需在他机构建环境补跑 `Iter26_*` 系列测试以获得运行期证据。
