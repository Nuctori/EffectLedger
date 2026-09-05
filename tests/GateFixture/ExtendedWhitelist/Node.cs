// 门禁 fixture 的 Godot 类型替身（与 Leaky/Paired 同型）：真实 Godot 命名空间（类型门按命名空间识别）。
// CustomSpawn/CustomDespawn 不在基础白名单——只能由 cosmos.effect.json 扩展解释（真接线证明载体）。
namespace Godot;

public class Node
{
    public Node AddChild(Node n) => n;
    public void QueueFree() { }
    public void CustomSpawn() { }
    public void CustomDespawn() { }
}
