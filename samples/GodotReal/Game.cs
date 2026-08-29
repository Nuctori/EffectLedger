// GodotReal — 最小 net8 消费样板：验证 L1/L2/L3 在 net8 下无 CS8032/CS0433。
// 游戏侧照常写 Godot 风格 API（AddChild/QueueFree），由 L3 按白名单审计；此处以 IHost 抽象保持零 Godot 依赖。
using Cosmos.EffectAlgebra;

public class Game
{
    public void Spawn()
    {
        var s = new ScopeId.Scene("Battle");
        var sig = Signature.Of(
            new Claim(Kind.Occupy, new ResourceId.Memory(0), Mode.Create, s, Interval.Exact(1)));
        // 模拟 AddChild -> QueueFree 配对，L3 审计可识别 Builtin 38 条中的 AddChild/QueueFree
        _ = sig;
    }
}
