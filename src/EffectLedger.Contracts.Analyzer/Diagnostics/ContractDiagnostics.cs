// ContractDiagnostics.cs — P7 诊断定义（独立前缀 EBC，不与现有 EAA 冲突）。
// 诊断携带语义维度，不是单纯"warning"。位置 + 解释链由 DiagnosticReporter 补充。

using Microsoft.CodeAnalysis;

namespace EffectLedger.Contracts.Analyzer.Diagnostics;

public static class ContractDiagnostics
{
    private const string Category = "EffectLedger.Contracts";

    public static readonly DiagnosticDescriptor InvalidProfile = new(
        id: "EBC0001",
        title: "无效或冲突的行为角色声明",
        messageFormat: "类型 '{0}' 的角色声明无法解析：{1}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC0001：要么类型未实现 IConstrained<T>，要么 T 不是受支持的内建角色，要么在同一类型上声明了多个角色.");

    public static readonly DiagnosticDescriptor ImmutableMutatingWrite = new(
        id: "EBC1001",
        title: "不可变值发生可观察修改",
        messageFormat: "类型 '{0}' 声明为 ImmutableValue，但 {1} 违反了构造后稳定性：{2}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC1001：受约束类型的字段 / 可达对象 / 集合元素在构造后被修改（深层不可变违反）.");

    public static readonly DiagnosticDescriptor ImmutableAliasEscape = new(
        id: "EBC1002",
        title: "内部可变别名泄露或输入别名保留",
        messageFormat: "类型 '{0}' 声明为 ImmutableValue，但 {1} 暴露了可变别名：{2}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC1002：返回/存入内部可变对象，或保留输入的可变别名（readonly List / IReadOnlyList 包装 / 浅复制可变元素均不算冻结）.");

    public static readonly DiagnosticDescriptor ImmutableCtorEscape = new(
        id: "EBC1003",
        title: "构造期 this 逃逸",
        messageFormat: "类型 '{0}' 声明为 ImmutableValue，但在构造完成前 this 已逃逸：{1}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC1003：构造函数/初始化器把 this 注册回调、写入静态位置或传给未知方法（构造期逃逸）.");

    public static readonly DiagnosticDescriptor DeterministicHiddenInput = new(
        id: "EBC2001",
        title: "确定性计算读取隐藏变化输入",
        messageFormat: "类型 '{0}' 声明为 DeterministicComputation，但 {1} 读取了隐藏输入：{2}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC2001：计算依赖时钟/随机/环境/文化/共享可配置状态等未声明变化来源（含经由 helper 的间接读取）.");

    public static readonly DiagnosticDescriptor DeterministicExternalWrite = new(
        id: "EBC2002",
        title: "确定性计算产生外部可观察修改",
        messageFormat: "类型 '{0}' 声明为 DeterministicComputation，但 {1} 产生了外部写入/IO：{2}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC2002：计算修改输入/receiver/外部状态或执行 IO（日志/文件/网络/进程环境）.允许修改不逃逸的局部对象.");

    public static readonly DiagnosticDescriptor DeterministicEntryCondition = new(
        id: "EBC2003",
        title: "确定性入口含未满足的输入/输出稳定性条件",
        messageFormat: "类型 '{0}' 的入口 '{1}' 不满足确定性输入/输出条件：{2}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC2003：公共方法参数/返回值/receiver 配置不是已知稳定值（未知可变输入或外部可变引用）.");

    public static readonly DiagnosticDescriptor UnknownDependency = new(
        id: "EBC9001",
        title: "未知依赖或不支持的语言操作",
        messageFormat: "类型 '{0}' 的约束无法完全验证：{1}",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EBC9001：外部依赖无摘要 / 开放动态派发 / 反射 / dynamic / unsafe / 预算耗尽.Unknown 不能当作通过（strict 模式失败）.",
        customTags: Microsoft.CodeAnalysis.WellKnownDiagnosticTags.CompilationEnd);

}
