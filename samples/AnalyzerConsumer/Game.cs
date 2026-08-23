using Cosmos.EffectAlgebra;

namespace Game;

// 故意泄漏：仅 acquire（AddChild）无 release、无 [EffectOverride] → 应触发 EAA0901（§3.3.1 DO-9）。
public sealed class Leaker
{
    private object? _child;
    public void AddChild(object x) => _child = x;
    public void Spawn() => AddChild(new object());
}

// 平衡：AddChild + QueueFree 配对 → 不应报 EAA0901。
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
