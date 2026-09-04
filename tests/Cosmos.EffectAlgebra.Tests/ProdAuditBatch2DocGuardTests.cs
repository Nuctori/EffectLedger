// ProdAuditBatch2DocGuardTests.cs — 独立生产就绪审计（2026-09）批 2 回归钉：模板/文档-代码一致性。
// 文档即测试（doc-as-test）模式扩展：README/templates/schema 的可执行承诺在此逐条守卫。
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class ProdAuditBatch2DocGuardTests
{
    static string RepoRoot()
    {
        // 从 bin/Debug|Release/net10.0 上溯到仓库根（同 PublicApi 守卫的目录上溯法）。
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cosmos.EffectAlgebra.slnx")))
            dir = dir.Parent!;
        return dir!.FullName;
    }

    static string ReadRepo(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    // ── A1-03/A4-02：官方模板必须被自家 Parse 接受（此前根级 $schema 被白名单拒绝，README 却推荐该模板） ──
    [Fact]
    public void Template_EffectScriptJson_ParsesAndAudits()
    {
        var json = ReadRepo("templates/effect-script.json");
        var script = EffectScriptContract.Parse(json); // 修改前：FormatException「根层未知键 "$schema"」
        Assert.NotEmpty(script.Events);
        var audit = script.Audit(script.Budget);
        Assert.True(audit.Passed, $"模板自洽性破坏：{string.Join("; ", audit.Violations.Select(v => $"{v.Kind}@{v.AtT}"))}");
    }

    [Fact]
    public void Schema_AllowsSchemaKeyAtRoot()
    {
        // $schema 是 JSON 工程惯例键——schema 白名单（additionalProperties:false）必须为其留位，
        // 否则"用 schema 校验模板"与"用 Parse 校验模板"互相矛盾。
        using var doc = JsonDocument.Parse(ReadRepo("docs/effect-script.schema.json"));
        var props = doc.RootElement.GetProperty("properties");
        Assert.True(props.TryGetProperty("$schema", out _), "docs/effect-script.schema.json 根级 properties 须声明 $schema");
    }

    // ── A4-01/A2-01：README 快速上手的接线必须是真接线（OutputItemType="Analyzer"），不得死接线假绿 ──
    [Fact]
    public void Readme_Quickstart_AnalyzerWiring_IsRealAnalyzerReference()
    {
        var readme = ReadRepo("README.md");
        // 抽取 ① 节的 xml 代码块（含 ProjectReference 的第一块；CRLF 兼容）
        var block = Regex.Matches(readme, "```xml\r?\n(.*?)```", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value)
            .FirstOrDefault(b => b.Contains("ProjectReference") && b.Contains("Analyzer.csproj"));
        Assert.True(block is not null, "README 快速上手①须含 Analyzer/Generator 的 ProjectReference xml 块");
        foreach (var proj in new[] { "Cosmos.EffectAlgebra.Analyzer.csproj", "Cosmos.EffectAlgebra.Generator.csproj" })
        {
            // <ProjectReference ...> 元素可能跨行展开（Include 与属性分行），按元素整体提取
            var m = Regex.Match(block!, @"<ProjectReference\b[^>]*" + Regex.Escape(proj) + "[^>]*>", RegexOptions.Singleline);
            Assert.True(m.Success, $"README ① 须含 {proj} 的 ProjectReference");
            Assert.Contains("OutputItemType=\"Analyzer\"", m.Value); // 修改前：裸 ProjectReference ⇒ 诊断/生成一个都不触发
            Assert.Contains("ReferenceOutputAssembly=\"false\"", m.Value);
        }
        // L1 必须以普通引用接入（生成器 emit 的代码引用 global::Cosmos.EffectAlgebra.Signature 等；路径斜杠无关）
        Assert.True(Regex.IsMatch(block!, @"Include=""[^""]*Cosmos\.EffectAlgebra[\\/]Cosmos\.EffectAlgebra\.csproj"""),
            "README ① 须含 L1 的普通 ProjectReference");
    }

    // ── A4-05：README 代码栅栏必须成对（第91行的孤立 ``` 曾把后半篇全部渲染成代码块） ──
    [Fact]
    public void Readme_CodeFences_Balanced()
    {
        var lines = ReadRepo("README.md").Split('\n');
        int open = 0;
        foreach (var l in lines)
            if (l.TrimStart().StartsWith("```") && !l.TrimStart().StartsWith("> "))
                open++;
        Assert.Equal(0, open % 2);
    }

    // ── A4-10：每张表格内行列数一致（代码 span 内的裸 | 曾打碎表格；不同表列数可不同，分块校验） ──
    [Fact]
    public void Readme_Tables_ColumnCountConsistent()
    {
        var lines = ReadRepo("README.md").Split('\n');
        var blocks = new System.Collections.Generic.List<System.Collections.Generic.List<string>>();
        var cur = new System.Collections.Generic.List<string>();
        foreach (var raw in lines)
        {
            var l = raw.TrimEnd('\r');
            if (l.StartsWith("|")) cur.Add(l);
            else if (cur.Count > 0) { blocks.Add(cur); cur = new System.Collections.Generic.List<string>(); }
        }
        if (cur.Count > 0) blocks.Add(cur);
        Assert.True(blocks.Count >= 2, $"README 须含能力表与分层表，实际 {blocks.Count} 块");
        foreach (var b in blocks)
        {
            var counts = b.Select(r => r.Count(c => c == '|')).Distinct().ToList();
            Assert.True(counts.Count == 1, $"表格行列数不一（| 计数：{string.Join(",", counts)}）——代码 span 内的裸 | 须转义或改写：{b[0]}");
        }
    }

    // ── A4-11：README 反引号中的仓库相对路径必须真实存在 ──
    [Fact]
    public void Readme_ReferencedRepoPaths_Exist()
    {
        var readme = ReadRepo("README.md");
        var paths = Regex.Matches(readme, "`([a-zA-Z0-9_./\\-]+)`")
            .Select(m => m.Groups[1].Value)
            .Where(p => p.Contains('/') && !p.StartsWith("http") && !p.Contains('<') && !p.Contains(' ')
                        && (p.EndsWith(".md") || p.EndsWith(".json") || p.EndsWith(".csproj") || p.EndsWith(".cs") || p.EndsWith(".slnx") || p.EndsWith("/*")))
            .Distinct();
        var missing = new System.Collections.Generic.List<string>();
        foreach (var p in paths)
        {
            if (p.EndsWith("/*")) // 通配形态：校验目录存在（如 tests/*/AdvE2E_* 曾指错目录）
            {
                var dir = p[..p.LastIndexOf('/')];
                if (!Directory.Exists(Path.Combine(RepoRoot(), dir))) missing.Add(p);
                continue;
            }
            if (!File.Exists(Path.Combine(RepoRoot(), p.Replace('/', Path.DirectorySeparatorChar)))) missing.Add(p);
        }
        Assert.True(missing.Count == 0, $"README 引用的路径不存在：{string.Join(", ", missing)}");
    }

    // ── A4-12/计数漂移：README 不得再硬编码会漂移的全量测试总数（"N passed" 形态） ──
    [Fact]
    public void Readme_TestCount_ClaimsStructureNotBrittleTotal()
    {
        var readme = ReadRepo("README.md");
        Assert.DoesNotMatch(@"\*\*\d{3,} passed\*\*", readme); // 修改前："**547 passed**" 与实际 550+ 漂移
        // 结构化计数（95 Runtime + N Tests + 73 SampleGame）保留：95/73 为稳定锚点
        var m = Regex.Match(readme, @"(\d+)\s*Runtime\s*\+\s*(\d+)\s*Tests\s*\+\s*(\d+)\s*SampleGame");
        if (m.Success)
        {
            Assert.Equal("95", m.Groups[1].Value);
            Assert.Equal("73", m.Groups[3].Value);
        }
    }

    // ── A4-08：PackAsTool 的 Tool 必须在 slnx（否则 cosmos-audit.yml 的 dotnet pack 永远不打 Tool 包） ──
    [Fact]
    public void Slnx_ContainsToolProject()
    {
        Assert.Contains("Cosmos.EffectAlgebra.Tool", ReadRepo("Cosmos.EffectAlgebra.slnx"));
    }

    // ── A1-03 配套：模板防假绿的 README 承诺（"模板…可防"）以模板可解析为前提，守卫其句内路径真实 ──
    [Fact]
    public void Readme_TemplatePath_MatchesActualTemplate()
    {
        var readme = ReadRepo("README.md");
        Assert.Contains("templates/effect-script.json", readme);
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "templates", "effect-script.json")));
    }

    // ── R2B-05：README 须含 NuGet 安装章节（含三包命令——单装 Generator 无 L1 会编译失败） ──
    [Fact]
    public void Readme_NuGetConsumption_SectionExists()
    {
        var readme = ReadRepo("README.md");
        Assert.Contains("dotnet add package Cosmos.EffectAlgebra", readme);
        Assert.Contains("dotnet add package Cosmos.EffectAlgebra.Generator", readme);
    }

    // ── R2B-02：分析器/生成器宿主前提必须声明（VS/.NET Framework MSBuild 静默不加载） ──
    [Fact]
    public void Readme_HostPrerequisite_Declared()
    {
        var readme = ReadRepo("README.md");
        Assert.Matches(new System.Text.RegularExpressions.Regex("Visual Studio[^\n]{0,80}(静默|不加载|宿主)"), readme);
    }

    // ── R2B-06：cosmos CLI 退出码契约必须有外部文档（0=通过/2=违例/1=错误，与常见惯例不同） ──
    [Fact]
    public void Readme_ExitCodeContract_Documented()
    {
        var readme = ReadRepo("README.md");
        Assert.Contains("退出码", readme);
        // 三种退出码语义都在文档中（防止 CI 作者按 1=违例 的惯例把门接反）
        Assert.True(
            new System.Text.RegularExpressions.Regex("exit code").IsMatch(readme) && readme.Contains("| `0` |"),
            "README 须含退出码表格（0=通过）");
        Assert.Contains("`2`", readme);
    }

    // ── R2A-11：EFFECT_SCRIPT §4 契约主文档须记载 inf 别名与 $schema 键（Parse 接受集 = 文档声明集） ──
    [Fact]
    public void EffectScriptDoc_ContractPoints_Synced()
    {
        var doc = ReadRepo("EFFECT_SCRIPT.md");
        Assert.Contains("inf", doc);
        Assert.Contains("$schema", doc);
        Assert.Contains("memory:", doc);
    }

    // ── R2A-04：JSON Schema 与 Parse 同界——lifetime lo 禁 ⊤、size 双 ⊤ 合法、budget memory 段数字 ──
    [Fact]
    public void JsonSchema_Constraints_MatchParse()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(ReadRepo("docs/effect-script.schema.json"));
        var evProps = doc.RootElement.GetProperty("properties").GetProperty("events")
            .GetProperty("items").GetProperty("properties");
        // lifetime 用 prefixItems 且 lo（首位）为纯 integer（禁 ⊤/inf）
        var lifetime = evProps.GetProperty("lifetime");
        Assert.True(lifetime.TryGetProperty("prefixItems", out var pi));
        Assert.Equal("integer", pi[0].GetProperty("type").GetString());
        // size 是 anyOf 双分支（lo=⊤ 仅当 hi=⊤）
        var size = evProps.GetProperty("footprint").GetProperty("items").GetProperty("properties").GetProperty("size");
        Assert.True(size.TryGetProperty("anyOf", out _));
        // budget：memory 段须数字、字符串 id 非空
        var patterns = doc.RootElement.GetProperty("properties").GetProperty("budget").GetProperty("patternProperties");
        Assert.True(patterns.TryGetProperty("^memory:\\d+$", out _));
        Assert.True(patterns.TryGetProperty("^(gpu|commandBuffer|occupancy|signalBus|custom):\\S+$", out _));
    }
}
