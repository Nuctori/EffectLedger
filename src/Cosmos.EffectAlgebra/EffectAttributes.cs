// EffectAttributes.cs — PDR §8.3.1/§8.3.2 实现：[EffectOverride]（禁覆盖 kind、reason 强制）/[AcceptDeviation]（ε∈[0,0.5] 构造子强制）。LANDING_PLAN §3：L1 属性层（零 Godot）。
// §8.3 [EffectOverride] / [AcceptDeviation] 校验规则（收口 iter50 #62）。
// 这两个属性是"逃逸通道"的唯一批准形式：类型层即强制其边界，避免无差别压制。
// 命名空间 Cosmos.EffectAlgebra（L1 零 Godot 依赖；此处仅为属性定义，映射到 L2/L3 消费）。

namespace Cosmos.EffectAlgebra;

/// <summary>
/// §8.3.1 — [EffectOverride]：仅可覆盖单条 Claim 的 mode / size / scope 三类属性。
/// 不变式（类型强制 + 构造子强制）：
///   (1) reason 非空：构造子强制，否则抛（CI 需人工 approve，reason 须引用证据，不接受"信任我"）——注释约定，不含审批逻辑。
///   (2) 禁止覆盖 kind：本类型**不提供** OverrideKind 属性 ⇒ 类型层即禁止 read/write/occupy 互转（§8.3.1 显式禁覆盖 kind）。
///   (3) 禁止压制 DO-9 根因：override 只能给 mode/size/scope 的保守修正，必须配真实 release 路径证明；本属性不提供"豁免 DO-9"开关，DO-9 仍报警（注释不变式）。
///   (4) 禁止静默豁免 DO-7 量纲混算：跨 kind 仍需 weight 定义（§3.3.2），本属性不提供跨 kind 豁免开关（注释不变式）。
/// 作用对象：标注于单条 Claim 或 resource 上（L2/L3 解析 target）。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false)]
public sealed class EffectOverrideAttribute : Attribute // §8.3.1
{
    /// <summary>§8.3.1 审查：非空 reason，须引用 API 文档/实测证据，CI 人工 approve。构造子强制非空。</summary>
    public string Reason { get; }

    /// <summary>§8.3.1 可选 scope 覆盖（覆盖单条 Claim 的 scope 属性）。默认 null = 沿用原 scope。</summary>
    public ScopeId? Scope { get; set; }

    /// <summary>
    /// §8.3.1 覆盖 mode：仅 Use/Create/Release/Move（不含 Unknown）。
    /// 类型边界：用 Mode? 且代码注释明言 Unknown 不被允许（Unknown 由默认规则处理，不得经 override 指定）。
    /// 注：无 OverrideKind 属性 ⇒ kind（read/write/occupy）禁止覆盖（§8.3.1 类型强制）。
    /// </summary>
    public Mode? OverrideMode { get; set; }

    /// <summary>
    /// §8.3.1 覆盖 size 数值（单值；区间形式由 L2 展开为 [v,v]，见 §3.1.5）。
    /// 类型边界：double?；负值非法（size 属 ℕ*，§3.1.5a），调用方须保证 ≥1，本层不重复校验（L1 仅承载属性数据）。
    /// </summary>
    public double? OverrideSize { get; set; }

    /// <summary>
    /// §8.3.1 构造子：reason 非空强制。空/空白 reason ⇒ 抛（fail-fast，避免无证逃逸通道）。
    /// </summary>
    public EffectOverrideAttribute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("§8.3.1: [EffectOverride] reason 必须非空且引用证据（CI 人工 approve，不接受\"信任我\"）", nameof(reason));
        Reason = reason;
    }
}

/// <summary>
/// §8.3.2 — [AcceptDeviation(ε)]：epsilon ∈ [0.0, 0.5]，超出 ⇒ 编译/构造错误（上界约束）。
/// 不变式：
///   (1) epsilon 上界靠构造子抛异常强制（类型边界：double 无上界，故构造子拦 [0,0.5]）。
///   (2) 仅放宽运行时 Deviation > 报警阈值（§9.1 基础 0.2f）的局部报警；不豁免编译期 DO 报警（注释不变式）。
///   (3) 作用域：标注对象所在 scope（§3.1.3b），不跨 scope 传播（注释不变式）。
///   (4) 若 epsilon < 阈值（0.2）则无意义 ⇒ 构造子发警告（此处仅注释，编译警告由 L2/L3 触发，见 §8.3.2）。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AcceptDeviationAttribute : Attribute // §8.3.2
{
    /// <summary>§8.3.2 放宽幅度，ε ∈ [0.0, 0.5]（构造子已强制）。</summary>
    public double Epsilon { get; }

    /// <summary>
    /// §8.3.2 构造子：epsilon 越界 ⇒ 抛（上界约束，编译期/构造期即拦）。
    /// 不变式：ε 仅在 [0.0, 0.5] 合法；越界视为错误而非静默截断（fail-closed）。
    /// </summary>
    public AcceptDeviationAttribute(double epsilon)
    {
        if (epsilon < 0.0 || epsilon > 0.5)
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "§8.3.2: epsilon ∈ [0.0, 0.5]（超出上界 ⇒ 编译错误）");
        Epsilon = epsilon;
    }
}
