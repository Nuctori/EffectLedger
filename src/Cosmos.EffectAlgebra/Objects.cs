// Objects.cs — PDR §3.1.1/§3.1.2/§3.1.3b/§3.1.4a/§3.1.4b 实现：Claim 五元组、ResourceId 单点真相、ScopeId 偏序、Signature 三桶量纲隔离。LANDING_PLAN §3.1：L1 纯代数核心。
using System.Collections.Generic;
using System.Collections.Immutable;

#if ANALYZER_SHARED
namespace Cosmos.EffectAlgebra.Analyzer.Shared;
#elif GENERATOR_SHARED
namespace Cosmos.EffectAlgebra.Generator.Shared;
#else
namespace Cosmos.EffectAlgebra;
#endif

// L1 纯代数核心：Godot 类型（Rid/StringName）以内部原语别名替代，保持零 Godot 依赖。
// 映射层（§7）负责在 Godot 实际类型与这些原语间转换（见 LANDING_PLAN §3.11）。

/// <summary>Godot RID 的内部原语别名（§7 映射层负责与 Godot.Rid 转换）。</summary>
public readonly record struct Rid(string Value);

/// <summary>Godot StringName 的内部原语别名（§7 映射层负责与 Godot.StringName 转换）。</summary>
public readonly record struct StringName(string Value);

/// <summary>
/// §3.1.2 + §3.1.2b — ResourceId 判别联合。
/// 结构相等（record）直接给出「构造子标签 + 字段逐位相等」(§3.1.4a(4))，类型保证标签完整性。
/// 字段类型即数学边界；跨字段归一由 <see cref="ResourceId.Normalize"/> 承载（类型给不了，见注释）。
/// </summary>
public abstract record ResourceId
{
    // ── JSON 契约面（§4 六资源，public）：Memory / Gpu / CommandBuffer / SignalBus / Occupancy / Custom ──
    public sealed record Memory(ulong Uid) : ResourceId;           // §7 裸 'memory' ⇒ Memory(uid="mem")
    public sealed record Gpu(Rid BufferId) : ResourceId; // §3.1.2 GPU 资源
    public sealed record Occupancy(string Channel) : ResourceId;   // §7 audio_channel / animation_state
    public sealed record Custom(string Name) : ResourceId; // §3.1.2 自定义资源
    public sealed record CommandBuffer(string Channel) : ResourceId;  // §3.1.2 命令缓冲资源（裸 command_buffer ⇒ §7）
    public sealed record SignalBus(StringName Name) : ResourceId;     // §3.1.2 信号总线（统一 signal_bus / "signal_"+s，§3.1.4a）

    // ── C# 超集本体（P1-B4b 转 internal）：非 JSON 契约面，§7 白名单层（同程序集）与
    //    InternalsVisibleTo 授权的仓库测试/样例仍可用；外部消费者不可见（诚实边界 #18 的
    //    「C# 超集可审计但 ToJson 抛」分叉随之消失）。 ──
    internal sealed record Tree(NodePathOrUnknown Path) : ResourceId; // §3.1.2 场景树资源
    internal sealed record Self(string Component) : ResourceId; // §3.1.2 自身资源
    internal sealed record Physics(Rid BodyId) : ResourceId; // §3.1.2 物理资源
    internal sealed record Disk(string Path) : ResourceId; // §3.1.2 磁盘资源
    internal sealed record Signal(StringName Name) : ResourceId; // §3.1.2 信号资源（归一垫片：统一入 SignalBus，§3.1.4a）
    internal sealed record AudioMixer(int ChannelId) : ResourceId; // §3.1.2 音频混音资源
    internal sealed record Callback(string Id) : ResourceId;         // §7 Connect callback（常量实例保守合并，QED-A7）
    internal sealed record Network(int PeerId, string Method) : ResourceId; // §3.1.2 网络资源
    internal sealed record Input(string Action) : ResourceId;        // §7.8 input

    /// <summary>
    /// §3.1.4a — 归一化函数（非结构相等）。Two Claims 相等 ⇔ 二者 Resource 经 Normalize 后相等。
    /// 映射表（与 §7 白名单裸名一一对应）：
    ///   signal_bus / "signal_"+s / Self("signal_"+s) ⇒ SignalBus(s)
    ///   gpu / command_buffer ⇒ CommandBuffer("gpu")
    ///   memory⇒Memory("mem") disk⇒Disk(p) physics⇒Physics(b) audio_mixer⇒AudioMixer(c)
    ///   audio_channel⇒Occupancy("audio") animation_state⇒Occupancy("animation") callback⇒Callback("cb")
    ///   network⇒Network(...) input⇒Input(a) self⇒Self(c) tree⇒Tree(p)
    /// Unknown 处理（§3.1.4a）：resource 为 Unknown 当且仅当静态不可判定；Unknown ≢ 已知，Unknown=Unknown（由 Tree(NodePathOrUnknown.Unknown) 结构相等保证）。
    /// </summary>
    public static ResourceId Normalize(ResourceId r) => r switch
    {
        // §3.1.2b / ST-02：Self("signal_"+s) ≡ SignalBus(s)
        Self s when s.Component.StartsWith("signal_", StringComparison.Ordinal)
            => new SignalBus(new StringName(s.Component.Substring("signal_".Length))),
        // §3.1.2b：Signal("signal_"+s) 也归一到 SignalBus(s)（PDR §3.1.4a "signal_"+s ≡ SignalBus(s)）
        Signal sig when sig.Name.Value.StartsWith("signal_", StringComparison.Ordinal)
            => new SignalBus(new StringName(sig.Name.Value.Substring("signal_".Length))),
        // §3.1.2b：SignalBus 已是规范命名空间构造子，原样返回（不再二次剥 signal_ 前缀，
        // 否则 SignalBus("signal_signal_x") ⇒ SignalBus("x") 破坏 Normalize 幂等性，§3.1.4a）。
        SignalBus bus => bus,
        // 其余构造子已为规范形式，原样返回
        _ => r
    };
}

/// <summary>§3.1.2 Tree 路径：可为具体 NodePath 或 Unknown（静态不可判定）。【P1-B4b 转内部：仅被 internal 的 Tree 使用】</summary>
internal readonly record struct NodePathOrUnknown
{
    /// <summary>true 表示路径静态不可判定（§3.1.4a Unknown 处理）。</summary>
    public bool IsUnknown { get; }

    /// <summary>§3.1.4a — 仅当 !IsUnknown 有效（具体路径）。</summary>
    public string Path { get; }

    private NodePathOrUnknown(bool isUnknown, string path) { IsUnknown = isUnknown; Path = path; }

    /// <summary>静态不可判定路径（结构相等保证 Unknown=Unknown，§3.1.4a）。</summary>
    public static readonly NodePathOrUnknown Unknown = new(true, string.Empty);

    /// <summary>§3.1.4a — 从具体路径构造。</summary>
    public static NodePathOrUnknown Of(string path) => new(false, path);
}

/// <summary>
/// §3.1.3 + §3.1.3b — ScopeId 偏序 ⊆* = (a ⊑ b) ∨ (b == Global)。
/// IncludedIn 方法内嵌 §3.1.3b 查表；跨标签（如 Method(m) vs Scene(s), m≠s）返回 false（不可比较）。
/// </summary>
public abstract record ScopeId
{
    public sealed record Method(string Name) : ScopeId; // §3.1.3b 方法作用域
    public sealed record Type(string Name) : ScopeId; // §3.1.3b 类型作用域
    public sealed record Scene(string Name) : ScopeId; // §3.1.3b 场景作用域
    public sealed record Global : ScopeId;                 // §3.1.3b 最大元
    // 以下四个为 C# 超集本体（非 JSON 契约面：契约 4 scope=scene/method/type/global），P1-B4a 转 internal——
    // §7 白名单层（同程序集）与经 InternalsVisibleTo 授权的仓库测试/样例仍可用，外部消费者不可见。
    internal sealed record Shell : ScopeId;                  // §3.1.3b shell 作用域（ST-04 收口：shell_scope ⇒ Shell）
    internal sealed record Loop(string Id) : ScopeId; // §3.1.3b 循环作用域（循环归因经 Combination.Loop 的 loopScope 参数表达，QED-A6）
    internal sealed record Conditional(string Branch) : ScopeId; // §3.1.3b 条件作用域（L1 无 if/while 记法，QED-A8 Signature(b):=∅）
    internal sealed record Async(string Id) : ScopeId; // §3.1.3b 异步作用域

    /// <summary>§3.1.3b ⊆*：自反（同构造子同字段）、反对称、传递；Global 为最大元（含一切）。</summary>
    public bool IncludedIn(ScopeId other)
    {
        if (Equals(other)) return true;          // 自反
        if (other is Global) return true;        // Global 含一切
        // 跨标签不可比较 ⇒ false（仅同标签同字段已在 Equals 命中）
        return false;
    }
}

/// <summary>§3.1.1（Claim 的 kind ∈ {read,write,occupy} 定义）/ §3.1.4b（Signature 按 kind 分三桶量纲隔离，DO-7）。enum 保证穷举，无未定义值。</summary>
public enum Kind { Read, Write, Occupy }

/// <summary>
/// §3.2.3 — 模式。Unknown 按 Use 处理（fail-open/permissive：静默放行，§3.2.3 P4；勿标 fail-closed——R4-F8/R10-F6）。
/// </summary>
public enum Mode { Use, Create, Release, Move, Unknown }

/// <summary>
/// §3.1.1 — Claim = (kind, resource, mode, scope, size)。position-record 给结构相等。
/// 类型层强制（用户铁律：类型能约束的用类型）：五参位置记录 ⇒ 五字段构造时全必填，
///   不存在「漏字段」的 Claim（构造即合法，不靠运行时 if 漏判；位置参数不可缺省）。
///   位置式 <c>new Claim(kind, res, mode, scope, size)</c> 与 <c>with</c> 均保留全字段。
/// 不变量：集合运算（∪ / net 分组 / Deviation 对齐）须用 <see cref="Normalize"/> 后的键（§3.1.4a）。
/// resource 必须归一、size 缺省 ⇒ Default，否则同资源多 Claim 不被合并（§3.1.4a 后果）。
/// rich-hickey2 R5 V5-002锐边：record struct 的 default/├with┤ 可产出 null Resource/Scope 与 0 值——Signature.Of 在集合边界用 fail-fast（含 V3 加入的 null Resource/Scope 校验）守卫，类型边不能被梢掉。</summary>
public readonly record struct Claim(Kind Kind, ResourceId Resource, Mode Mode, ScopeId Scope, Interval? Size)
{
    /// <summary>§3.1.4a 归一化：resource 走 ResourceId.Normalize；size 缺省（null）⇒ Default([1,1])，
    /// 显式 Exact(0)=[0,0] 与缺省 null 通过可空类型区分，不再被膨胀为 [1,1]（修复零 size 误报泄漏）。
    /// Hickey R1：Read 仅允许 Use/Unknown（读不应携带 Create/Release/Move 生命周期），构造期拒绝而非静默参与 net/peak。
    /// Write/Occupy 保留 Create/Release/Move（白名单 AddChild/RemoveChild 等显式建模写时创建/释放）。</summary>
    public Claim Normalize()
    {
        if (Kind == Kind.Read && Mode != Mode.Use && Mode != Mode.Unknown)
            throw new ArgumentException($"非法 Kind×Mode：{Kind}+{Mode}（Read 仅允许 Use/Unknown，读操作不应携带 Create/Release/Move 生命周期）");
        return this with
        {
            Resource = ResourceId.Normalize(Resource),
            Size = Size ?? Interval.Default
        };
    }

    // R4-RH-12（Hickey 视角）：CompatibleWith 已删（零消费且名过实——只比 Mode 不看 Resource/Kind，
    // 按名使用会误判；完整冲突判定唯一入口是 EffectScript.Audit gate(3) 的 resource×scope×mode 分组）。
}

/// <summary>
/// §3.1.4b — Signature 按 kind 分三不相交桶（DO-7 量纲隔离）。
/// 跨桶聚合须 Kind 过滤或 L3 EAA0303（L3 诊断，§3.3.2b）。
/// 类型暴露三桶；运行时集异质使跨桶聚合无法纯类型静态护栏，故由 Analyzer 补（注释契约）。
/// </summary>
public sealed class Signature
{
    private ImmutableHashSet<Claim> _read = ImmutableHashSet<Claim>.Empty;
    private ImmutableHashSet<Claim> _write = ImmutableHashSet<Claim>.Empty;
    private ImmutableHashSet<Claim> _occupy = ImmutableHashSet<Claim>.Empty;

    public ImmutableHashSet<Claim> ReadClaims => _read; // §3.1.4b 读桶（量纲隔离）
    public ImmutableHashSet<Claim> WriteClaims => _write; // §3.1.4b 写桶（量纲隔离）
    public ImmutableHashSet<Claim> OccupyClaims => _occupy; // §3.1.4b 占用桶（量纲隔离；net 仅含此桶）

    private Signature() { }

    /// <summary>§3.2.1 — 空签名（⊔ 单位元）。</summary>
    public static readonly Signature Empty = new();

    /// <summary>§3.2.1 — 从一组 Claim 构造签名（自动按 Normalize 键去重分桶）。
    /// P0-4（hickey-x3 F4）：归一化后完全相同的重复 Claim 显式报错——集合语义会静默坍缩并发数量
    /// （两份占用算一份，Peak/net 系统性减半）；并发表达必须走 <see cref="Combination.Loop"/> 的 LoopCount（ω）。</summary>
    public static Signature Of(params Claim[] claims)
    {
        var s = new Signature();
        var seen = new HashSet<Claim>();
        foreach (var c in claims)
        {
            var n = c.Normalize();
            if (!seen.Add(n))
                throw new ArgumentException(
                    $"重复 Claim({n.Kind},{n.Resource},{n.Mode},{n.Scope})：Signature 是集合，并发副本会被静默去重导致峰值/净占用低估。并发表达须用 Combination.Loop(footprint, LoopCount.Of(n), scope)（P0-4）");
            s = s.Add(n);
        }
        return s;
    }

    private Signature Add(Claim c)
    {
        // rich-hickey2 R3 V3-005：Signature 边界校验——record struct 的 with/default 后门可产出 null Resource/Scope
        // 与未定义 Kind，非法状态在进入集合前拦截（fail-fast，与重复-Claim 守卫同型）。
        if (c.Resource is null) throw new ArgumentException($"Claim.Resource 不可为 null（with/default 后门？）：{c.Kind} {c.Mode}", nameof(c));
        if (c.Scope is null) throw new ArgumentException($"Claim.Scope 不可为 null（with/default 后门？）：{c.Kind} {c.Resource}", nameof(c));
        var n = c.Normalize();
        var s = new Signature { _read = _read, _write = _write, _occupy = _occupy };
        switch (n.Kind)
        {
            case Kind.Read: s._read = s._read.Add(n); break;
            case Kind.Write: s._write = s._write.Add(n); break;
            case Kind.Occupy: s._occupy = s._occupy.Add(n); break;
            default: throw new ArgumentOutOfRangeException(nameof(c), $"未知 Kind: {n.Kind}");
        }
        return s;
    }

    /// <summary>§3.2.1/§3.2.2 ∪：并集，按 Normalize 键去重（幂等由结构相等保证，§3.1.4a）。</summary>
    public static Signature Union(Signature a, Signature b)
    {
        var s = new Signature { _read = a._read, _write = a._write, _occupy = a._occupy };
        foreach (var c in b._read) s = s.Add(c);
        foreach (var c in b._write) s = s.Add(c);
        foreach (var c in b._occupy) s = s.Add(c);
        return s;
    }

    /// <summary>§3.2.4 ⊔：条件分支合并（join-semilattice 幂等/交换/结合；**L1 警告** rich-hickey2 R5 V5-001：与 Union 等价于"同键 size 求并区间"(merge_I)——条件分支[10,10]⊔[50,50]⇒[10,50] Peak=max而非求和，不承载"合并时序"，与 Signature.Union 仅在同键合并上差异）。</summary>
    public static Signature Join(Signature a, Signature b)
    {
        var merged = new Dictionary<(Kind, ResourceId, Mode, ScopeId), Interval>();
        void Accumulate(Claim c)
        {
            var n = c.Normalize();
            var key = (n.Kind, n.Resource, n.Mode, n.Scope);
            var size = n.Size ?? Interval.Default;
            merged[key] = merged.TryGetValue(key, out var cur) ? cur.Merge(size) : size;
        }
        foreach (var c in a.AllClaims()) Accumulate(c);
        foreach (var c in b.AllClaims()) Accumulate(c);
        var claims = new List<Claim>(merged.Count);
        foreach (var kv in merged)
            claims.Add(new Claim(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4, kv.Value));
        return Of(claims.ToArray());
    }

    // R4-RH-03（Hickey 视角）：UnionWidening 与 Join 完全等价的别名已删（零消费；揭示语义见 Join 的 XML doc）。

    /// <summary>§3.3.1 net(S,scope)：按资源分组，带符号 size 求和（create/release 抵消），仅含 ⊆* 过滤的 Claim。</summary>
    public NetTable Net(ScopeId scope) => NetTable.Compute(this, scope);
    // §3.1.4a(R4 P1) — Signature 看似值实则为引用相等（class 无结构相等），是 Hickey 式 footgun：
    // 两个结构相同的签名不会 == / 不会哈希相等。补结构相等使「值」语义与外观一致（不改任何代数语义）。
    public bool Equals(Signature? other) =>
        other is not null && _read.SetEquals(other._read) && _write.SetEquals(other._write) && _occupy.SetEquals(other._occupy);

    public override bool Equals(object? obj) => Equals(obj as Signature);

    public override int GetHashCode()
    {
        unchecked
        {
            // 顺序无关折叠：XOR 累加（ImmutableHashSet 迭代序不保证），桶内顺序不影响哈希
            var h = 0;
            foreach (var c in _read) h ^= c.GetHashCode();
            foreach (var c in _write) h ^= c.GetHashCode();
            foreach (var c in _occupy) h ^= c.GetHashCode();
            // 区分空桶与跨桶移动（Claim 含 Kind 已区分，额外混入桶计数防退化）
            h ^= _read.Count * 17;
            h ^= _write.Count * 31;
            h ^= _occupy.Count * 53;
            return h;
        }
    }

    public static bool operator ==(Signature? a, Signature? b) => Equals(a, b);
    public static bool operator !=(Signature? a, Signature? b) => !Equals(a, b);
}
