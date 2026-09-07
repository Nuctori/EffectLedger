// QedP1B1PublicApiSnapshotTests.cs — P1-B1 Runtime 公共 API 快照（机制与 L1 侧镜像，见
// tests/Cosmos.EffectAlgebra.Tests/QedP1B1PublicApiSnapshotTests.cs 文件头）：
// 反射枚举 Runtime 程序集全部导出类型的公共成员，与仓库内冻结快照逐字节比对——任何公共面
// 新增/变更/删除即红。重生成：QED_REGEN_API_SNAPSHOT=1 dotnet test（写入后须人工审查 git diff）。xUnit。
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class QedP1B1PublicApiSnapshotTests
{
    const string AssemblyName = "Cosmos.EffectAlgebra.Runtime";

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cosmos.EffectAlgebra.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（Cosmos.EffectAlgebra.slnx）");
    }

    static string SnapPath() => Path.Combine(RepoRoot(), "tests", "Cosmos.EffectAlgebra.Runtime.Tests",
        $"PublicApiSnapshot.{AssemblyName}.txt");

    [Fact]
    public void Runtime_PublicApi_MatchesFrozenSnapshot()
    {
        var current = Render(Assembly.Load(AssemblyName));
        var snap = SnapPath();
        var regen = Environment.GetEnvironmentVariable("QED_REGEN_API_SNAPSHOT") == "1";

        if (regen)
        {
            File.WriteAllText(snap, current);
            return; // 显式重生成：审查 git diff 后随实现一并提交
        }

        Assert.True(File.Exists(snap), $"快照缺失：{snap}（QED_REGEN_API_SNAPSHOT=1 生成初版并审查提交）");
        var frozen = File.ReadAllText(snap);
        if (frozen != current)
            Assert.Fail($"""
                Runtime 公共 API 面与冻结快照不一致（P1-B1 冻结机制）：任何公共面变更 = 破坏性，须显式 semver major 决策并记录。
                若变更已有决策背书：QED_REGEN_API_SNAPSHOT=1 dotnet test --filter FullyQualifiedName~QedP1B1 重生成，审查 git diff 后提交。
                === 差异（- 快照 / + 当前，各至多 30 行）===
                {Diff(frozen, current)}
                """);
    }

    internal static string Render(Assembly asm)
    {
        var blocks = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var t in asm.GetExportedTypes())
        {
            // 【P5.3-L10】不再按命名空间过滤：导出类型全集入快照（防「新命名空间里的公共类=冻结面外
            // 膨胀」的枚举缝隙——红队 P5.2-L10）。当前全部导出类型本就在 Cosmos.EffectAlgebra* 下，
            // 过滤移除应为零差异（历史上证明该过滤是冗余防御）。
            blocks[$"{KindOf(t)} {t.FullName}"] = Members(t).Select(m => "  " + m).ToArray();
        }
        return string.Join("\n", blocks.SelectMany(kv => new[] { kv.Key }.Concat(kv.Value))) + "\n";
    }

    static string KindOf(Type t) =>
        t.IsEnum ? "enum" :
        t.IsValueType ? "struct" :
        t.IsInterface ? "interface" :
        t.BaseType == typeof(MulticastDelegate) ? "delegate" : "class";

    static IEnumerable<string> Members(Type t)
    {
        const BindingFlags F = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var c in t.GetConstructors(F).OrderBy(c => c.ToString(), StringComparer.Ordinal))
            yield return $"ctor {string.Join(", ", c.GetParameters().Select(p => p.ParameterType))}";
        foreach (var f in t.GetFields(F).OrderBy(f => f.Name, StringComparer.Ordinal))
            yield return $"field {f.FieldType} {f.Name}" + (f.IsLiteral && f.GetRawConstantValue() is { } v ? $" = {v}" : "");
        foreach (var p in t.GetProperties(F).OrderBy(p => p.Name, StringComparer.Ordinal))
            yield return $"prop {p.PropertyType} {p.Name}({string.Join(", ", p.GetIndexParameters().Select(ip => ip.ParameterType))})";
        foreach (var e in t.GetEvents(F).OrderBy(e => e.Name, StringComparer.Ordinal))
            yield return $"event {e.EventHandlerType} {e.Name}";
        foreach (var m in t.GetMethods(F).OrderBy(m => m.ToString(), StringComparer.Ordinal))
        {
            if (m.IsSpecialName && (m.Name.StartsWith("get_", StringComparison.Ordinal) || m.Name.StartsWith("set_", StringComparison.Ordinal)))
                continue; // 属性访问器由 prop 行承载
            if (m.Name.Contains('<')) continue; // 编译器合成（<Clone>$ 等）
            if (m.GetCustomAttribute<CompilerGeneratedAttribute>() is not null) continue;
            yield return $"method {m.ReturnType} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType))})";
        }
    }

    static string Diff(string frozen, string current)
    {
        var f = frozen.Split('\n');
        var c = current.Split('\n');
        return string.Join("\n",
            f.Except(c).Take(30).Select(l => "- " + l)
            .Concat(c.Except(f).Take(30).Select(l => "+ " + l)));
    }
}
