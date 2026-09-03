// GodotApiStub.cs — 真实 Godot 4.6.3 API 形状占位（非 Godot.NET.Sdk 类型，仅用于在本机/CI 无 Godot 引擎时
// 验证 L2/L3 的「按方法名规范化匹配 §7 白名单」设计确实对接近真实 Godot 方法签名）。
// 方法名/签名取自 §7 白名单（已对照 Godot 4.6.3 开源源码核实 28 个键为真实 Godot API 名）。
// 真实 Godot.NET.Sdk 未安装（本机仅有 GDScript-only 引擎，无 GodotSharp.dll），故以结构等价 stub 替代，
// 保留真实方法名 + 真实形参/返回形状，使 L2 生成的 Compute* 委托与 L3 的 acquire/release 判定得到 faithfully 验证。
namespace Godot.Shapes;

public class Node { public Node? Parent { get; set; } public void QueueFree() { } }
public class Node2D : Node { public Vector2 Position { get; set; } = new Vector2(); public Vector2 GlobalPosition { get; set; } = new Vector2(); public float Rotation { get; set; } public Vector2 Scale { get; set; } = new Vector2(); }
public class Node3D : Node { public Node AddChild(Node node) => node; public void RemoveChild(Node node) { } public void MoveChild(Node node, int toPosition) { } }
public class PhysicsBody2D : Node2D { public Vector2 MoveAndSlide(Vector2 velocity) => velocity; public int GetSlideCollisionCount() => 0; }
public class Resource { }
public class PackedScene : Resource { public Node Instantiate() => new Node(); }
public class ResourceLoader { public static Resource Load(string path) => new Resource(); public static Resource LoadInteractive(string path) => new Resource(); public static Resource Preload(string path) => new Resource(); }
public class Signal { public void EmitSignal(string name) { } public void Connect(string signal, Callable callable) { } public void Disconnect(string signal, Callable callable) { } public bool IsConnected(string signal, Callable callable) => false; }
public class Callable { }
public class CanvasItem { public void DrawMesh(object mesh, object material) { } public void DrawRect(object rect) { } public void SetMaterialOverride(object material) { } }
public class AudioStreamPlayer { public void Play() { } public void Stop() { } public void Seek(double toPosition) { } public void SetVolumeDb(float db) { } }
public class Input { public static bool IsActionPressed(string action) => false; public static bool IsActionJustPressed(string action) => false; public static Vector2 GetMousePosition() => new Vector2(); }
public class Vector2 { public float X; public float Y; public Vector2() { X = 0; Y = 0; } public Vector2(float x, float y) { X = x; Y = y; } }
