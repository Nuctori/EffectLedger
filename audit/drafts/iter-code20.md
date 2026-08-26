# 迭代20 审计（L2 生成真实签名）

## 摘要
- 全解构建：0 错误 0 警告（已核实 `dotnet build Cosmos.EffectAlgebra.slnx`）；测试 `dotnet test` 经 `ToolingTests` 过滤实际执行 **6 通过 0 失败**（含 3 条 L2 生成器测试 + 3 条 L3 分析器测试），非 grep 假绿。
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**。

## 逐条核对（回指行号 + 被测 + PDR/LANDING § + 结论）

| 检查 | 行号 | 被测 | PDR/LANDING § | 结论 |
|---|---|---|---|---|
| 1 真委托 L1（非桩） | EffectAlgebraGenerator.cs L89-99（`Compute{Name}` / `{Name}_Claims`）；ToolingTests.cs L42-58 真断言 | `GodotApiWhitelist.All` + `Signature.Union`/`Signature.Of` 实为生成代码内联调用；返回 `Signature.Union(baseSig, {Name}_Claims())`，`{Name}_Claims()` 遍历白名单并按名 Union `Signature.Of(c)`。无任何 `return baseSig; // TODO` 桩 | §L2 / §14 L2 | 真：生成代码是「§7 白名单 Claims 组合成 Signature」的真正翻译，数学全在 L1 |
| 2 生成代码可运行消费 | ToolingTests.cs L74-107 `Generator_EmittedCompute_AddChild_ReturnsWhitelistedClaims` | 构造含生成器的编译 → `Emit` 到 MemoryStream → `Assembly.Load` → 反射 `EffectAlgebraGenerated.ComputeAddChild` → 传入 `Signature.Empty` 调用 → 取 `AllClaims` → 断言非空且含 `Kind.Occupy` | §7.1（AddChild Claims 含 `Oc(Tree(...),Create,Exact(1))`）/ §14 L2 | 真：反射执行成功、返回 Signature 含白名单 Occupy Claim（非仅 grep 文本）；emit 无错、断言通过（执行实证 6/6） |
| 3 属性匹配（长短名） | EffectAlgebraGenerator.cs L60-72 `HasAttributeName`；ToolingTests.cs L29（长名 `EffectOverrideAttribute`）/ L75（短名 `EffectOverride`） | `text == simpleName \|\| text == simpleName + "Attribute"`（`IdentifierName`/`QualifiedName` 两种语法名均覆盖）；无属性方法 `GetAnnotatedMethod` 两 flag 均 false ⇒ 返回 null 被 `.Where(m => m is not null)` 过滤 | §8.3 / §14 L2 | 真：短名 `[EffectOverride("r")]` 与长名 `[EffectOverrideAttribute("r")]` 测试均通过；普通方法不误判 |
| 4 每方法独立 | EffectAlgebraGenerator.cs L80-110（`canon` 按 `MethodName` 派生；`Compute{Name}` + `{Name}_Claims` 各自独立） | 标注方法 `M`（L29）生成 `ComputeM`；`AddChild`（L75）生成 `ComputeAddChild`；二者互不覆盖。`public static partial class EffectAlgebraGenerated` 跨 .g.cs 合并，方法名唯一键 ⇒ 无串桶 | §14 L2 | 真：每方法独立 `Compute_<name>` + `<name>_Claims`，按方法名规范化键隔离 |
| 5 数学在 L1 | EffectAlgebraGenerator.cs 生成体 L89-99 | 生成代码仅调用 `Signature.Union`/`Signature.Of` 并按方法名匹配白名单；无 net/Peak/Compatible 重算（net/Peak/Compatible 在 L1 运行期完成，§3.3.1） | §3.1–§3.3 / LANDING_PLAN §1 | 真：生成层零代数重算 |
| 6 残差诚实 | EffectAlgebraGenerator.cs L1-17（类注释）+ L88-91（方法注释）+ L93-95 | 明言「数学在 L1」「本代码仅按方法名匹配 §7 白名单并 Union 出 Signature，不重算代数」「L2 残差（honest）：方法体语义绑定靠『方法名↔白名单键』规范化匹配」「真实调用级闭合由运行期 Σnet 判定（DO-9 近似见 L3）」 | §14 L2 / LANDING_PLAN §4 | 真：近似性两处明言，无假绿、无 TODO 埋雷 |
| 7 约束（用户铁律） | EffectAlgebraGenerator.cs L89/96/99 | 生成代码用 L1 真实类型 `global::Cosmos.EffectAlgebra.Signature` 与 `GodotApiWhitelist.All`（返回 `ImmutableArray<ApiMapping>` 其 `Claims` 为 `Claim[]`）；Claim 经 `Signature.Of(c)` → `Add` → `Normalize` 强制归一，无 `new` 裸对象 | §3.1.1/§3.1.4a | 真：类型边界由 L1 构造子/归一保证，生成器只搬运语法位置 |
| 8 零 Godot 依赖 / 零魔法数 | `grep -c "using Godot"` = 0；EffectAlgebraGenerator.cs L96-99 | 生成体遍历 `GodotApiWhitelist.All`（§7 强类型数据），API 名取白名单键（运行时规范化 `.Replace(".","").Replace("_","").ToLowerInvariant`），无硬编码 API 串、无魔法数 | §7 / LANDING_PLAN §3（L1 零 Godot） | 真：生成器 + 生成代码均零 Godot 引用，API 名全源自白名单 |

## open 项清单
无。

## 备注（非 open，低严重度，供父级知晓）
- **规范化规则双写**：生成器 C# 侧算 `canon`（L83-84：`Where(c => c != '.' && c != '_').Select(char.ToLowerInvariant)`），生成体内又对白名单键重做 `.Replace(".","").Replace("_","").ToLowerInvariant()`（L97）。二者当前等价；若未来规范化规则变更需同步两处，否则可能失配。属可接受的轻量重复（未构成正确性缺陷，测 `AddChild`/`Audio.Play` 同源路径已覆盖）。
- **匹配粒度为方法名**：L2 是「方法名↔白名单键」的翻译层（§14 L2 明确），故方法体内部真实调用图不被静态展开——此即 L2 残差，已诚实注释，运行期 Σnet（L1）+ L3 近似补位。非 open。

## 结论
L2 升级后**确为真委托 L1 的翻译层**：生成代码内联 `GodotApiWhitelist.All` + `Signature.Union`/`Of`，非桩；测试以反射执行实证 `ComputeAddChild` 返回含 `Kind.Occupy` 的 Signature（真可运行消费）；属性长短名都匹配、普通方法不误判；每方法独立互不串；代数全在 L1 不重算；残差诚实（方法名匹配近似 + 运行期权威）；类型边界由 L1 强制、零 Godot、零硬编码魔法数。8 条审计项全部为「真」，0 open。
**终止判定：可终止。**
