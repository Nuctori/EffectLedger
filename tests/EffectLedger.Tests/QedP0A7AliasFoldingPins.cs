// QedP0A7AliasFoldingPins.cs — QED 迭代 P0-A7 归一化实例身份定稿钉（iter55 PO-55-08）：
// §7 API 白名单层对无身份差分资源族（Callback("cb")/AudioMixer(0)/裸名哨兵）采用**常量实例保守合并**
// ——跨调用点折叠是显式契约：Connect(sigA)+Disconnect(sigB) ⇒ net=0 的泄漏掩蔽属静态近似已声明盲区，
// 权威判定=运行期 Σnet（README ③/⑦ 宪法）。JSON 契约面（冻结核心）强制显式资源 id、拒裸名
// ⇒ 不同 id 即不同资源，无折叠。参数化 alias（按实参派生身份）记 F 轨候选（与 F1 流敏感化同窗）。
// 决策记录：PDR §3.1.4a【QED-A7】+ README 诚实边界 #20 + audit/qed/ROADMAP A7。xUnit。
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace EffectLedger.Tests;

public class QedP0A7AliasFoldingPins
{
    static ScopeId ShellScope() => new ScopeId.Shell();
    static ScopeId Global() => new ScopeId.Global();

    // ── 钉 1（掩蔽=显式契约，iter55 原反例形状）：白名单 Connect/Disconnect 形状的 claim 对
    //    （同一常量 Callback 实例、同 size [8,8]）在 net 上精确抵消——泄漏掩蔽在本层为已声明行为。 ──
    [Fact]
    public void Whitelist_ConnectDisconnect_ConstantInstance_NetZero()
    {
        var cb = new ResourceId.Callback("cb");
        var connect = new Claim(Kind.Occupy, cb, Mode.Create, ShellScope(), Interval.Exact(8)).Normalize();
        var disconnect = new Claim(Kind.Occupy, cb, Mode.Release, ShellScope(), Interval.Exact(8)).Normalize();
        var sig = Signature.Of(connect, disconnect);

        var net = NetTable.Compute(sig, Global()).Get(ResourceId.Normalize(cb));
        Assert.Equal(0L, net.Lo.Value);
        Assert.Equal(0L, net.Hi.Value);
    }

    // ── 钉 2（折叠住在白名单表里）：Connect 与 Disconnect 的白名单条目发同一常量 Callback 实例——
    //    未来 F 轨参数化 alias（按实参派生身份）落地时必须有意更新本钉（防静默语义漂移）。 ──
    [Fact]
    public void Whitelist_Connect_And_Disconnect_ShareConstantCallbackInstance()
    {
        var connect = GodotApiWhitelist.All.Single(m => m.GodotApi == "Connect");
        var disconnect = GodotApiWhitelist.All.Single(m => m.GodotApi == "Disconnect");

        var cbConnect = connect.Claims.Single(c => c.Resource is ResourceId.Callback);
        var cbDisconnect = disconnect.Claims.Single(c => c.Resource is ResourceId.Callback);

        Assert.Equal(ResourceId.Normalize(cbConnect.Resource), ResourceId.Normalize(cbDisconnect.Resource));
    }

    // ── 钉 3（冻结核心无折叠）：JSON 契约面显式 id 即身份——texA 的 create 与 texB 的 release
    //    不互相抵消（Leak 与 NegativeDip 并存）；若发生折叠则剧本全绿，本钉必红。 ──
    [Fact]
    public void Json_ExplicitInstanceIds_NeverFold()
    {
        const string json = """
            {
              "events": [
                { "lifetime": [0, 10], "scope": { "scene": "Battle" },
                  "footprint": [ { "kind": "occupy", "resource": { "memory": 101 }, "mode": "create", "size": [64, 64] } ] },
                { "lifetime": [5, 10], "scope": { "scene": "Battle" },
                  "footprint": [ { "kind": "occupy", "resource": { "memory": 102 }, "mode": "release", "size": [64, 64] } ] }
              ]
            }
            """;
        var script = EffectScriptContract.Parse(json);
        var result = script.Audit(script.Budget);

        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "Leak");
        Assert.Contains(result.Violations, v => v.Kind == "NegativeDip");
        var leakRes = result.Violations.First(v => v.Kind == "Leak").Resource;
        var dipRes = result.Violations.First(v => v.Kind == "NegativeDip").Resource;
        Assert.NotEqual(leakRes, dipRes); // 两个显式 id 是两个资源——无折叠
    }
}
