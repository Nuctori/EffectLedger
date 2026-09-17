# 可选类型行为约束（Behavior Contracts）

状态：pre-release 实验特性。本文描述**已实现**的行为；未实现项在「支持范围」节逐条列出。

目标计划：[总体计划](behavior-contracts-plan.md)、[分阶段工作包](behavior-contracts-phases.md)、[需求与保证边界](behavior-contracts-requirements.md)。

## 这是什么

给一个普通 C# 类型加一条声明，选择一套**可静态检查的行为规则**。检查覆盖该类型的实现及其可解析的依赖，用来尽早发现隐藏输入、外部修改、可变别名与构造期逃逸。

不需要 Godot、不需要 EffectLedger 的资源模型或 Runtime，也不需要实现任何业务无关成员。

## 快速开始

### 1. 引用（发布前走源码引用）

本模块尚未发布到 nuget.org。当前按源码引用：

```xml
<ItemGroup>
  <!-- 声明面 -->
  <ProjectReference Include="..\..\src\EffectLedger.Contracts\EffectLedger.Contracts.csproj" />
  <!-- 分析器必须以 OutputItemType="Analyzer" 接线，裸引用不会产生任何诊断 -->
  <ProjectReference Include="..\..\src\EffectLedger.Contracts.Analyzer\EffectLedger.Contracts.Analyzer.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 2. 声明角色

```csharp
using EffectLedger.Contracts;

public sealed class PriceCalculator : IConstrained<DeterministicComputation>
{
    public decimal Calculate(OrderSnapshot order, PricingRules rules)
    {
        return order.Subtotal * rules.Multiplier;
    }
}
```

`dotnet build` 即会报出 `EBC*` 诊断。把诊断升为 error 即成门禁：

```
[*.cs]
dotnet_diagnostic.EBC2001.severity = error   # 隐藏变化输入
dotnet_diagnostic.EBC2002.severity = error   # 外部写入 / IO
dotnet_diagnostic.EBC2003.severity = error   # 入口稳定性（可变集合/指针不得作确定性入口）
dotnet_diagnostic.EBC1001.severity = error   # 不可变值被修改
dotnet_diagnostic.EBC1002.severity = error   # 内部可变别名泄露
dotnet_diagnostic.EBC1003.severity = error   # 构造期 this 逃逸
dotnet_diagnostic.EBC9001.severity = error   # 未知依赖（最常触发；漏了它门禁形同虚设）
```

> **不要把 EBC9001 留成 warning**：Unknown 是本工具最常报的一类，
> 只把前几条升为 error 会让"未验证"的代码在构建上显示为通过——那正是本工具要消除的假绿。

### 3. 严格审核（推荐用于 CI）

IDE 诊断可以被 `pragma`/`NoWarn` 抑制，所以**不要**只依赖"构建没警告"。用独立入口：

```pwsh
dotnet run --project src/EffectLedger.Contracts.Tool -- check <你的工程.csproj> -c Release --report contracts-report.json
```

退出码：`0` 通过 / `2` 存在违规或未知 / `1` 参数或构建错误。缺工程参数是用法错误（exit 1），不会当作成功。

模式：`--mode strict`（默认）按结论阻断（违规或未知 → exit 2）；`--mode advisory` 只报告不阻断（exit 0），
二者退出码**刻意不同**，便于迁移期逐步收紧。

**零受约束类型默认 exit 2**（与模式无关）——"没检查"不等于"通过"；确属预期时显式传 `--allow-empty`。

严格模式读取**真实构建输入**并按内部原始结论判定：即使 `NoWarn` 隐藏了全部 EBC 诊断、或源码里写了 `#pragma warning disable`，违规仍会以 exit 2 失败。

**源生成器**：若工程引用了源生成器、而本次编译输入中未见生成产物（`obj/`），工具会拒绝给出通过结论（exit 1）——因为生成源可能声明受约束类型而工具看不到。确认生成源不含受约束类型后，用 `--allow-generators` 显式放行。

## 两个角色

### `ImmutableValue` — 深层不可变值

承诺：在受支持的托管操作模型下，实例构造完成后，公开可观察的实例值及其可达数据保持稳定。

检查：
- 构造完成后的 receiver 字段 / 可达对象修改（EBC1001）
- 内部可变别名外泄：公开可变集合/数组、只读视图覆盖可变字段、构造期保留输入的可变引用（EBC1002）
- 构造期 `this` 逃逸：注册回调、传入未知方法、写入静态位置（EBC1003）

不承诺：`Equals`/`GetHashCode` 正确性、非空、业务不变量、序列化往返、构造不抛异常。

### `DeterministicComputation` — 确定性计算

承诺：输入的逻辑值与已声明依赖相同时，计算不依赖未声明的变化来源，也不产生外部可观察写入。

检查：
- 隐藏输入：时钟、随机、环境、文化、共享可变配置（EBC2001），含经 helper 的间接读取
- 外部写入 / IO：控制台、文件、网络、静态可写状态、修改输入参数（EBC2002）

允许：显式传入的时间值；不逃逸的局部对象与集合（`new List<int>()` 建临时集合是合法的）。

不承诺：终止性（可能不返回）、不抛异常、线程安全、交换律/幂等律（属后续 P8 范围）。

## 支持范围与已知边界

**明确报告 Unknown、绝不当作通过**的形状：

| 形状 | Unknown 原因 |
| --- | --- |
| 外部程序集/BCL 依赖无目录条目 | `ExternalSummaryMissing` |
| 开放虚/接口派发，目标未闭合 | `OpenDispatch` |
| `dynamic` 分派、函数指针 | `UnsupportedOperation` |
| 分析预算耗尽 | `BudgetExceeded` |

strict 模式下，只要根上存在任何 Unknown，该根即判定为不通过——**"没查清"不等于"没问题"**。

**已支持**（含第三轮审计后补齐的形状）：

- **`foreach` 枚举传播**：用户自定义枚举器（模式式 `GetEnumerator`/`MoveNext`/`Current`
  与 **Dispose**，含 `IEnumerable<T>` 实现与显式接口实现）的效应沿调用图传播；
  枚举器经接口返回且实现不可闭合时报 Unknown。数组与 BCL 集合不产生噪声。
- **回调的创建 / 执行 / 逃逸区分**：委托**创建**不计为执行；`d()` 执行记为"执行"，
  来源不可解析时报 Unknown；委托存入**字段/属性/数组元素/集合**均记为逃逸，
  lambda 对方法参数或 this 的**捕获**会被分析（`() => input.Clear()` 存入集合即违规）。
- **record 与 with**：record 为语言级不可变（稳定入口类型）；`with` 是 clone-then-init，
  不计为对既有状态的修改；位置属性的合成访问器无用户代码。
- **用户运算符全形态**：`+`/`==`、复合赋值 `+=`、`++` 与显式/隐式转换运算符体均传播。
- **防御性拷贝与冻结判定**：是否"真冻结"按**来源**而非按类型判断——
  `_xs = input.ToArray()`/`.ToList()`/`new List<T>(src)`（拷贝）放行；
  `_xs = input`（静态类型 `IReadOnlyList<T>` 而实参是调用方的 `List<T>`）**被拒**，
  因为调用方仍持有原引用（正是文档所说"`IReadOnlyList` 包装不算冻结"）。
  `ImmutableArray`/`ImmutableList`/`Frozen*` 等真不可变集合类型可直接持有。
- **构造期注册期逃逸**：`this` 写入静态字段**或**交给静态集合的 mutator
  （`StaticBag.Items.Add(this)`）都会被检出。
- **属性与索引器 getter**：作为独立分析入口（含公共、私有与被调用路径）。
- **用户运算符**（`+`/`-`/`==` 等）：运算符方法体沿调用图传播。
- **`ref`/`out` 参数**：经其写入或修改其对象状态记为修改调用方状态。
- **入口稳定性（EBC2003）**：确定性入口的参数/返回值必须是稳定值
  （标量、string、只读视图、record、已声明 `ImmutableValue` 的类型）；
  **指针、数组、`List<T>` 等可变集合与未知引用被拒**——调用方可在计算期间并发修改它们。
  这是设计而非误报：请改用 `IReadOnlyList<T>` / `ImmutableArray<T>` 传递集合输入
  （这两族的常用成员已登记；但见下方"已知未实现"里关于覆盖面仍有限制的说明）。
  同理，`in` 参数放行（只读），`ref` 拒绝（双向别名）。
- **确定性计算修改自身状态**：receiver 字段/集合写入被检出（同一实例跨调用结果依赖历史）。
- **装箱与承载**：`object` 字段、`Span`/`Memory`、值类型内承载的可变引用（`KeyValuePair`/元组/struct）递归判定。
- **文化敏感格式化**：`$"{x:C}"`、`string.Format` 等依赖 `CurrentCulture` 的格式化被检出。
- **`fixed`/指针**：按文档 unsafe 策略报 Unknown。

**已知未实现（首版范围外，不是缺陷；此处如实列出以免误以为已检查）**：

- **未声明不可变的用户类型**：若某类既未声明 `ImmutableValue`，也未被显式认可，
  则按"可能可变"处理（保守）。这符合"声明不等于证明"：**要让工具认可，
  请给值类型声明 `ImmutableValue` 契约**，而不是期待工具猜测。
- **经中间容器间接流转的别名**：直接返回/存储、局部别名链（含分支重赋值）已覆盖；
  多跳容器中转可能未完整覆盖。
- **迭代器（`yield`）/ `async`**：方法体按普通同步方法处理，状态机执行语义未建模。
- **数组/可变集合作为确定性入口**被 EBC2003 拒绝（设计；迁移到 `IReadOnlyList<T>`）。
- **公共方法/索引器的暴露检查**：方法返回内部可变字段、可写 `ref` 返回索引器已检出；
  经多层容器间接流转的暴露可能未完整覆盖。
- 用户摘要配置（`effectledger.contracts.json`）未接线；strict 不依赖它。
- **BCL 目录仍是"已登记才放行"的白名单**：未登记成员落 `ExternalSummaryMissing`。
  已覆盖常见集合/字符串/数值/LINQ 纯子集与 InvariantCulture 重载，但**不可能穷尽**；
  这也是当前最主要的 Unknown 来源。遇此情况请提 issue 或改用已覆盖的等价 API。
- 跨程序集调用（第三方库）无摘要 ⇒ `ExternalSummaryMissing`（设计：不猜测外部行为）。
- 源生成器：工具侧以 fail-closed 处理（见上），不声称已完整覆盖生成源。

上述未实现项的共同方向是**保守**：要么报 Unknown 而 strict 失败，要么已在此显式声明；
不应据此认为相应形状已被检查。

## 设计约束（为什么这样做）

- **声明不等于证明。** 实现 role 接口只是主张；检查必须落到实现与依赖。
- **不用方法名字符串匹配。** 判定基于语义符号与 `IOperation`。
- **不靠 IDE 诊断做判定。** 严格入口消费原始结论，不受诊断抑制影响。
- **允许自然业务代码。** 局部变量、循环、临时集合均合法，不要求改写为函数式或固定 pipeline。

## 关闭 opt-in

移除类型上的 `IConstrained<TProfile>` 声明即可；未声明角色的类型不产生任何行为约束诊断。

## 验证命令

```pwsh
dotnet build EffectLedger.slnx -c Release -warnaserror
dotnet test tests/EffectLedger.Contracts.Tests/EffectLedger.Contracts.Tests.csproj -c Release
dotnet run --project src/EffectLedger.Contracts.Tool -c Release -- check samples/BehaviorContracts/BehaviorContracts.csproj
```
