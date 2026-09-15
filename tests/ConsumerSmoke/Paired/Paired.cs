// 打包消费烟测——配对样例（构建必须成功、零 EAA 诊断）：
// ① Godot 类型门 + 白名单配对（AddChild → QueueFree 同方法 ⇒ 无 EAA0901）；
// ② Generator 单装（无显式 L1 引用）下 [EffectOverride] 标注方法经真实包链 emit 且可编译
//    （emit 代码硬引用 global::EffectLedger.Signature ⇒ L1 传递闭包被编译器真实验证）。
namespace Godot
{
    // 烟测自带的 Godot 形状替身（与 tests/GateFixture 同法）：分析器类型门按 "Godot" 命名空间放行。
    public class Node
    {
        public Node AddChild(Node n) => this;
        public void QueueFree() { }
    }
}

public static class PairedScene
{
    public static void SpawnAndFree()
    {
        var n = new Godot.Node();
        var child = n.AddChild(new Godot.Node());
        child.QueueFree(); // 配对 release ⇒ 无 EAA0901
    }

    public static class Generated
    {
        [EffectLedger.EffectOverride("smoke: 真实包链（pack → 本地 feed → 还原 → 编译）emit 走查")]
        public static void Load() { }
    }
}
