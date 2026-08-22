// SampleGame.cs — 真实 Godot 风格游戏代码样例（stub 类型占位，非真 Godot SDK，仅作 L2/L3 集成载体）。
// 设计意图：证明「游戏侧照常写 Godot API，零门面改动」即可被 L2 生成器 + L3 分析器审计。
// L2 按方法名规范化匹配 §7 白名单（addchild→AddChild, queue_free→QueueFree, remove_child→RemoveChild ...）；
// L3 按 §7 acquire/release-class 数据驱动发 EAA0901（仅 acquire 无 release 且无 [EffectOverride]）。
using Cosmos.EffectAlgebra;

namespace SampleGame;

/// <summary>Godot 风格节点（stub 占位；真实工程为 Godot.Node）。</summary>
public sealed class Node3D
{
    public object? child;
    public object? mesh;
    public object? material;
}

/// <summary>
/// 健康实体：acquire（AddChild）+ release（RemoveChild）在 Spawn/Despawn 中配对 ⇒ L3 不报 EAA0901。
/// AddChild / RemoveChild 标 [EffectOverride] ⇒ L2 生成 ComputeAddChild / ComputeRemoveChild（§7 白名单 Claims）。
/// </summary>
public sealed class HealthyEnemy
{
    private readonly Node3D _node = new();

    [EffectOverride("spawn/despawn 配对：AddChild 占 Tree, RemoveChild 释放 Tree（§7）")]
    public void AddChild(object child) { _node.child = child; }

    [EffectOverride("spawn/despawn 配对：释放 Tree（§7）")]
    public void RemoveChild() { _node.child = null; }

    public void SpawnAndDespawn()
    {
        AddChild(new object());
        RemoveChild(); // 配对释放 ⇒ 平衡
    }
}

/// <summary>
/// 泄漏实体：仅 acquire（AddChild）无释放、无 [EffectOverride] ⇒ L3 必报 EAA0901（DO-9 近似捕捉）。
/// 注意：此源文件作为「被分析」对象，集成测试会单独编译它并断言诊断出现。
/// </summary>
public sealed class LeakyEnemy
{
    private readonly Node3D _node = new();

    public void AddChild(object child) { _node.child = child; } // 仅 acquire

    public void Spawn()
    {
        AddChild(new object()); // 无对应 RemoveChild ⇒ 泄漏
    }
}

/// <summary>
/// 已知临时占用：仅 acquire 但标 [EffectOverride]（逃逸通道，reason 引用证据）⇒ L3 不报 EAA0901。
/// </summary>
public sealed class IntentionalTempOccupancy
{
    private readonly Node3D _node = new();

    public void AddChild(object child) { _node.child = child; }

    [EffectOverride("帧内临时占用，已知泄漏（人工 approve，§8.3）")]
    public void TempHold()
    {
        AddChild(new object());
    }
}

/// <summary>
/// 多重 acquire/release 顺序混合：Instantiate(create) + QueueFree(release) 配对（§7 release-class）⇒ 平衡。
/// 标 [EffectOverride] 让 L2 生成对应 Compute 方法。
/// </summary>
public sealed class PooledParticles
{
    [EffectOverride("实例化粒子（create）")]
    public void Instantiate(object prefab) { }

    [EffectOverride("释放粒子（§7 release-class: queue_free）")]
    public void QueueFree() { }

    public void Burst()
    {
        Instantiate(new object());
        QueueFree();
    }
}
