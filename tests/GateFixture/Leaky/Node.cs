// 门禁 fixture 的 Godot 类型替身：放在真实的 Godot 命名空间下（类型门按命名空间识别）。
namespace Godot;

public class Node
{
    public Node AddChild(Node n) => n;   // acquire 类 API（ApiMapping 白名单命中）
    public void QueueFree() { }          // release 类 API
}
