// IHost.cs — Godot 唯一接触面（§7 抽象）。运行时层经此与 Godot 交互，本文件零 Godot 依赖。
namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§7 — 宿主抽象：运行时层与 Godot 的唯一接触面（消除强依赖）。</summary>
public interface IHost
{
    /// <summary>§7 R5-4 — 壳级 Defer 包装替代 call_deferred（Suspending 时丢弃/延后）。</summary>
    void Defer(Action action);

    /// <summary>§7 — 设置 Fiber 子树 ProcessMode=Disabled（Suspending/TearingDown 暂停派发）。</summary>
    void SetProcessMode(FiberId id, bool disabled);

    /// <summary>§3 R4-1 — 关闭/退出路径同步排空队列（在调度器 _ExitTree 内执行）。</summary>
    void EnqueueExitDrain(Action drain);

    /// <summary>§1 R4-3 — 原生句柄判空（Godot Object 经 QueueFree 后的裸引用）。</summary>
    bool IsInstanceValid(object handle);
}
