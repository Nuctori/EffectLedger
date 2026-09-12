// CompatibleMatrixTests.cs — PDR §3.2.3 兼容矩阵全 25 组合逐字穷举锁。
// 期望表逐字引 §3.2.3（CONFLICT/良性配对/use 放行/Unknown 视为 Use），断言 Compatible.IsCompatible 零漂移。
// LANDING_PLAN §3.2：类型编码全函数 + 对称；注释承载 P4/P3 语义。

using System.Collections.Generic;
using EffectLedger;
using Xunit;

namespace EffectLedger.Tests;

/// <summary>
/// §3.2.3 兼容矩阵（mode 对 a,b）：
///   - CONFLICT（false）：(Create,Create)/(Move,Move)/(Release,Release) —— 同类自冲突。
///   - 良性配对（true）：(Create,Release)/(Release,Create)/(Create,Move)/(Move,Create)/(Release,Move)/(Move,Release) —— P3 生命周期互补。
///   - use 放行（true）：(Use,Use)/(Use,Create)/(Use,Release)/(Use,Move)/(Create,Use)/(Release,Use)/(Move,Use) —— use 最弱共享权限。
///   - Unknown 视为 Use（true）：(Unknown,*) 与 (*,Unknown) 同 (Use,*) —— P4，运行期具体化前 Unknown 按 Use 处理。
/// 全 5×5 = 25 组合。
/// </summary>
public class CompatibleMatrixTests
{
    // §3.2.3 兼容矩阵：false = CONFLICT，true = 兼容
    public static readonly (Mode a, Mode b, bool expected)[] Matrix =
    {
        // CONFLICT（同类自冲突）
        (Mode.Create,  Mode.Create,  false),
        (Mode.Move,    Mode.Move,    false),
        (Mode.Release, Mode.Release, false),
        // 良性配对（P3 生命周期互补）
        (Mode.Create,  Mode.Release, true),
        (Mode.Release, Mode.Create,  true),
        (Mode.Create,  Mode.Move,    true),
        (Mode.Move,    Mode.Create,  true),
        (Mode.Release, Mode.Move,    true),
        (Mode.Move,    Mode.Release, true),
        // use 放行（Use 与任意 mode 兼容）
        (Mode.Use,     Mode.Use,     true),
        (Mode.Use,     Mode.Create,  true),
        (Mode.Use,     Mode.Release, true),
        (Mode.Use,     Mode.Move,    true),
        (Mode.Create,  Mode.Use,     true),
        (Mode.Release, Mode.Use,     true),
        (Mode.Move,    Mode.Use,     true),
        // Unknown 视为 Use（P4）：(Unknown,*) 与 (*,Unknown) 同 (Use,*)
        (Mode.Unknown, Mode.Use,     true),
        (Mode.Unknown, Mode.Create,  true),
        (Mode.Unknown, Mode.Release, true),
        (Mode.Unknown, Mode.Move,    true),
        (Mode.Use,     Mode.Unknown, true),
        (Mode.Create,  Mode.Unknown, true),
        (Mode.Release, Mode.Unknown, true),
        (Mode.Move,    Mode.Unknown, true),
        (Mode.Unknown, Mode.Unknown, true),
    };

    public static IEnumerable<object[]> MatrixCases
    {
        get
        {
            foreach (var (a, b, expected) in Matrix)
                yield return new object[] { a, b, expected };
        }
    }

    /// <summary>§3.2.3 — 全 25 组合逐字断言与期望表一致；实现偏离矩阵必红。</summary>
    [Theory]
    [MemberData(nameof(MatrixCases))]
    public void Compatible_Matrix_AllPairs(Mode a, Mode b, bool expected) =>
        Assert.Equal(expected, Compatible.IsCompatible(a, b));

    /// <summary>§3.2.3 (P1) 对称：IsCompatible(a,b)==IsCompatible(b,a) 对全 25 组合；全函数不抛。</summary>
    [Theory]
    [MemberData(nameof(MatrixCases))]
    public void Compatible_Symmetric_AllPairs(Mode a, Mode b, bool _)
    {
        var ab = Compatible.IsCompatible(a, b);
        var ba = Compatible.IsCompatible(b, a);
        Assert.Equal(ab, ba);
    }

    /// <summary>§3.2.3 — 全函数：25 组合均返回 bool 且无一抛异常（穷举断言非异常路径）。</summary>
    [Fact]
    public void Compatible_TotalFunction_NoThrow()
    {
        foreach (Mode a in new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown })
        foreach (Mode b in new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown })
            Assert.IsType<bool>(Compatible.IsCompatible(a, b));
    }
}
