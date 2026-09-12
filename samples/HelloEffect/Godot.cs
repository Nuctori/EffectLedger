// Godot 类型替身（真实 Godot 命名空间——分析器的类型门按命名空间识别，A2-09）。
// CustomSpawn/CustomDespawn 不在基础白名单——由本工程 effectledger.config.json 扩展解释（白名单扩展演示）。
namespace Godot;

public class Node
{
    public Node AddChild(Node n) => n;
    public void QueueFree() { }
    public void CustomSpawn() { }
    public void CustomDespawn() { }
}
