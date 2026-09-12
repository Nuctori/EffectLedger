// effectledger audit — EffectScript JSON 一键门（AI 闭环用，零 Godot 依赖）
// 用法：dotnet run --project src/EffectLedger.Tool -- audit effect-script.json [--out violations.json]
// 退出码：0=Passed, 2=Violations, 1=FormatException/IO
using System.Text.Json;
using EffectLedger;

// 【QED-P5.2 方言 HIGH】stdout 编码钉 UTF-8：默认跟随控制台代码页（中文 Windows = GBK），
// 重定向给 LLM 消费时 ⊤/中文 会退化成 '?'——回修载荷语义丢失（od 逐字节实证）。
// 重定向或宿主不支持时保持默认（不因编码设置失败而中断审计）。
try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("usage: effectledger audit <script.json> [--out violations.json]");
    Console.WriteLine("  解析 EffectScriptContract.Parse → Audit → 打印 Violation[t,r,Kind,EventIndex]，Failed 时 violations 可喂回 LLM 重投");
    return 0;
}
if (args[0] != "audit")
{
    Console.Error.WriteLine($"unknown command: {args[0]} (only 'audit')");
    return 1;
}
if (args.Length < 2) { Console.Error.WriteLine("audit 需要 <script.json>"); return 1; }
var input = args[1];
string? outPath = null;
for (int i = 2; i < args.Length; i++) if (args[i] == "--out" && i + 1 < args.Length) outPath = args[++i];

string json;
try { json = File.ReadAllText(input); }
catch (Exception ex) { Console.Error.WriteLine($"read {input}: {ex.Message}"); return 1; }

EffectScript script;
try { script = EffectScriptContract.Parse(json); }
catch (FormatException ex) { Console.Error.WriteLine($"parse FormatException: {ex.Message}"); return 1; }
catch (Exception ex) { Console.Error.WriteLine($"parse: {ex.Message}"); return 1; }

var result = script.Audit(script.Budget);
var payload = new
{
    passed = result.Passed,
    // QED-P5.3 方言 LOW 收口：透出峰值门状态——AI 闭环可区分「没查（缺 budget）/ 查了 / 幽灵查（CapsChecked>0 但无违例）」
    capsChecked = result.CapsChecked,
    isPeakChecked = result.IsPeakChecked,
    events = script.Events.Length, // R3-CG-08：审计规模进载荷——空剧本全绿不再是不可见的「没查当全绿」
    violations = result.Violations.Select(v => new
    {
        kind = v.Kind.ToString(),
        atT = v.AtT.ToString(),
        resource = v.Resource.ToString(),
        scope = v.Scope.ToString(),
        eventIndex = v.EventIndex,
        detail = v.Detail
    }).ToArray()
};
var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var outJson = JsonSerializer.Serialize(payload, opts);
Console.WriteLine(outJson);
if (outPath != null)
{
    // R3-CG-05（三轮审计）：落盘失败须落契约退出码 1（此前未处理 IO 异常炸出非 0/1/2 码，破坏机器可读契约）。
    try { File.WriteAllText(outPath, outJson); }
    catch (Exception ex) { Console.Error.WriteLine($"write {outPath}: {ex.Message}"); return 1; }
}
return result.Passed ? 0 : 2;
