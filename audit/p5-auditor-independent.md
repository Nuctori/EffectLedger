# 独立对抗审计报告（第八轮）

> 审计员：独立对抗审计员（第八轮，与前七轮无共享上下文）
> 目标：`D:\Godot\Cosmos`（Cosmos.EffectAlgebra，Godot C# 资源效应代价代数）
> 方法：只读审计 + 系统临时目录可复现工程（`dotnet 10.0.103`，L1/Runtime/Analyzer/Generator Debug DLL 直引，Roslyn 4.12 驱动）
> 范围：跨层一致性、C# 语义陷阱、Analyzer 真实工程漏报、Generator 边界、退出排空/看门狗/OnSuspending 组合重入
> 临时工程在报告完成前已删除；仓库内文件零修改。

---

## 总评

前七轮把 L1 代数骨架、方言、供应链、并发边界、性能曲线、QED 声明都做了很深的覆盖，本轮刻意避开已冻结面，专打**「两层判定互相矛盾」「HashSet 枚举顺序」「非方法体的语法形状」「运行时排空重入」**四类边缘。

结论：**发现 12 条，其中 9 条判定为实现缺陷**（5 条新缺陷 + 4 条「声明边界窄于实际行为」），3 条为防御生效（攻击失败，记录在案）。

最重要的一条是 **P5-8-01（CRITICAL）**：`NetTable`/`NetBenefitClosure` 的有符号加法在**长整型求和溢出边缘**与**`ImmutableHashSet<Claim>` 的枚举顺序**耦合，而枚举顺序依赖字符串哈希随机化的进程种子 ⇒ **同一个合法、数学上净值为 0 的 Fiber，在不同进程运行中会随机被 `LoadAll` 判为「未闭合」而拒载**。实测 40 次进程运行中 9 次误拒（≈22.5%）。这条不是「已声明的 fail-closed 保守」（保守方向应当确定性地拒绝），而是**同输入跨进程结果抖动**，直接违反 README 的确定性承诺并会让 CI 随机红。

跨层方面，README 诚实边界 #11 只声明了「scope ⊄\* fiber.Scope（如 Global）不参与该 fiber 守恒」，但实测 **`⊆*` 全体跨标签组合（Scene↔Type↔Method↔Global 两两，共 9 组）都发生 L1 报泄漏 / Runtime 静默放行**；此外 Runtime 还缺少 L1 的两个 gate（瞬时 `NegativeDip`、`ω=⊤` 居民豁免）——**声明边界明显窄于实际行为**。

Analyzer 侧确认了一批**未声明的漏报形状**（表达式体属性/索引器、运算符重载、转换运算符、字段初始化器、事件 add/remove 访问器、静态构造函数）；同时发现一个**未声明的误报**（方法内声明但从未调用的局部函数仍参与 acquire 计数）。Generator 在 partial/接口默认实现/async/表达式体方法/转义关键字/跨类型同名/`A_B` 与 `A.B` 撞名等形状下产物正确，无缺陷。

排空/看门狗/重入组合本轮**打不穿**（逆执行恒为 1 次，无零次或两次路径），仅发现 `AttachShell` 静默覆盖用户 `OnSuspending` 钩子这一 LOW 级缺口。

---

## 发现清单

| # | 严重度 | 攻击向量 | 实证复现步骤/代码 | 预期 vs 实际 | 判定 |
|---|--------|----------|-------------------|--------------|------|
| P5-8-01 | **CRITICAL** | `NetTable.Compute` 的 `SignedInterval.Add`（`ZStar +` 溢出⇒⊤，非结合）叠加 `ImmutableHashSet<Claim>` 枚举顺序（依赖 `string.GetHashCode()` 进程随机种子） | 见下方最小复现 M1；同一 exe 连跑 40 次，结果在 `Conserved=true/LoadAll=OK` 与 `Conserved=false/LoadAll=REJECTED` 间抖动（9/40 误拒） | 同一合法 net=0 Fiber 应确定性通过装载；实际按进程哈希种子随机拒载（抛 `LoadValidationException`） | **实现缺陷** |
| P5-8-02 | HIGH | 跨层：Runtime `NetTable.Compute` 的 `⊆*` 过滤 vs L1 `EffectScript.Audit` 无过滤 | `fiber=Type("S")`、claim scope=`Scene("S")`（或任意两个不同标签），Runtime `Conserved=true`，L1 `Audit.Passed=false(Leak)` | README #11 声明差异仅限「scope ⊄\*（如 Global）」；实际**全部 9 组跨标签组合**都分歧 | **声明边界窄于行为（超声明）** |
| P5-8-03 | HIGH | 跨层：Runtime 无瞬时态判定（只有最终闭合），L1 有 `NegativeDip` gate | release(t0..5)→create(t10..20) 同一资源：L1 `Passed=false`（2×NegativeDip），Runtime `Conserved=true` | README 未声明两层在「瞬时净负」上的口径差；Runtime 静默放行 L1 捕获的瞬时下溢 | **声明边界窄于行为（超声明）** |
| P5-8-04 | HIGH | 跨层：L1 `ω=⊤` 居民层豁免在 Runtime 不存在 | `create` 不上界事件（`loop:⊤`）：L1 `Passed=true`，Runtime 同 claim `Conserved=false`（判泄漏） | README 未声明 Runtime 缺失居民豁免；同一「常驻资源」两层结论相反（方向为 Runtime 更严=安全，但属未声明口径分裂） | **声明边界窄于行为（超声明）** |
| P5-8-05 | HIGH | Analyzer EAA0901 只注册 `MethodDeclarationSyntax` | 见下方 M2：表达式体属性/索引器、`operator +`、`implicit operator`、字段初始化器、事件 `add` 访问器、`static` 构造函数中含未配对 acquire，全静默 | README #9 仅列「构造函数、属性访问器、`using var`」；实际漏报面远大于此（含运算符重载/转换运算符/表达式体属性/索引器/字段与事件访问器/静态构造） | **声明边界窄于行为（超声明，漏报）** |
| P5-8-06 | MEDIUM | Analyzer EAA0901 对「声明但从未调用」的局部函数仍计数 | `void M() { void Helper() { ResourceLoader.Load("x"); } }` 报 EAA0901，尽管 Helper 从不执行 | 应静默（无运行时 acquire）；实际误报 | **实现缺陷（误报）** |
| P5-8-07 | MEDIUM | `PluginRuntime.AttachShell` 无条件覆盖 `OnSuspending` | 先 `rt.OnSuspending = f => ...` 再 `rt.AttachShell(shell)`，teardown 级联时用户钩子 0 次调用 | 用户自设钩子应保留或 loud 冲突；实际被 `shell.CascadeProcessModeDisabled` 静默替换 | **实现缺陷（静默覆盖，未声明）** |
| P5-8-08 | MEDIUM | L1 `EffectScript.Audit` 闭包路径同样在溢出边缘把守恒判成 ⊤（确定性但错误方向） | 4 claim 同资源 `create 2^62`+`create 2^62+1`+`release 2^62`+`release 2^62+1`：数学 net=0，`Audit` 报 `Leak` | 溢出检测应有「先抵消再溢出判定」语义或至少与 L1 声明一致；实际把可守恒剧本判成泄漏（fail-closed 但语义不正确，且是 P5-8-01 的 L1 同源） | **实现缺陷（同源，确定性但错误）** |
| P5-8-09 | MEDIUM | `Fiber.EffectiveSignature` 折入 create(Provides) 造成 fail-closed 误拒（A3-14 自称「已知锐边」，但无测试钉住可复现形态） | Effect 已含不同 size 的 `release(Provides)` 时，折入另一 `create(Provides)` 后 net 多减 | 注释承认；本轮确认可构造 `Conserved=false` 的合法 Fiber（无测试红线） | **已知锐边（复现确认，无钉）** |
| P5-8-10 | LOW | `GodotShell.EnqueueExitDrain` 可被重复注册相同 Action | `EnqueueExitDrain(d)` ×2 → `FlushExitDrain` 调 `d` 两次 | 无去重（`Defer` 有去重，drain 无）；第三方 drain 可能双执行 | **实现缺陷（低危，文档未声明）** |
| P5-8-11 | LOW | `EffectScriptContract.ToJson` 对 `ScopeId.Global` 输出 `{"type":"global"}` 与 README「混 scope 往返破裂」描述 | 与 #15 一致，非新缺陷；仅登记为「已声明」 | 一致 | 防御生效/已声明 |
| P5-8-12 | INFO | `NatStar.Min/Max` 与 `CompareToFinite` 在全部 ⊤/有限组合一致；`Claim` 含 `Interval?` 的 `==`/`GetHashCode` 正确 | 枚举 6 组 ⊤/有限组合，`max==max(cmp)`、`min==min(cmp)` 全成立；`null` vs `Exact(1)` 不等、`Normalize` 后相等 | 应一致/正确；实际一致/正确 | 防御生效（无缺陷） |

---

### 最小复现

#### M1 — P5-8-01（Runtime 跨进程非确定性拒载）★最高严重度

```csharp
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;

var S = (ScopeId)new ScopeId.Scene("S");
ulong a1 = 1UL << 62, a2 = (1UL << 62) + 1;
var r = new ResourceId.Custom("R");

// 关键：用 Signature.Union 逐条并（等价于生产代码 Fiber.EffectiveSignature 的折叠路径）
var effect = Signature.Empty;
foreach (var c in new[] {
    new Claim(Kind.Occupy, r, Mode.Create,  S, (Interval?)Interval.Exact(a1)),
    new Claim(Kind.Occupy, r, Mode.Create,  S, (Interval?)Interval.Exact(a2)),
    new Claim(Kind.Occupy, r, Mode.Release, S, (Interval?)Interval.Exact(a1)),
    new Claim(Kind.Occupy, r, Mode.Release, S, (Interval?)Interval.Exact(a2)) })   // 数学 net = [0,0]
    effect = Signature.Union(effect, Signature.Of(c));

var prov = new ResourceId.Custom("prov");
var coeffect = new Coeffect(new ResourceId.Custom("req"), prov, S);
var inv = new InverseClaim(prov, S, () => { }, ImmutableHashSet.Create("free"));

var fiber = new Fiber(new FiberId("f"), effect, coeffect, ImmutableStack<InverseClaim>.Empty.Push(inv));
Console.WriteLine(NetBenefitClosure.Check(fiber).Conserved);   // 期望恒 True

var rt = new PluginRuntime();
rt.Register(new FiberSpec(new FiberId("f"), effect, coeffect,
    ImmutableStack<InverseClaim>.Empty.Push(new InverseClaim(prov, S, () => { }, ImmutableHashSet.Create("free")))));
try { rt.LoadAll(); Console.WriteLine("OK"); }
catch (LoadValidationException) { Console.WriteLine("REJECTED"); }   // 期望恒 OK
```

**实测（同一 exe 连跑 40 次，每次新进程 = 新字符串哈希种子）**：

```
REJECTED: 9 / 40     （≈22.5% 进程把合法 net=0 Fiber 判为未闭合而拒载）
```

对照单次输出可见枚举顺序决定结果：

```
seed=  -779644653 order=[R,R,C,C] Conserved=False LoadAll=REJECTED
seed=   -1702892274 order=[C,R,R,C] Conserved=True  LoadAll=OK
seed=   1113873442 order=[C,C,R,R] Conserved=False LoadAll=REJECTED
```

根因链：
1. `Signature` 用 `ImmutableHashSet<Claim>` 存桶，`AllClaims()` 的枚举顺序 = HashSet 桶序，依赖 `Claim.GetHashCode()` → `ResourceId.Custom("R").GetHashCode()`（**`string.GetHashCode()` 受进程级哈希随机化影响**，实测跨进程种子不同）。
2. `SignedInterval.Add` → `ZStar +`：溢出 ⇒ 保守 `⊤`（`SignedNet.cs:36-46`）。`⊤` 一旦出现即不可逆（`⊤ + x = ⊤`）。
3. 故「两个大正项相邻」的枚举顺序得 `⊤ ⇒ ContainsZero=false ⇒ 不守恒`；「create/release 交替」的枚举顺序得 `0 ⇒ 守恒`。
4. `ZStar +` 因此**非结合**，而 `NetTable.Compute` 把它当结合求和用，且输入顺序不确定。

影响面：`NetBenefitClosure.Check/CheckAll`（§5 闸门）、`LoadValidation.VerifyNetClosure`（`LoadAll` 内必调）、`LoadValidation.ValidateForLoad`（每个 Fiber）。即**每个使用该库的游戏工程，其插件能否装载会随进程哈希种子抖动**；CI 上表现为随机红，与 README「确定性/可复现」承诺冲突。

#### M2 — P5-8-05（Analyzer 漏报形状）

在 Roslyn 驱动（Analyzer Debug DLL + `Godot` 命名空间桩）下逐形状编译，`GetAnalyzerDiagnosticsAsync()`：

```
[EAA0901] BASELINE method leak            (方法体 acquire 无 release，基线)
[       ] expression-bodied property leak  public Resource R => ResourceLoader.Load("x");
[       ] expression-bodied indexer leak   public Resource this[int i] => ResourceLoader.Load("x");
[       ] operator overload leak           public static A operator +(A x, A y) { ResourceLoader.Load("x"); return x; }
[       ] conversion operator leak         public static implicit operator Resource(A x) { ResourceLoader.Load("x"); return null; }
[       ] field initializer leak           private readonly Resource _r = ResourceLoader.Load("x");
[       ] event add accessor leak          event Action E { add { ResourceLoader.Load("x"); } remove { } }
[       ] static constructor leak          static A() { ResourceLoader.Load("x"); }
[EAA0901] local function not called        void M() { void Helper() { ResourceLoader.Load("x"); } }   ← 误报
[EAA0901] partial method impl leak         partial void M(); partial void M() { ResourceLoader.Load("x"); }
[EAA0901] nested class method leak
[EAA0901] record primary ctor body leak
```

漏报根因：`EffectAlgebraAnalyzer.OnCompilationStart` 只 `RegisterSyntaxNodeAction(..., SyntaxKind.MethodDeclaration)`（`EffectAlgebraAnalyzer.cs:176`）。上述形状均**不是 `MethodDeclarationSyntax`**，故整类不被扫描。README #9 只列了「构造函数、属性访问器、`using var`」——`operator`/`implicit operator`/表达式体属性/索引器/字段初始化器/事件访问器/静态构造函数**均未声明**。

误报根因：`AnalyzeMethod` 用 `method.DescendantNodes().OfType<InvocationExpressionSyntax>()`，**包含方法内声明的局部函数的函数体**，不分析可达性/调用关系，故未调用的局部函数仍计入 acquire。

---

## 攻击失败记录（试过但打不穿的，附原因）

1. **`NatStar.Min/Max` 与 `CompareToFinite` 不一致**（重点 2 第三项）——枚举 `(⊤,⊤)/(⊤,x)/(x,⊤)/(x<y)/(x>y)/(0,ulong.Max)` 六组，`Max == (cmp>=0?a:b)` 与 `Min == (cmp<=0?a:b)` 恒成立。`Min` 的 `(IsTop,o.IsTop)` 分支覆盖正确，无 rich-hickey2 R2-002 类残留。**打不穿（实现正确）**。

2. **`Claim` 含 `Interval?` 的 `==`/`GetHashCode`**（重点 2 第二项）——`Nullable<Interval>` 的 `Equals` 正确区分 `null` 与 `Exact(1)`：`c(null)==c(Exact(1))` 为 `False`、哈希不等；`Normalize()` 后二者相等且 `Signature.Of` 按规范化键判重抛「重复 Claim」。**打不穿（实现正确）**。

3. **`ImmutableHashSet<Claim>` 枚举顺序影响 `Audit` 的违例顺序**——`Audit` 的 sweep 按事件序（`sweep.Sort` 时间序）累加，gate(1)/(3) 的字典迭代顺序只影响**违例上报顺序**，不影响 `Passed`（`PeakExceeded` 有 `peakReported` 去重，`Leak` 为闭包一次性）。**部分打穿但无害**（仅排序抖动，非判定抖动）；真正有害的是 M1 的 `NetTable` 求和（已单列为 CRITICAL）。

4. **`SynchronousExitDrain` + `OnSuspending` + `TickWatchdog` 组合造成逆执行零次或两次**（重点 5）——构造四类组合：①逆 Action 重入 `TickWatchdog`；②逆 Action 调 `SynchronousExitDrain()`；③逆 Action `Unload()` 自身再 `TickWatchdog`；④`OnSuspending` 内调 `SynchronousExitDrain`；⑤逆 Action 对同批兄弟 `BeginTeardown`（陈旧任务路径）；⑥宿主重复调 `SynchronousExitDrain`。**全部逆执行恰好 1 次**（`replayCount==1`），无零次/两次。防线：`ReplayInProgress` 重入 loud 抛（`InverseReplay.cs:28`）、排空入口清队 + 快照、`Dead`-skip 陈旧任务、`TeardownEnqueued` 幂等守卫、`ForceTeardownOnWatchdog` 仅 Active/Suspending 转入队。**打不穿（防御生效）**。

5. **Analyzer 经局部函数/lambda/try-catch/模式匹配/switch 表达式间接 acquire 漏报**（重点 3 部分）——这些形状的 `InvocationExpressionSyntax` 都是方法体的 **后代节点**，全部命中并报 EAA0901。**打不穿**（真正漏报的是非 `MethodDeclarationSyntax` 形状，见 P5-8-05）。

6. **Generator 在 partial/接口默认实现/async/表达式体方法/运算符重载上的正确性**（重点 4）——`GetAnnotatedMethod` 只认 `MethodDeclarationSyntax`，故：partial（含 decl+impl）、接口默认实现、async、表达式体方法均生成正确 `Compute{M}`；`operator`/表达式体属性**不生成**（与 Analyzer 同源盲区，非「生成错误」）。转义关键字 `@class` 正确剥离 `@` 生成 `Computeclass`；跨类型同名经 `(MethodName, FullType)` + 递增下标消歧；`A_B` vs `A.B`、`A_B_C` vs `A.B.C`、`namespace A.B` vs `A.B_C` 三种 `fullType` 撞名形态都由递增下标化解为不同 hint/成员，无 `CS0111`/AddSource 覆盖。**打不穿（实现正确）**。

7. **Generator `memberName` 与既有成员撞名导致 `CS0111`**——同一 partial 类两处声明同名方法（重载）得 `_G_A_0` / `_G_A_1`，两嵌套同名类型得不同 `fullType`，均无冲突。**打不穿**。

8. **`Audit` sweep gate(1) 与 `NetTable.Compute` 在普通（非溢出）输入上不一致**——`create1+release2`、`release-then-create`、双 create 重叠峰值等用例，`Audit` 结论与 `NetTable.IsConserved` 方向一致（`NegativeDip`/`Leak` 对应 `Conserved=false`）。仅在溢出边缘（M1/P5-8-08）与跨层口径（P5-8-02/03/04）分歧。**普通输入打不穿**。

9. **`Interval` 构造子 `[⊤,5]` 绕过**——`new Interval(NatStar.Top, NatStar.Of(5))` 抛 `ArgumentException`（下界 ⊤ 而上界有限非法），`[5,⊤]` 合法。与 §3.1.5 声明一致。**打不穿**。

---

## 附：本轮未覆盖/建议后续

- `formal/` 下 89 条 Dafny 定律是否对「`ZStar +` 结合律」有显式建模：本轮的 M1 表明**实现层的 `+` 非结合**，若 Dafny 侧把 net 求和建模为任意顺序折叠，则定律与实现存在语义鸿沟（建议核 `formal/` 中 net/Σ 相关定律是否假设结合律）。本轮未展开 Dafny 侧（超出临时工程能力）。
- `P5-8-09` 的 `Fiber.EffectiveSignature` 多减形态建议补一条可复现钉，避免锐边随重构漂移。
