// IHost.cs — Godot 唯一接触面（§7 抽象）。运行时层经此与 Godot 交互，本文件零 Godot 依赖。
namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§7 — 宿主抽象：运行时层与 Godot 的唯一接触面（消除强依赖）。</summary>
public interface IHost
{
    /// <summary>§7 R5-4 — 壳级 Defer 包装替代 call_deferred（Suspending 时丢弃/延后）。</summary>
    void Defer(Action action);

    /// <summary>§7 — 禁用 Fiber 子树派发（Suspending/TearingDown；R4-RH-09：拆布尔参，消除 true=禁用 的翻转陷阱）。</summary>
    void DisableDispatch(FiberId id);

    /// <summary>§7 — 恢复 Fiber 子树派发（与 DisableDispatch 成对；库内当前无恢复调用点，自定义宿主实现者补全语义用）。</summary>
    void EnableDispatch(FiberId id);

    // A3-12（生产审计批3）：EnqueueExitDrain 死成员已移除——GodotShell 自持退出 drain 列表并自行 FlushExitDrain，
    // 宿主从不接收注册；保留该成员会让自定义宿主实现者误以为经 IHost 注册即可参与退出排空。

    /// <summary>§1 R4-3 — 原生句柄判空（Godot Object 经 QueueFree 后的裸引用）。</summary>
    bool IsInstanceValid(object handle);
}
