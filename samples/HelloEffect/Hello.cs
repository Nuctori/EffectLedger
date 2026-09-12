using EffectLedger;
using Godot;

namespace HelloEffect;

// ── 正确用法 1：配对的 acquire/release ⇒ 零诊断。 ──
public sealed class Pool : Node
{
    public void Spawn() => CustomSpawn();
    public void Despawn() => CustomDespawn();
}

public static class Demo
{
    // ── 正确用法 2：白名单扩展 API（effectledger.config.json）配对使用 ⇒ 零诊断。 ──
    public static void SpawnAndDespawn(Pool pool)
    {
        pool.Spawn();
        pool.Despawn();
    }

    // ── 演示泄漏：AddChild 无配对 release ⇒ EAA0901（warning，构建成功但警告可见）。 ──
    //    修复方式：补 QueueFree（见 Pool 的配对形态）。
    public static void SpawnLeak(Node parent) => parent.AddChild(new Node());

    // ── 正确用法 3：跨方法配对属静态近似盲区 ⇒ [EffectOverride("理由")] 声明意图（不豁免 EAA0901）。 ──
    [EffectOverride("生命周期由 Pool 管理本节点，QueueFree 在 Pool.Despawn 内调用")]
    public static void SpawnManaged(Node parent) => parent.AddChild(new Node());
}
