using EffectLedger;
using Godot;

// R6-P-03 收口：真实 Godot 形状 stub——分析器的 Godot.* 命名空间门控（A2-09）要求调用接收者
// 绑定 Godot/Godot.* 命名空间类型才触发诊断。此前样例只有自有同名方法（namespace Game），
// EAA0901 永不触发，csproj 声称的「EAA0901 真在编译期出现」是绿灯偶然（见 ProdAuditR6ToolingTests 钉）。
namespace Godot
{
    public class Node
    {
        public Node AddChild(Node n) => n;
        public void QueueFree() { }
    }
}

namespace Game
{
    // 真实 Godot 形状故意泄漏：AddChild（§7 create 类）无配对 release、无 [EffectOverride]
    // → 必触发 EAA0901（warning；csproj WarningsNotAsErrors 豁免使 build 保持绿，警告即「分析器活着」的可见证据）。
    public sealed class GodotLeaker : Node
    {
        public void Spawn() => AddChild(new Node());
    }

    // 自有同名方法（非 Godot 命名空间）：按 A2-09 设计零诊断——防同名误报，但也无保护（README ③ 触发前提）。
    public sealed class Leaker
    {
        private object? _child;
        public void AddChild(object x) => _child = x;
        public void Spawn() => AddChild(new object());
    }

    // 自有同名方法配对：同样零诊断（设计如此），保留作为「同名不误报」的对照。
    public sealed class Balanced
    {
        private object? _child;
        public void AddChild(object x) => _child = x;
        public void QueueFree() => _child = null;
        public void Lifecycle()
        {
            AddChild(new object());
            QueueFree();   // 配对释放，净为零
        }
    }
}
