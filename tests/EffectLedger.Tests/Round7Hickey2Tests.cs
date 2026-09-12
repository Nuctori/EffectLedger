using Xunit;
using System.IO;
using System.Linq;
using System.Collections.Immutable;
using EffectLedger;

namespace EffectLedger.Tests;

/// <summary>rich-hickey2 R7（文档示例可运行性）— doc 即测试：文档改一字 CI 即红。D07-001/002/004/006。</summary>
public class Round7Hickey2Tests
{
    // ── D07-001：README §4 演示 JSON 必须可 Parse + Audit（"用户复制 README 跑"=第一次接触有护城河） ──
    static string ExtractReadmeSection4Json()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var md = File.ReadAllText(Path.Combine(dir!.FullName, "README.md"));
        int sec = md.IndexOf("声明式剧本数据契约", StringComparison.Ordinal);
        Assert.True(sec >= 0, "README 缺 §4 演示");
        // README §4 演示用 raw-string 字面量 $$""" ... """ 包裹 JSON（非 ```json fence）
        const string open = "$$\"\"\"";
        const string close = "\"\"\"";
        int start = md.IndexOf(open, sec, StringComparison.Ordinal);
        Assert.True(start >= 0, "README §4 演示未用 $$ \"\"\" 包裹 JSON");
        int body = start + open.Length;
        int end = md.IndexOf(close, body, StringComparison.Ordinal);
        Assert.True(end >= 0, "README §4 演示未闭合 \"\"\"");
        return md[body..end].Trim();
    }

    [Fact]
    public void Readme_Example_ParsesAndAudits()
    {
        var json = ExtractReadmeSection4Json();
        var script = EffectScriptContract.Parse(json); // 修复前：claim scope 缺失/事件级 scope 缺失会抛
        Assert.Equal(2, script.Events.Length);
        var at5 = script.At(NatStar.Of(5));
        Assert.Single(at5.OccupyClaims); // 单点投影：t=5 仅首个 alive 事件在
        var audit = script.Audit(script.Budget);
        Assert.True(audit.Passed);                 // 该示例自洽：无违例
        Assert.Equal(2, audit.CapsChecked);        // 两资源峰值门实际运行（非零预算，D07-006）
    }

    // ── D07-002：resource 扁平字符串形态是唯一契约；嵌套对象形态（历史 spec-drift）必须 loud 拒 ──
    [Fact]
    public void NestedResourceShape_Rejected_WithResourceInMessage()
    {
        var json = """{"events":[{"lifetime":[0,1],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"gpu":{"bufferId":"m"}},"mode":"use","scope":{"scene":"S"}}]}]}""";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("resource", ex.Message);
    }

    // ── D07-004/006：Budget.None 时峰值门不运行 ⇒ IsPeakChecked==false（别把"没查"当"全绿"） ──
    [Fact]
    public void ZeroBudget_PeakGateNotRun_NotPeakChecked()
    {
        var e = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), new ScopeId.Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("r")), Mode.Create, new ScopeId.Scene("A"), Interval.Exact(10))), LoopCount.Of(1));
        var aud = new EffectScript(ImmutableArray.Create(e)).Audit(Budget.None);
        Assert.Equal(0, aud.CapsChecked);
        Assert.False(aud.IsPeakChecked);
        // 注：单 create 无 release 会在闭包路径报 Leak（守恒门独立运行），这里只验证峰值门状态语义
    }
}
