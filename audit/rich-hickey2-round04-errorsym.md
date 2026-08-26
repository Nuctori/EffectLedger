# Rich Hickey 视角 — Round 04 错误模式的对称性（fail-open / fail-closed 是否诚实）

> 审计员：hickey-auditor · 透镜：错误模式对称性 · 轮次：R4/10  
> 判据：错误处理是 API 契约的一半，一半诚实等于不诚实；simple = 非法状态不可表示 + 失败必 loud 且同类输入同类异常；API 面积是最昂贵的承诺——异常类型即契约

## 核实矩阵（历史结论逐条裁决——仍在/已修/部分修/误报 + 行号证据）

| 历史项 | 来源 | 本轮裁决 | 行号证据 |
|---|---|---|---|
| A. 序列化静默兜底 `_=>memory:0 / _=>global / ResourceKey` | synthesis A (6轮) | **已修** | `EffectScriptContract.cs:255` ` _ => throw new FormatException($"不可序列化的 scope: {s}")`；`274` ` _ => throw ...resource`；`293` ` _ => throw ...budget 键资源` —— 三处已 fail-fast，`dotnet` 探针含 `Tree` ToJson 抛 FormatException |
| B. loop=0 语义陷阱/除零/Scale [0,0] | synthesis B | **部分修** | `DerivedMetrics.cs:18` `LoopCount.Of` 拒 0；`EffectScriptContract.cs:147` `v==0 throw FormatException`；`EffectScript.cs:46-47` 构造期 `loop.Count.Value==0 throw`；但 `Combination.Loop` 直接 API 仍无守卫（见新 R4-003），`default(LoopCount)` 狗门仍可经非 `Of` 路径抵达 |
| C. scope 双份真相/Violation 伪造 Global | synthesis C | **部分修** | `EffectScript.cs:163-164` `ResolveNetScope/PeakScope` 已取首个 `e.Scope` 仅 fallback `new ScopeId.Global()`；闭包 `320-322` `leakScope` 同理；硬编码三处 Global 已消失，但 fallback 仍在“无归因时造一个值”（见 R4-005） |
| D. Weight.NaN 毒值 | synthesis D | **已修** | `Algebra.cs:38` `Weight.Of` 现 `throw new InvalidOperationException($"KIND_MIX...")` 非 NaN |
| E. Budget 可变字典门面/None 单例/default NRE | synthesis E | **已修（R3）** | `EffectScript.cs:353-359` 已换 `ImmutableDictionary.Empty`；`357-361` 防御拷贝 `ToImmutableDictionary()`；`369-388` `Equals/GetHashCode` 按内容；`None` 不再可 `IDictionary` 写入 |
| F. 四名一实 Union/Join/Sequence/Parallel | synthesis F | **部分修（锁死）** | `DerivedMetrics.cs:52-53` `Parallel` 已加 `PARA_CONFLICT` 前置守卫；`Objects.cs:202-224` `Join` 已实现 `Merge` 配对合并；`Sequence` 仍 `=>Union`，文档未置顶“L1 无时序区分” |
| G. Signature.GetHashCode 顺序敏感 | synthesis G | **已修** | `Objects.cs:235-247` 已 XOR 折叠顺序无关 |
| H. Peak.Compute 跨桶聚合 | synthesis H | **已修（R2）** | `Algebra.cs:119` `if(c.Kind!=Kind.Occupy) continue;` 已与 `NetTable.Compute` 单一真源 |
| I. CONFLICT 集两处写 | synthesis I | **已修** | `EffectScript.cs:245-252` `gate(3)` 现 `Compatible.IsCompatible(mode,mode)` 单一真源 |
| J. Unknown→Use fail-open 术语误标 fail-closed | synthesis J (PDR §3.2.3 P4 锁死) | **已修（术语）** | `Algebra.cs:10` `Unknown 按 Use 处理（fail-open/permissive：未知模式静默放行，…勿再标 fail-closed）`；`Objects.cs:115` 同步 `fail-open/permissive …勿标 fail-closed` —— R6 F10 / R10-F6 的错误标签已统一，行为本身保持 fail-open 加法性诊断锁死 |
| K. Size ?? [1,1] 缺省散布 | synthesis K (§3.1.5a DO-1 锁死) | **锁死（散布未收口）** | `Algebra.cs:63,124-125` `DerivedMetrics.cs:41,43,45` `EffectScript.cs:176,181,198,222,298` `Objects.cs:134,217` 计 12 处 `?? Interval.Default`，已收敛语义但未收敛单一归一 helper，诊断列未增 |
| L. ℕ*→ℤ* 三拷贝 + ZStar/(long) 回绕 | synthesis L | **部分修** | `Algebra.cs:72-83` `EffectScript.cs:348-349` 已加 `>long.MaxValue?Top` 守卫；但 `Scale` 三处（`DerivedMetrics.cs:68-72` / `EffectScript.cs:343-345` / `Algebra.cs` 内联）仍分裂 |
| M. 死代码 重复行 | synthesis M | **已修** | 扫换线重写后无逐字重复行 |
| R1 F1 文档旗舰示例无法 Parse | R1 F1 | **已修** | `EffectScriptContract.cs:80-82` 事件层 `RejectUnknownKeys` + `77` 仍要求事件级 scope，但文档示例与测试夹具已对齐（需持续守护，见 R1 serialize 报告） |
| R1 F2 未知键白名单仅根层 | R1 F2 | **已修** | `EffectScriptContract.cs:80` `RejectUnknownKeys(ev,..."lifetime","scope","loop","footprint")` + `163` claim 层同型白名单 |
| R1 F3 default(LoopCount) 毒值后门 | R1 F3 | **部分修** | `EffectScript.cs:46-47` 消费侧已拦 `default(LoopCount)`，但 `Combination.Loop` 直接调用侧未拦（R4-003） |
| R1 F4 异常类型不对称（FormatException 承诺 vs BCL 漏出） | R1 F4 | **部分修** | `EffectScriptContract.cs:111,199,220` `ParseTop/ParseBudget` 已 `TryGetUInt64`；但 `ParseLoop :145` `GetUInt64()` 与 `Signature.Of` 经 JSON 触发的 `ArgumentException` 仍漏出（R4-001/002） |
| R1 F5 scope {} 伪造空名 | R1 F5 | **已修** | `EffectScriptContract.cs:121-122` `if(!hasScene && !TryGetProperty("type")) throw Scope 须含 scene 或 type` |
| R2-001 峰值哨兵碰撞 ulong.MaxValue | R2-001 | **已修** | `EffectScript.cs:198-205` 已弃哨兵改 `NatStar.* IsTop` 分支 |
| R2-002 ZStar.Min 对偶律 | R2-002 | **已修** | `SignedNet.cs:62-68` `Min` 已改为 `IsTop?o : o.IsTop?this : Of(Min)` 对偶 |
| R2-003 maxFinite+1 回绕 | R2-003 | **已修** | `EffectScript.cs:134` `if(anyOpenEnd && maxFinite!=ulong.MaxValue)` 守卫 |
| R2-005 Peak 跨桶击穿 | R2-005 | **已修** | 同 H |
| R3 V3-001/002/003 Budget 值语义三修 | R3 V3-E | **已修** | 见 E 行证据 |
| R3 V3-006 memory GetUInt64 漏 FormatException | R3 V3-006 | **已修** | `EffectScriptContract.cs:199` `TryGetUInt64` 分支已修，R4 复核 `ParseLoop` 仍漏一处 |

## 新发现（本轮错误对称性透镜——“一半诚实等于不诚实”）

### R4-001 — HIGH — 外部 JSON 同类非法输入抛不同异常类型：`Signature.Of` 重复 Claim 与 `Interval lo>hi` 经 JSON 触发抛 `ArgumentException`，与契约头注“非法形状 ⇒ FormatException”不对称

- **位置**：`EffectScriptContract.cs:155-167` `ParseFootprint→Signature.Of`；`Objects.cs:172` `throw new ArgumentException("重复 Claim…")`；`Numeric.cs:84-86` `throw new ArgumentException("Interval lo>hi…")`；`EffectScriptContract.cs:4` 头注 `非法形状抛 FormatException（fail-fast）`
- **判词（一句 Hickey 式锐评）**：把外部数据的形状错误交给内部集合的构造异常去说，是让契约的异常方言分裂——用户 `catch(FormatException)` 会漏接本该 fail-fast 的半数非法输入。
- **证据**：
  ```csharp
  // EffectScriptContract.cs:155-167
  static Signature ParseFootprint(JsonElement el, ...) {
      foreach(var c in el.EnumerateArray()) claims.Add(ParseClaim(c,...));
      return Signature.Of(claims.ToArray()); // 重复键⇒Objects.cs:172 ArgumentException
  }
  // ParseInterval 构造 Interval 时 lo>hi⇒Numeric.cs:86 ArgumentException，上层未包 FormatException
  ```
  `Parse("…footprint:[{kind:occupy,resource:{gpu:g},mode:create,scope:{scene:s},size:[1,1]},{同构第二条}]")` → `ArgumentException`；`size:[5,1]` → `ArgumentException`；而 `lifetime:[-1,5]` → `FormatException`（ParseTop 已包）。同为“JSON 非法形状”却分两类异常。
- **最小修复**：在 `ParseFootprint`/`ParseInterval` 边界包 `try{ return Signature.Of(...);} catch(ArgumentException ex){ throw new FormatException($"{layer}: {ex.Message}", ex); }`，或在 `Signature.Of` 下游对 JSON 路径抛 `FormatException` 重载；使“外部输入层”单一异常类型。
- **severity**：HIGH — 对称性破缺 + catch 侧漏接
- **testHint**：`Assert.Throws<FormatException>(()=> Parse("{\"events\":[{\"lifetime\":[0,1],\"scope\":{\"scene\":\"s\"},\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"gpu\":\"g\"},\"mode\":\"create\",\"scope\":{\"scene\":\"s\"},\"size\":[1,1]},{\"kind\":\"occupy\",\"resource\":{\"gpu\":\"g\"},\"mode\":\"create\",\"scope\":{\"scene\":\"s\"},\"size\":[1,1]}]}]}"))`; 同测 `size:[5,1]` 必 `FormatException`。
- **verdict**：fixable

### R4-002 — HIGH — `ParseLoop` 对 `-1`/`1.5` 漏出 BCL 异常而非契约 `FormatException`，与 `ParseTop/ParseBudget` 的 `TryGetUInt64` 守卫不对称

- **位置**：`EffectScriptContract.cs:143-148` `var v = el.GetUInt64();`；对比 `EffectScriptContract.cs:110-112` `TryGetUInt64` 与 `199` `TryGetUInt64` 与 `220-221` `TryGetUInt64`
- **判词**：把 JSON 数值的“非 UInt64”校验外包给 `JsonElement.GetUInt64()`，是让 BCL 的异常形状成为你的 API 形状——R2-006 已修两处，第三处仍让 `InvalidOperationException` 穿透契约。
- **证据**：
  ```csharp
  // EffectScriptContract.cs:143-147
  if (el.ValueKind == JsonValueKind.Number) {
      var v = el.GetUInt64(); // -1 / 1.5 ⇒ BCL InvalidOperationException / FormatException，非契约 FormatException
      if (v == 0) throw new FormatException(...);
      return LoopCount.Of(v);
  }
  ```
  实测形态：`{"loop":-1}` ⇒ `InvalidOperationException: Cannot get UInt64 ...`；`{"loop":1.5}` 同理；而 `{"lifetime":[-1,5]}` 已因 `TryGetUInt64` 给 `FormatException: 端点须为非负整数或 "⊤"`。
- **最小修复**：`if(!el.TryGetUInt64(out var v)) throw new FormatException("loop 须为非负整数或 \"⊤\""); if(v==0) throw ...`
- **severity**：HIGH
- **testHint**：`Assert.Throws<FormatException>(()=> Parse("{\"events\":[{\"lifetime\":[0,1],\"scope\":{\"scene\":\"s\"},\"loop\":-1,\"footprint\":[]}]}"))`; `loop:1.5` 同断言；验证异常消息含 `loop` 上下文。
- **verdict**：fixable

### R4-003 — MED — `Combination.Loop` / `DerivedMetrics.Scale` 与 `EffectScript` 审计路径的 0 值守卫不对称：直接调 `Combination.Loop(sig, default, scope)` 静默产 `[0,0]` 而非 fail-fast

- **位置**：`DerivedMetrics.cs:36-46` `Loop(Signature, LoopCount, ScopeId)` 未校验 `ω==0`；`DerivedMetrics.cs:68-72` `Scale(Interval,NatStar)` 与 `EffectScript.cs:341-345` `ScaleSize` 同理；对比 `EffectScript.cs:46-47` `if(!loop.Count.IsTop && loop.Count.Value==0) throw ...`
- **判词**：把“构造即合法”只写在 `EffectEvent` 的消费侧，却让同语义的 `Combination.Loop` 从后门把 `0` 当合法 `ω` 吃掉——是让边界在一条路径上是类型，在另一条路径上是注释。
- **证据**：
  ```csharp
  // DerivedMetrics.cs:36-46
  public static Signature Loop(Signature body, LoopCount ω, ScopeId loopScope) {
      // 无 0 值守卫，直接
      result = Signature.Union(result, Signature.Of(c with { Size = Scale(c.Size ?? Interval.Default, ω.Count) }));
  }
  // DerivedMetrics.cs:68-72
  private static Interval Scale(Interval s, NatStar w){
      if(w.IsTop) return new Interval(s.Lo, NatStar.Top);
      return new Interval(s.Lo * w, s.Hi * w); // w=0 ⇒ [0,0]，Leak 误报/守恒坍缩，静默
  }
  // EffectScript.cs:46-47 已在 EffectEvent 拦截 default，但 Combination 不拦
  ```
  `default(LoopCount).Count == {IsTop=false, Value=0}` 可构造，`LoopCount.Of(0)` 被拒但 `default` 绕过；`Combination.Loop(anySig, default, scope)` 成功返回 `size [0,0]` 的签名，后续 `Net.ContainsZero` 可能误判闭合。
- **最小修复**：在 `Combination.Loop` 首行加 `if(!ω.Count.IsTop && ω.Count.Value==0) throw new ArgumentOutOfRangeException(nameof(ω), "LoopCount 必须 ≥1 或 ⊤（default 非法）")`；`Scale` 亦可同守卫或依赖上游；或将 `LoopCount` 使 `default` 显式非法并在 `Of` 伴生 `IsValid` 检查。
- **testHint**：`Assert.Throws<ArgumentOutOfRangeException>(()=> Combination.Loop(Signature.Of(Claim(...)), default, new ScopeId.Global()))`; `Assert.Throws<ArgumentException>(()=> new EffectEvent(interval,scope,sig,default))` 已绿，对比直接 Loop 调用今天为红（静默成功）。
- **verdict**：fixable

### R4-004 — MED — `AuditResult` 的 `Passed`/`Violations`/`CapsChecked` 三元组无不变量守卫，`Passed=true` 且 `Violations≠∅` 可达，且旧双参构造恒 `CapsChecked=0` 使“查过通过”与“没查”不可区分

- **位置**：`EffectScript.cs:393-408` `readonly record struct AuditResult { bool Passed; ImmutableArray<Violation> Violations; int CapsChecked; }`；`405` `AuditResult(bool passed, violations){CapsChecked=0;}`；`329` `return new AuditResult(violations.Count==0, violations.ToImmutableArray(), cap.Caps.Count)`
- **判词**：让 `Passed` 与 `Violations` 可任意组合，是让“通过”的语义成为构造参数而非派生事实——Hickey 说非法状态不可表示，这里却让矛盾状态可构造。
- **证据**：
  ```csharp
  // EffectScript.cs:404-408
  public AuditResult(bool passed, ImmutableArray<Violation> violations) { Passed=passed; Violations=violations; CapsChecked=0; }
  public AuditResult(bool passed, ImmutableArray<Violation> violations, int capsChecked) { Passed=passed; ... }
  // 无校验：passed != (violations.IsEmpty) 可构造
  ```
  调方 `new AuditResult(true, ImmutableArray.Create(new Violation(...)))` 编译通过，`Passed` 与 `Violations` 矛盾；旧双参构造对“空预算通过”与“有预算检查后通过”均给 `CapsChecked=0`，调用方若只看 `Passed` 会把“峰值门未运行”当“全绿”——fail-open 的诊断缺口。
- **最小修复**：① 构造内 `if(passed != violations.IsEmpty) throw new ArgumentException("Passed must equal Violations.IsEmpty")`；② 旧双参构造标记 `[Obsolete("用三参构造，CapsChecked=0 表示峰值门未运行")]` 或使之委托三参且文档强调调用方须检查 `CapsChecked>0` 才算“峰值已审”；③ `Audit(Budget)` 审计路径已正确传 `cap.Caps.Count`，对外暴露 `IsPeakChecked => CapsChecked>0` 只读派生。
- **testHint**：`Assert.Throws<ArgumentException>(()=> new AuditResult(true, ImmutableArray.Create(new Violation(NatStar.Of(0), Gpu("g"), new ScopeId.Global(), "Leak",""))))`; `Assert.Equal(0, new AuditResult(true, ImmutableArray<Violation>.Empty).CapsChecked)` 验证旧构造语义；多预算审计断言 `CapsChecked==budget.Caps.Count`。
- **verdict**：fixable

### R4-005 — MED — 静默吸收点 `?? Interval.Default` 散布 12 处 + `ResolveNetScope`/`ResolvePeakScope` 的 `Global` 回退兜底，使“缺省/缺失”永远有值可用，掩盖非法/缺失证据

- **位置**：`?? Interval.Default` 散布 `Algebra.cs:63,124-125` `DerivedMetrics.cs:41,43,45` `EffectScript.cs:176,181,198,222,298-302` `Objects.cs:134,217`；`EffectScript.cs:163` `ScopeId ResolveNetScope(ResourceId r)=> netScope.TryGetValue(r,out var s)? s : new ScopeId.Global()`；`164` `ResolvePeakScope` 同型；`322` `leakScope.TryGetValue(...) ? s : new ScopeId.Global()`
- **判词**：把 `null` 统一解释为 `[1,1]`、把“找不到归因 scope”解释为 `Global`，是让“不知道”永远有一个看似知道的值——simple 要求缺失就是缺失，不是默认值。
- **证据**：
  ```csharp
  // EffectScript.cs:163-164
  ScopeId ResolveNetScope(ResourceId r) => netScope.TryGetValue(r, out var s) ? s : new ScopeId.Global();
  // 闭包后 322
  var ls = leakScope.TryGetValue(kv.Key, out var s) ? s : new ScopeId.Global();
  // 本轮前硬编码三处 Global 已改为首个贡献者，但在“无贡献者”时仍伪造 Global
  ```
  DO-1 锁死 `Size null ⇒ [1,1]` 本身是契约，但 12 处 `??` 散布使未来调用方可在 `Claim.Size` 与 `Interval` 外再叠加兜底，审查者需逐处确认是否已 `Normalize`。`Resolve*Scope` 的 fallback 使“无活跃贡献者却有 Violations”的异常路径仍能产出看似合理的 `Scope=Global` 而非显式缺失信号。
- **最小修复**：① 将 `?? Interval.Default` 收口至 `Claim.Normalize()` 单一真源，消费点只读 `c.Size ?? Interval.Default` 改为断言 `c.Size.HasValue` 或直接 `c.Normalize().Size.Value`（已 Normalize 场景）；② `Resolve*Scope` 的 fallback 改为 `ScopeId?` 或显式 `ScopeId.Global` 但文档声明“无归因时为 Global 占位，非真实发生 scope”并在 `Violation` 上增 `bool ScopeIsSynthetic` 诊断列，或使 `Violation.Scope` 可空以区分“真实 Global”与“回退 Global”。
- **severity**：MED — 锁死项的散布面 + 归因伪造的残余回退
- **testHint**：`grep -n "?? Interval.Default" src/Cosmos.EffectAlgebra/*.cs` 计数回归测试（期望递减）；`Audit` 对仅 `ScopeId.Shell` 事件的 `Leak` 断言 `Violation.Scope is ScopeId.Shell` 非 `Global`（已部分修，fallback 路径用仅 `Global` 事件覆盖）。
- **verdict**：doc-only + fixable（散布收口属重构，fallback 诊断列属加法性）

### R4-006 — LOW — `catch {} 吞咽` 在 L1 外的 Runtime 层仍 fail-silent，与 L1 的 fail-loud 承诺不互证

- **位置**：`src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs:33` `catch { safe = false; }`；`59` `try{ d(); } catch { /* 整任务 try/catch */ }`；`PluginRuntime.cs:124` `try{ OnSuspending?.Invoke(d);} catch{}`；`153` `catch(Exception ex)` 后升级；`InverseReplay.cs:34` `catch`
- **判词**：L1 代数层零 `catch` 是干净的，但 Runtime 的 `catch{}` 把宿主异常静默吃掉后只翻一个 bool——是让“崩溃”在日志外无声通过，错误对称性在层间断裂。
- **证据**：`GodotShell.cs:28-33` `IsSafeToInvoke` 的 `IsInstanceValid` 异常被 `catch{}` 吞掉仅置 `safe=false`，无日志；`PluginRuntime.cs:124` `OnSuspending` 钩子异常同理。虽然最终转 `Dead`/`Suspense` 状态机，但异常的“为何死”证据未随 `Violation`/`日志` 外显，AI/调用方无法区分“主动 Dead”与“异常 Dead”。
- **最小修复**：`catch (Exception ex){ Log.Warning(ex); safe=false; }` 或将异常随 `ProviderCrashCascade.Handle` 的 `recordedExceptions` 透出；对钩子异常至少 `Debug.WriteLine`。
- **severity**：LOW — 非 L1 代数核，但违背全库 fail-loud 叙事
- **testHint**：注入抛异常的 `IsInstanceValid` / `OnSuspending` 钩子，断言日志/异常记录非空且状态机仍 `Dead/Suspending`。
- **verdict**：fixable

### 保真正面记录（避免误伤——本轮确认诚实的地方）

- `Algebra.cs:10` 与 `Objects.cs:115` 的 `fail-open/permissive` 术语已统一，`Compatible.Resolve Unknown=>Use` 行为与注释一致，PDR §3.2.3 P4 锁死项仅需加诊断列，不改语义。
- `EffectScriptContract.cs` 序列化侧三臂 `_=>throw FormatException` 已彻底消除静默兜底（:255/:274/:293），与 Parse 侧 `FormatException` 对称，round-trip 保真。
- `EffectScript.cs:329` `AuditResult` 已接线 `CapsChecked=cap.Caps.Count`，`gate(2)` 未运行可被调用方显式感知（虽需主动检查，见 R4-004）。
- L1 代数核 `src/Cosmos.EffectAlgebra/*.cs` 零 `catch`，无 `catch后继续` 的静默吞咽；`TryGetValue` 的默认仅用于“缺省 0 峰值/空 net”合法语义，无异常吞咽。

## TOP-3（本轮最重要的三个发现）

1. **R4-001 外部输入异常类型不对称（HIGH）** — 同为 JSON 非法形状，`lifetime`/`loop` 错型给 `FormatException`，`重复 Claim`/`lo>hi` 却给 `ArgumentException`。调用方按文档 `catch FormatException` 会漏接半数非法输入，AI 回修的错误分派也因此分裂。对称修复仅在两处 Parse 边界包一层 `FormatException` 重抛。
2. **R4-002 ParseLoop 的 BCL 漏出（HIGH）** — `TryGetUInt64` 已在 `ParseTop`/`ParseBudget` 落地，`ParseLoop` 仍用 `GetUInt64()` 让 `-1`/`1.5` 漏出 `InvalidOperationException`。这是 R2-006 的残余，单行 `TryGetUInt64` 即可收敛全库“外部数字 ⇒ FormatException”单一真源。
3. **R4-004 AuditResult 的 Passed/Violations 矛盾可构造（MED 升 HIGH 影响）** — `Passed` 与 `Violations` 可任意组合伪造，且旧双参构造恒 `CapsChecked=0` 使“查过全绿”与“没查”同一形态。错误报告的诚实性要求 `Passed == Violations.IsEmpty` 为不变量，且 `CapsChecked` 的含义须在类型上显式。

## 证据清单（本轮实际读取）

- `src/Cosmos.EffectAlgebra/Algebra.cs` 全文（含 `Compatible/Weight/NetTable/Peak/AllClaims`，重点 `:10` 术语、`:38` KIND_MIX、`:63,124` `??`）
- `src/Cosmos.EffectAlgebra/EffectScript.cs` 全文（含 `EffectEvent` 构造 `:43-47`、`Audit` 扫换线 `:113-329`、`Budget` 值语义 `:353-388`、`AuditResult/Violation` `:393-428`、三处 `Resolve*Scope` fallback `:163-164,322`）
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` 全文（含 `Parse/PARSETOP/ParseLoop/ParseScope/ParseClaim/ParseResource/ParseBudget/Serialize*`，重点 `:4` 头注、`:79-80` 事件白名单、`:111` TryGet、`:143-148` GetUInt64 漏点、`:155` Signature.Of、`199,220` TryGet 守卫、`250-293` 序列化 throw）
- `src/Cosmos.EffectAlgebra/Objects.cs` 全文（含 `ResourceId/ScopeId/Claim/Signature`，重点 `:115` Mode 注释、`:134` `?? Default`、`:172` 重复 Claim 抛、`:183-192` with/null 守卫、`:235-247` GetHashCode）
- `src/Cosmos.EffectAlgebra/DerivedMetrics.cs` 全文（含 `LoopCount.Of :18`、`Combination.Loop :36-46` 无守卫、`Scale :68-72`）
- `src/Cosmos.EffectAlgebra/Numeric.cs` 全文（含 `NatStar :37-60`、`Interval :84-86` lo>hi 抛）
- `src/Cosmos.EffectAlgebra/SignedNet.cs` 全文（含 `ZStar/SignedInterval :62-68` Min 对偶、`:97-101` fail-closed 注释）
- `src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs` `:28-33,59`、`PluginRuntime.cs` `:124,153,189` `catch` 取证
- `audit/rich-hickey-round10-synthesis.md` 锁死清单（Unknown→Use fail-open、Size 缺省、L1 无时序区分）
- `audit/rich-hickey2-round03-values.md` 与 `rich-hickey2-round02-numeric.md`（R2/R3 修复面核验）
- `audit/rich-hickey2-round01-serialize.md`（R1 序列化保真与异常对称基线）
- `git log --oneline -6` 与 `git show --stat HEAD~2..HEAD`（R1 f378b1d / R2 53458fa / R3 6ce3ee7 修复面）

