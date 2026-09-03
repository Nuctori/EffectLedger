namespace PairedFixture;

using Godot;

// 配对 fixture：acquire/release 同方法配对 ⇒ 无 EAA0901 ⇒ 构建绿（门禁不误报的证明）。
public static class Paired
{
    public static void SpawnAndCleanup()
    {
        var g = new Node();
        var child = g.AddChild(new Node());
        child.QueueFree(); // 配对 ⇒ 不报
    }
}
