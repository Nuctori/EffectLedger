// Deviation.cs — PDR §9.1 实现：开发期 Deviation 公式（分母 ε=1 防除零、⊤ 整体跳过）。LANDING_PLAN §3.3：L1 派生度量。
using System.Collections.Generic;

namespace Cosmos.EffectAlgebra;

/// <summary>
/// §9.1 开发模式验证 — Deviation 公式（编译期/开发期校准）。
/// 类型字段即边界：⊤ 不崩溃靠 <see cref="DeviationVal"/>/<see cref="NatStar"/> 类型强制，不靠运行时 if 漏判（MA-002）。
/// 资源对齐靠 <see cref="ResourceId.Normalize"/>（§3.1.4a）。
/// </summary>
public static class SignatureDeviation
{
    /// <summary>
    /// §9.1 — CalculateDeviation(expected, actual)。
    /// Deviation = Σᵢ |actualᵢ_mid − expectedᵢ_mid| / max(expectedᵢ_range, ε)
    ///   expectedᵢ_mid = (lo + hi)/2，expectedᵢ_range = hi − lo（均须有限；IsTop ⇒ 该项 ⊤）
    ///   ε = 1（分母下界，单值区间 [s,s] 时 range=0 ⇒ 用 ε 避免除零，iter33/iter50 收口 #61）
    ///   任一端为 ⊤（上界未知，§3.1.5a）⇒ 该项 Deviation 计为 ⊤ ⇒ 整体 ⊤（不可校准，跳过；不 NaN 不 ∞，MA-002 / ST-03）
    ///   返回 ⊤ 时调用方视为「需人工界定」，不触发普通 0.2f 报警（避免掩盖，§8.3.2/§9.1）
    /// scope 取 <see cref="ScopeId.Global"/>（§3.1.3b 最大元），仅含 occupy 桶净效应（net，§3.3.1）——create/move 与 release 抵消后的占用对账；read/write 不进 net（§3.3.1 量纲隔离），故本 Deviation 只比对占用净效应，非全量 Claim。这与 §9.1 开发期占用对账一致。
    /// </summary>
    public static DeviationVal Calculate(Signature expected, Signature actual)
    {
        var exp = NetTable.Compute(expected, new ScopeId.Global());
        var act = NetTable.Compute(actual, new ScopeId.Global());

        // 两签名归一化资源键并集（§3.1.4a 对齐）
        var keys = new HashSet<ResourceId>(exp.Resources);
        foreach (var r in act.Resources) keys.Add(r);

        bool anyTop = false;
        double sum = 0.0;
        foreach (var r in keys)
        {
            var e = exp.Get(r);
            var a = act.Get(r);
            // 任一端为 ⊤ ⇒ 该项 ⊤ ⇒ 整体 ⊤（§3.1.5a / ST-03）
            if (!e.TryMid(out var eMid, out var eRange) || !a.TryMid(out var aMid, out _))
            {
                anyTop = true;
                break;
            }
            // 分母下界 ε=1（iter33 收口）
            var denom = System.Math.Max(eRange, 1.0);
            sum += System.Math.Abs(aMid - eMid) / denom;
        }

        return anyTop ? DeviationVal.Top : DeviationVal.Of(sum);
    }

    /// <summary>§9.1/§3.1.5c — 复用 DeviationVal.ExceedsThreshold：先判 ⊤ 再比数值；IsTop ⇒ false（不报警）。</summary>
    public static bool ExceedsThreshold(DeviationVal d, double threshold) => d.ExceedsThreshold(threshold);
}
