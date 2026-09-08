// QedMaintFuzzTests.cs — 维护模式每晚对抗模糊测试（QED 路线收官后的常驻装置，ROADMAP 终段条款）：
// 随机生成 EffectScript JSON（~85% 合法形状打到深层路径 + ~15% 畸形输入验证方言鲁棒性），
// 断言四条不变量：①合法形状必可 Parse（畸形只允许 FormatException，绝无其他异常类型）；
// ②Audit 永不抛任何异常；③Audit 确定性（同剧本两次运行违例序列一致）；④ToJson→Parse 往返闭合。
// 种子 = UTC 日期：每晚一组新用例、同日重跑完全可复现（失败即得精确反例剧本）。
// 配套门禁组件：变异门 = GateFixture；性能曲线钉 = ProdAuditR4AuditScaleTests。xUnit。
using System.Text;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class QedMaintFuzzTests
{
    static readonly string[] Kinds = { "read", "write", "occupy" };
    static readonly string[] Modes = { "use", "create", "release", "move", "unknown" };
    // (键, 值, 是否字符串引号)——memory 的值是非负整数（裸数字，契约 E7 轮已确认），
    // 其余资源值为字符串；fuzzer 语料必须同界（首轮 fuzz 即抓到本生成器的违例，方言防线实证有效）
    static readonly (string Key, string Val, bool Quoted)[] Resources =
    {
        ("gpu", "buf", true), ("commandBuffer", "gpu", true), ("memory", "1", false), ("memory", "2", false),
        ("occupancy", "audio", true), ("signalBus", "sig", true), ("custom", "c", true)
    };
    static readonly string[] ScopeTypes = { "method", "type", "global", "scene" };

    static string RandNat(Random rng, bool allowTop)
        => allowTop && rng.Next(4) == 0 ? "\"⊤\"" : rng.Next(0, 64).ToString();

    static string RandScope(Random rng)
    {
        if (rng.Next(2) == 0) return $"{{ \"scene\": \"s{rng.Next(3)}\" }}";
        var t = ScopeTypes[rng.Next(ScopeTypes.Length)];
        return t == "global" ? "{ \"type\": \"global\" }" : $"{{ \"scene\": \"s{rng.Next(3)}\", \"type\": \"{t}\" }}";
    }

    // 合法 size 生成：[a,b] (1≤a≤b) / [a,⊤] / [⊤,⊤]（[⊤,finite] 非法——契约 §3.1.5）
    static string RandSize(Random rng)
    {
        if (rng.Next(4) == 0) return $", \"size\": [{rng.Next(1, 8)}, \"⊤\"]";
        if (rng.Next(10) == 0) return $", \"size\": [\"⊤\", \"⊤\"]";
        var lo = rng.Next(1, 8);
        return $", \"size\": [{lo}, {lo + rng.Next(0, 32)}]";
    }

    // claim 省略 scope ⇒ 继承所属 event scope（R10 Top1 继承路径，fuzzer 顺带覆盖之；
    // 显式异 scope 会被 Parse 以「单一真相」拒绝——R3-L1-07，首轮 fuzz 即实证）
    static string RandClaim(Random rng)
    {
        var (rk, rv, quoted) = Resources[rng.Next(Resources.Length)];
        var kind = Kinds[rng.Next(Kinds.Length)];
        // Kind×Mode 契约约束：Read 仅允许 Use/Unknown（Claim.Normalize 铁律）
        var mode = kind == "read"
            ? (rng.Next(2) == 0 ? "use" : "unknown")
            : Modes[rng.Next(Modes.Length)];
        var resVal = quoted ? '"' + rv + '"' : rv;
        var size = RandSize(rng);
        return $"{{ \"kind\": \"{kind}\", \"resource\": {{ \"{rk}\": {resVal} }}, \"mode\": \"{mode}\"{size} }}";
    }

    static string FuzzScript(Random rng, bool malformed)
    {
        if (malformed)
        {
            // 畸形语料：截断/错型/未知键——验证方言鲁棒性（只允许 FormatException）
            // 首轮 4 类「合法形状被拒」的语料转为畸形常驻（P5.2-M5：方言拒绝路径保持每晚演练）
            return rng.Next(7) switch
            {
                0 => "{ \"events\": [ { \"lifetime\": [0",
                1 => "{ \"events\": \"not-an-array\", \"budget\": 3 }",
                2 => "{ \"events\": [ { \"lifetime\": [0, 5], \"scope\": { \"scene\": \"S\" }, \"footprint\": [ { \"kind\": \"occupy\", \"resource\": { \"gpu\": 3 }, \"mode\": \"use\" } ] } ] }",
                3 => "{ \"events\": [ { \"lifetime\": [0, 5], \"scope\": { \"scene\": \"S\" }, \"footprint\": [ { \"kind\": \"occupy\", \"resource\": { \"memory\": \"2\" }, \"mode\": \"use\" } ] } ] }",       // memory 字符串值
                4 => "{ \"events\": [ { \"lifetime\": [0, 5], \"scope\": { \"scene\": \"S\" }, \"footprint\": [ { \"kind\": \"read\", \"resource\": { \"memory\": 1 }, \"mode\": \"create\", \"scope\": { \"scene\": \"S\" } } ] } ] }",                                             // Read+Create
                5 => "{ \"events\": [ { \"lifetime\": [0, 5], \"scope\": { \"scene\": \"S\" }, \"footprint\": [ { \"kind\": \"occupy\", \"resource\": { \"memory\": 1 }, \"mode\": \"use\", \"scope\": { \"scene\": \"S\" }, \"size\": [7, 5] } ] } ] }",                                     // size lo>hi
                _ => "{ \"events\": [ { \"lifetime\": [0, 5], \"scope\": { \"scene\": \"S\" }, \"footprint\": [ { \"kind\": \"occupy\", \"resource\": { \"memory\": 1 }, \"mode\": \"use\", \"scope\": { \"scene\": \"T\" } } ] } ] }",                                                 // claim scope ≠ event scope
            };
        }
        var sb = new StringBuilder();
        sb.Append("{ \"events\": [ ");
        int n = rng.Next(1, 7);
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(", ");
            var lo = rng.Next(0, 20);
            var hi = lo + rng.Next(0, 20);
            var hiStr = rng.Next(6) == 0 ? "\"⊤\"" : hi.ToString();
            var loop = rng.Next(5) == 0 ? ", \"loop\": \"⊤\"" : (rng.Next(3) == 0 ? $", \"loop\": {rng.Next(1, 5)}" : "");
            var claims = string.Join(", ", Enumerable.Range(0, rng.Next(1, 4))
                .Select(_ => RandClaim(rng))
                .Distinct()); // 去重：同 footprint 内重复 Claim 会被 P0-4 拒——fuzzer 语料须合法
            sb.Append($"{{ \"lifetime\": [{lo}, {(hiStr == "\"⊤\"" ? "\"⊤\"" : hiStr)}], \"scope\": {RandScope(rng)}{loop}, \"footprint\": [ {claims} ] }}");
        }
        sb.Append(" ]");
        if (rng.Next(3) == 0)
        {
            var (rk, rv, _) = Resources[rng.Next(Resources.Length)];
            sb.Append($", \"budget\": {{ \"{rk}:{rv}\": {rng.Next(1, 16)} }}");
        }
        sb.Append(" }");
        return sb.ToString();
    }

    [Fact]
    public void NightlyFuzz_ParseDialect_AuditDeterministic_RoundTripCloses()
    {
        var daySeed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
        var rng = new Random(daySeed);

        for (int iter = 0; iter < 400; iter++)
        {
            var malformed = iter % 7 == 6; // ~14% 畸形语料
            var json = FuzzScript(rng, malformed);

            Cosmos.EffectAlgebra.EffectScript? script = null;
            try
            {
                script = Cosmos.EffectAlgebra.EffectScriptContract.Parse(json);
            }
            catch (FormatException ex)
            {
                Assert.True(malformed, $"合法形状剧本被拒（反例剧本）：\n{json}\n拒绝原因：{ex.Message}");
                continue; // 畸形输入的合法方言：FormatException 即正确行为
            }
            catch (Exception ex)
            {
                Assert.Fail($"方言违约：非 FormatException 异常 {ex.GetType().Name}（反例剧本）：\n{json}\n{ex}");
            }

            Assert.NotNull(script);

            // 不变量②：Audit 永不抛异常
            var r1 = script.Audit(script.Budget);
            // 不变量③：确定性——同剧本两次运行违例序列一致
            var r2 = script.Audit(script.Budget);
            Assert.Equal(r1.Violations.Length, r2.Violations.Length);
            for (int i = 0; i < r1.Violations.Length; i++)
                Assert.Equal((r1.Violations[i].Kind, r1.Violations[i].AtT), (r2.Violations[i].Kind, r2.Violations[i].AtT));

            // 不变量④：ToJson → Parse 往返闭合（JSON 契约剧本为单一真源形态）
            var back = Cosmos.EffectAlgebra.EffectScriptContract.ToJson(script);
            var reparsed = Cosmos.EffectAlgebra.EffectScriptContract.Parse(back);
            var r3 = reparsed.Audit(reparsed.Budget);
            Assert.Equal(r1.Violations.Length, r3.Violations.Length);
            for (int i = 0; i < r1.Violations.Length; i++)
                Assert.Equal((r1.Violations[i].Kind, r1.Violations[i].AtT), (r3.Violations[i].Kind, r3.Violations[i].AtT)); // P5.2-M5：投影点漂移也要红
        }
    }
}
