namespace LeakyFixture;

using Godot;

// A2-02 门禁存在性 fixture：acquire 无配对 release ⇒ EAA0901。
// .editorconfig 将 EAA0901 设为 error ⇒ dotnet build 必须失败（对门禁做 mutation 测试）。
public static class Leaky
{
    public static void Spawn()
    {
        var g = new Node();
        var child = g.AddChild(new Node());
        _ = child;
        // 无 QueueFree ⇒ EAA0901（error）⇒ 构建红
    }
}
