// AbstractDomain.cs — P2.1 抽象域定义。
// 设计要点（见 docs/behavior-contracts-phases.md P2.1）：
//   - 效果/别名/未知是**独立维度**，不能互相覆盖：同一方法可同时有已知违规与未知。
//   - Unknown 是"缺少依据"，不是"无副作用"；Join 必须单调保留未知（unknown 不能被已知清除）。
//   - 现场（Fresh）对象用于「允许修改不逃逸的局部对象」（不可妥协要求 5）。
//
// 本期刻意保持小而显式：任何未建模的形状走 Unsupported/Unknown，而不是默认 empty。

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace EffectLedger.Contracts.Analyzer.Analysis;

/// <summary>隐藏输入类别：这些都会让"相同显式输入得到不同结果"。</summary>
[Flags]
public enum HiddenInputKind
{
    None = 0,
    /// <summary>时钟：DateTime.Now/UtcNow/Today、Stopwatch、Environment.TickCount 等。</summary>
    Time = 1 << 0,
    /// <summary>随机：Random、Guid.NewGuid、RNGCryptoServiceProvider。</summary>
    Random = 1 << 1,
    /// <summary>环境：环境变量、当前目录、机器名、进程/线程环境。</summary>
    Ambient = 1 << 2,
    /// <summary>文化/区域：默认文化、当前区域、默认内置比较器（跨进程不确定）。</summary>
    Culture = 1 << 3,
    /// <summary>共享可变配置：静态可写属性/字段作为读取源。</summary>
    GlobalState = 1 << 4,
}

/// <summary>外部可观察写入 / IO 类别。</summary>
[Flags]
public enum ExternalEffectKind
{
    None = 0,
    /// <summary>控制台 / 日志 / Trace 等输出通道。</summary>
    Console = 1 << 0,
    /// <summary>文件系统。</summary>
    File = 1 << 1,
    /// <summary>网络 / HTTP / Socket。</summary>
    Network = 1 << 2,
    /// <summary>写入静态可写状态（进程级共享）。</summary>
    StaticWrite = 1 << 3,
    /// <summary>写入未知接收者（目标不可解析），保守视为外部修改。</summary>
    UnknownTargetWrite = 1 << 4,
    /// <summary>读/写环境或进程级配置（Environment.SetEnvironmentVariable 等）。</summary>
    Process = 1 << 5,
    /// <summary>其它未分类 IO（未映射 BCL/第三方但可判定为 IO 的调用）。</summary>
    Other = 1 << 6,
}

/// <summary>未知原因：必须可解释、可回归，不能笼统"未知"。</summary>
public enum UnknownReason
{
    None = 0,
    /// <summary>依赖的方法源码不在当前 compilation（无摘要）。</summary>
    ExternalSummaryMissing,
    /// <summary>开放虚派发 / 接口调用，目标集合无法闭合并验证。</summary>
    OpenDispatch,
    /// <summary>反射 / dynamic / PInvoke / unsafe / 函数指针等不支持的语言操作。</summary>
    UnsupportedOperation,
    /// <summary>分析预算（可达方法数 / 迭代 / 深度）耗尽。</summary>
    BudgetExceeded,
}

/// <summary>写入目标的抽象分类（P2.3）。Fresh 是"允许被改"的关键：分配点唯一且未逃逸。</summary>
public enum WriteTargetKind
{
    /// <summary>本方法新分配且未逃逸的对象（允许修改）。</summary>
    Fresh,
    /// <summary>本方法的参数 / 输入（禁止修改，R-BOUNDARY-02）。</summary>
    Parameter,
    /// <summary>受约束类型的 receiver 自身状态（构造后禁止修改）。</summary>
    Receiver,
    /// <summary>静态可写状态（进程级共享）。</summary>
    Static,
    /// <summary>未知来源（保守：按违规或未知处理，绝不按 Fresh 放行）。</summary>
    Unknown,
}

/// <summary>摘要中的一个写入点。位置用于诊断定位（文件行/列）。</summary>
public sealed record WriteSite(WriteTargetKind Target, string Description, string? LocationKey);

/// <summary>摘要中的一个隐藏输入读取点。</summary>
public sealed record HiddenInputSite(HiddenInputKind Kind, string Description, string? LocationKey);

/// <summary>摘要中的一个外部效果点。</summary>
public sealed record ExternalEffectSite(ExternalEffectKind Kind, string Description, string? LocationKey);

/// <summary>未知点：原因 + 可读说明 + 位置（用于调用链）。</summary>
public sealed record UnknownSite(UnknownReason Reason, string Description, string? LocationKey);

/// <summary>
/// 方法行为摘要（P2.1）。所有字段都是集合语义、Join = 并集；
/// <see cref="IsUnresolved"/> 表示"本摘要本身建立在未知之上"（外部依赖/开放派发/预算）。
/// </summary>
public sealed class MethodSummary
{
    public static readonly MethodSummary Empty = new();

    public ImmutableHashSet<HiddenInputSite> HiddenInputs { get; init; } = ImmutableHashSet<HiddenInputSite>.Empty;
    public ImmutableHashSet<ExternalEffectSite> ExternalEffects { get; init; } = ImmutableHashSet<ExternalEffectSite>.Empty;
    public ImmutableHashSet<WriteSite> Writes { get; init; } = ImmutableHashSet<WriteSite>.Empty;
    public ImmutableHashSet<UnknownSite> Unknowns { get; init; } = ImmutableHashSet<UnknownSite>.Empty;

    /// <summary>返回值是否可能是 receiver / 参数别名（不可变检查的逃逸判据之一）。</summary>
    public bool ReturnsInputAlias { get; init; }

    /// <summary>是否有参数被存入字段/静态位置或交给未知接收者（参数逃逸）。</summary>
    public bool EscapesParameters { get; init; }

    /// <summary>接收者是否被存入静态位置或交给未知接收者（this 逃逸，含构造期）。</summary>
    public bool EscapesReceiver { get; init; }

    /// <summary>是否注册/保存了回调（回调可能之后执行，效果不能在创建点丢弃）。</summary>
    public bool StoresCallback { get; init; }

    /// <summary>是否同步执行了传入的回调（委托创建 != 执行，见不可妥协要求 4）。</summary>
    public bool ExecutesCallback { get; init; }

    /// <summary>是否进入构造/初始化阶段语义（用于 ImmutableValue 的构造期检查）。</summary>
    public bool IsConstructorLike { get; init; }

    /// <summary>
    /// 需要按符号把行为并入本摘要的成员（属性 getter、隐式 Dispose 等）。
    /// 这些成员不出现在语法调用点，若不显式登记就会成为静默绕过路径（审查 F-01/F-05）。
    /// </summary>
    public ImmutableHashSet<IMethodSymbol> RequiresPropagation { get; init; }
        = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default);

    /// <summary>是否有任何"不允许"的效应（不含未知）。</summary>
    public static MethodSummary Merge(MethodSummary a, MethodSummary b) => new()
    {
        HiddenInputs = a.HiddenInputs.Union(b.HiddenInputs),
        ExternalEffects = a.ExternalEffects.Union(b.ExternalEffects),
        Writes = a.Writes.Union(b.Writes),
        Unknowns = a.Unknowns.Union(b.Unknowns),
        ReturnsInputAlias = a.ReturnsInputAlias || b.ReturnsInputAlias,
        EscapesParameters = a.EscapesParameters || b.EscapesParameters,
        EscapesReceiver = a.EscapesReceiver || b.EscapesReceiver,
        StoresCallback = a.StoresCallback || b.StoresCallback,
        ExecutesCallback = a.ExecutesCallback || b.ExecutesCallback,
        IsConstructorLike = a.IsConstructorLike || b.IsConstructorLike,
        RequiresPropagation = a.RequiresPropagation.Union(b.RequiresPropagation),
    };

    /// <summary>把另一份摘要的效应并入本摘要（调用点传播用；接收者上下文由调用方决定）。</summary>
    public MethodSummary With(MethodSummary other) => Merge(this, other);

    public MethodSummary WithHidden(HiddenInputKind kind, string desc, string? loc)
        => With(new MethodSummary { HiddenInputs = ImmutableHashSet.Create(new HiddenInputSite(kind, desc, loc)) });

    public MethodSummary WithEffect(ExternalEffectKind kind, string desc, string? loc)
        => With(new MethodSummary { ExternalEffects = ImmutableHashSet.Create(new ExternalEffectSite(kind, desc, loc)) });

    public MethodSummary WithWrite(WriteTargetKind target, string desc, string? loc)
        => With(new MethodSummary { Writes = ImmutableHashSet.Create(new WriteSite(target, desc, loc)) });

    public MethodSummary WithUnknown(UnknownReason reason, string desc, string? loc)
        => With(new MethodSummary { Unknowns = ImmutableHashSet.Create(new UnknownSite(reason, desc, loc)) });

}
