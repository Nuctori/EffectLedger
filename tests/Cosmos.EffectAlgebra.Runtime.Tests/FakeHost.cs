// FakeHost.cs — 测试替身 IHost（记录调用，供断言 ProcessMode 级联 / Defer 合并 / 退出排空）。
// 【P1-B2】自 src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs 迁出——测试替身不进入发布程序集的
// 公共面（公共 API 快照 QedP1B1 已同步收缩）。命名空间保持 Cosmos.EffectAlgebra.Runtime，
// 既有测试代码（using Cosmos.EffectAlgebra.Runtime）零改动。
// A3-12：IHost.EnqueueExitDrain 死成员已移除——GodotShell 自持 _exitDrains，宿主从不接收 drain 注册。
using Cosmos.EffectAlgebra.Runtime;

namespace Cosmos.EffectAlgebra.Runtime;

public sealed class FakeHost : IHost
{
    public readonly List<Action> Deferred = new();
    public readonly List<(FiberId Id, bool Disabled)> ProcessModes = new();
    public bool AllValid = true;

    public void Defer(Action action) => Deferred.Add(action);
    /// <summary>测试用：同步执行全部已记录 Defer 闭包（Godot 壳真实宿主由 _Process 帧驱动排空；FakeHost 无帧循环故显式 flush）。</summary>
    public void FlushDeferred() { foreach (var a in Deferred.ToArray()) a(); Deferred.Clear(); }
    // R4-RH-09：布尔参拆双方法——记录 (Id, Disabled=true/false)，断言语义不变。
    public void DisableDispatch(FiberId id) => ProcessModes.Add((id, true));
    public void EnableDispatch(FiberId id) => ProcessModes.Add((id, false));
    public bool IsInstanceValid(object handle) => AllValid;
}
