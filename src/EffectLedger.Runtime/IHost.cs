// IHost.cs — Godot 唯一接触面（§7 抽象）。运行时层经此与 Godot 交互，本文件零 Godot 依赖。
namespace EffectLedger.Runtime;

/// <summary>§7 — 宿主抽象：运行时层与 Godot 的唯一接触面（消除强依赖）。</summary>
public interface IHost
{
    /// <summary>§7 R5-4 — 壳级 Defer 包装替代 call_deferred（Suspending 时丢弃/延后）。
    /// **契约（审计 2026-09-15 显式化）**：抛异常当且仅当【未入队】——若实现先入队再抛，GodotShell
    /// 会据"抛=未入队"回滚去重登记并允许调用方重试，宿主队列里可能出现两份等价闭包（当次不会双执行，
    /// 由闭包内 Remove 守卫兜底，但已偏离不变式）。实现者请保证入队与返回的原子性。</summary>
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
