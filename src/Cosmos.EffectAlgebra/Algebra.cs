// Algebra.cs — PDR §3.2.1/§3.2.3/§3.3.1/§3.3.2 实现：组合、Compatible 全函数、net 净占用、Peak 峰值。LANDING_PLAN §3.2：L1 代数运算。
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra;

/// <summary>
/// §3.2.3 — Compatible：16 对全函数 + 对称。无未覆盖对（P2）。
/// CONFLICT = {(Create,Create),(Move,Move),(Release,Release)}。
/// Unknown 按 Use 处理（fail-closed 最弱兼容，P4）。
/// </summary>
public static class Compatible
{
    // §3.2.3 P4：Unknown 解析为 Use（最弱兼容，保守）
    private static Mode Resolve(Mode m) => m == Mode.Unknown ? Mode.Use : m;

    /// <summary>§3.2.3 全函数定义；(P1) 对称：IsCompatible(a,b)=IsCompatible(b,a)。</summary>
    public static bool IsCompatible(Mode a, Mode b)
    {
        var aa = Resolve(a);
        var bb = Resolve(b);
        if (aa == Mode.Use || bb == Mode.Use) return true;   // use 最弱共享权限，与任意 mode 兼容
        // §3.2.3 良性生命周期配对（P3：create+release 不再误判冲突）
        if ((aa, bb) is (Mode.Create, Mode.Release) or (Mode.Release, Mode.Create)) return true;
        if ((aa, bb) is (Mode.Create, Mode.Move) or (Mode.Move, Mode.Create)) return true;
        if ((aa, bb) is (Mode.Release, Mode.Move) or (Mode.Move, Mode.Release)) return true;
        return false; // CONFLICT 集：create+create / move+move / release+release
    }
}

/// <summary>
/// §3.3.2b — weight: Kind × Kind → ℝ ∪ {⊥}。⊥ 表示跨 kind 无定义 ⇒ 聚合须报错 KIND_MIX（L3）。
/// 类型表达为 partial 函数：同 kind ⇒ 1.0；跨 kind ⇒ double.NaN（约定为 ⊥ 编码，注释契约）。
/// </summary>
public static class Weight // §3.3.2b weight: Kind × Kind → ℝ ∪ {⊥}
{
    /// <summary>§3.3.2b — 返回 NaN 表示 ⊥（跨 kind 未定义，触发 KIND_MIX）。</summary>
    public static double Of(Kind a, Kind b) => a == b ? 1.0 : double.NaN;
}

/// <summary>
/// §3.3.1 — net(S, scope)：按资源分组，有符号 size 求和（create/release 抵消）。
/// 仅含 occupy 桶（c.kind == Occupy）的 Claim（read/write 不进 net 守恒，§3.3.1 量纲隔离）；且仅含 ⊆* 过滤的 Claim。
/// size 用 SignedInterval.Merge（§3.1.5b 同构，端点 ∈ ZStar 有符号）；同资源 create/move(+) 与 release(−) 符号相反，净区间含 0 即守恒（DO-9 不报警）。
/// </summary>
public sealed class NetTable
{
    // §3.3.1 — 有符号 net：键为归一化资源，值为 SignedInterval（ℤ* 区间，可负）。
    private readonly Dictionary<ResourceId, SignedInterval> _net = new();

    private NetTable() { }

    /// <summary>§3.3.1 — 仅含 c.kind == Occupy 且 c.Scope ⊆* scope 的 Claim；按归一化资源分组，create/move 加、release 减（有符号）。</summary>
    public static NetTable Compute(Signature sig, ScopeId scope)
    {
        var t = new NetTable();
        foreach (var c in sig.AllClaims())
        {
            if (c.Kind != Kind.Occupy) continue;      // §3.3.1 net 仅含 occupy 桶（量纲隔离）
            if (!c.Scope.IncludedIn(scope)) continue;  // §3.1.3b ⊆* 过滤
            var r = ResourceId.Normalize(c.Resource);
            // §3.3.1 有符号 size：release 取 size 的「负向」[-hi,-lo]；create/move 正号 [lo,hi]。
            var signed = c.Mode == Mode.Release ? Negate(c.Size) : ToSigned(c.Size);
            t._net[r] = t._net.ContainsKey(r) ? t._net[r].Merge(signed) : signed;
        }
        return t;
    }

    // §3.3.1 release 取 size 的「负向」[-hi,-lo]（非负 ℕ* 经 ZStar 转有符号）。
    // 任一端 IsTop ⇒ 对应 ZStar.Top（net 未知 ⇒ IsConserved fail-closed 返回 false，交人工确认）。
    private static SignedInterval Negate(Interval s)
    {
        var lo = s.Lo.IsTop ? ZStar.Top : ZStar.Of(-(long)s.Lo.Value);
        var hi = s.Hi.IsTop ? ZStar.Top : ZStar.Of(-(long)s.Hi.Value);
        return new SignedInterval(lo, hi); // [-hi, -lo]
    }

    // §3.3.1 create/move 正号：[lo, hi]（非负 ℕ* 转 ZStar）。
    private static SignedInterval ToSigned(Interval s)
    {
        var lo = s.Lo.IsTop ? ZStar.Top : ZStar.Of((long)s.Lo.Value);
        var hi = s.Hi.IsTop ? ZStar.Top : ZStar.Of((long)s.Hi.Value);
        return new SignedInterval(lo, hi);
    }

    /// <summary>§3.3.1 — 全部参与 net 的归一化资源键（供 §9.1 Deviation 资源对齐枚举）。</summary>
    public IEnumerable<ResourceId> Resources => _net.Keys;

    /// <summary>按归一化资源键取净效应有符号区间；缺省 ⇒ <see cref="SignedInterval.Zero"/>。供 §9.1 Deviation 资源对齐使用。</summary>
    public SignedInterval Get(ResourceId r) => _net.TryGetValue(ResourceId.Normalize(r), out var v) ? v : SignedInterval.Zero;

    /// <summary>§3.3.1 守恒判定：资源必须出现在净效应中且区间跨 0（lo<=0<=hi）⇒ 生命周期闭合（DO-9 不报警）。
    /// 未出现在 net 中的资源 ⇒ 无任何净效应记录 ⇒ 视为未闭合，fail-closed 返回 false（触发 DO-9 报警，不静默漏报）。
    /// 任一端 ⊤（未知上界）⇒ 视为「需人工界定」⇒ 不守恒（fail-closed，§3.3.1 DO-9）。</summary>
    public bool IsConserved(ResourceId r)
    {
        var key = ResourceId.Normalize(r);
        if (!_net.ContainsKey(key)) return false; // 无净效应记录 ⇒ 未闭合（fail-closed）
        var v = _net[key];
        if (v.Lo.IsTop || v.Hi.IsTop) return false; // ⊤ 不宣称守恒，交人工确认
        return v.ContainsZero; // 区间含 0 ⇒ 可能闭合，不报警
    }
}

/// <summary>
/// §3.3.2 / §3.2.5 — Peak：size 求和（§3.2.5 旧 cardinality 形式已废弃，以本节为准）。
/// c.mode=release 不计入（release 是释放，不贡献并发占用峰值，§3.3.2 公式 c.mode≠release）。
/// ω=⊤ ⇒ 返回 ⊤（上界标记，不发散，MA-002）。
/// </summary>
public static class Peak
{
    /// <summary>§3.3.2 — 对某 scope 下全部 claim（非 release）的 size 求和；任一 ⊤ ⇒ 整体 ⊤。</summary>
    public static NatStar Compute(Signature sig, ScopeId scope)
    {
        NatStar sum = NatStar.Of(0);
        foreach (var c in sig.AllClaims())
        {
            if (!c.Scope.IncludedIn(scope)) continue;
            if (c.Mode == Mode.Release) continue; // §3.3.2 c.mode≠release：release 不贡献峰值
            if (c.Size.Hi.IsTop) return NatStar.Top; // ω=⊤ 兜底（§3.2.5）
            sum = sum + c.Size.Hi;                    // 取 hi 作为峰值上界
        }
        return sum;
    }
}

// §3.1.4b — Signature 枚举扩展：暴露全部 Claim（三桶）。
public static class SignatureExtensions
{
    /// <summary>§3.1.4b 三桶合并枚举（顺序无关，因聚合按 Normalize 键）。</summary>
    public static IEnumerable<Claim> AllClaims(this Signature sig)
    {
        foreach (var c in sig.ReadClaims) yield return c;
        foreach (var c in sig.WriteClaims) yield return c;
        foreach (var c in sig.OccupyClaims) yield return c;
    }
}
