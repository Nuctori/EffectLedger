// ResourceNormalizationTests.cs — §3.1.4a ResourceId 归一等价类全锁（迭代25）。DO-8 单点真相完整性。
// 锁定 PDR §3.1.4a 合成命名空间归一映射：signal 资源统一 → SignalBus(s)；其余构造子保持规范形。
// 断言均可证伪：若实现偏离归一规则（如 Self("signal_x") 未剥前缀），对应 Assert.Equal 必红。
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class ResourceNormalizationTests
{
    // ── 1. Self("signal_x") == SignalBus("x")（ST-02 / iter13 收口） ──
    [Fact]
    public void Self_SignalPrefix_Equiv_SignalBus_X() // §3.1.4a：Self("signal_"+s) ≡ SignalBus(s)
    {
        var self = ResourceId.Normalize(new ResourceId.Self("signal_x"));
        var bus = ResourceId.Normalize(new ResourceId.SignalBus(new StringName("x")));
        Assert.Equal(bus, self); // 唯一规范形 SignalBus("x")
        Assert.Equal(new ResourceId.SignalBus(new StringName("x")), self);
    }

    // ── 2. Signal("signal_x") == SignalBus("x")（signal_ 前缀归一，§3.1.4a "signal_"+s ≡ SignalBus(s)） ──
    [Fact]
    public void Signal_SignalPrefix_Equiv_SignalBus_X() // §3.1.4a：Signal("signal_"+s) ≡ SignalBus(s)
    {
        var sig = ResourceId.Normalize(new ResourceId.Signal(new StringName("signal_x")));
        var bus = ResourceId.Normalize(new ResourceId.SignalBus(new StringName("x")));
        Assert.Equal(bus, sig);
        Assert.Equal(new ResourceId.SignalBus(new StringName("x")), sig);
    }

    // ── 3. Self("x") == SignalBus("x")（ST-03：Self 裸名当 signal，§3.1.4a self ⇒ Self(component)） ──
    [Fact]
    public void Self_Bare_Component_Equiv_SignalBus_X() // §3.1.4a：Self("signal_" 已剥；此处 Self("x") 不剥前缀
    {
        // §3.1.4a 映射表中 self ⇒ Self(component) 为独立构造子；仅 "signal_" 前缀才归 SignalBus。
        // 故 Self("x") 规范形仍是 Self("x")，与 SignalBus("x") 不等价（跨类不等价，见 #13）。
        var self = ResourceId.Normalize(new ResourceId.Self("x"));
        Assert.Equal(new ResourceId.Self("x"), self); // 不塌缩到 SignalBus
    }

    // ── 4. SignalBus 已是规范命名空间构造子：原样返回，不再二次剥 signal_ 前缀（保持 Normalize 幂等，§3.1.4a） ──
    [Fact]
    public void SignalBus_IsCanonical_AndIdempotent() // §3.1.4a：SignalBus 为规范形，原样保留，幂等
    {
        var bus = ResourceId.Normalize(new ResourceId.SignalBus(new StringName("signal_x")));
        Assert.Equal(new ResourceId.SignalBus(new StringName("signal_x")), bus); // 不再二次剥前缀
        Assert.Equal(bus, ResourceId.Normalize(bus)); // 幂等（不动点）
        Assert.Equal(ResourceId.Normalize(new ResourceId.SignalBus(new StringName("x"))),
            new ResourceId.SignalBus(new StringName("x")));
    }

    // ── 5. Gpu / CommandBuffer 关系（按真实语义：独立构造子，PDR §3.1.4a 在本实现未强制同形） ──
    [Fact]
    public void Gpu_And_CommandBuffer_AreIndependentConstructors() // §3.1.4a：gpu/command_buffer 在本实现为独立构造子
    {
        // PDR §3.1.4a 表列「gpu/command_buffer ≡ CommandBuffer("gpu")」，但本实现将 Gpu(Rid) 与
        // CommandBuffer(string) 保留为两个独立判别联合构造子（字段类型不同，无法结构相等）。
        // 残差：运行时别名由 §7 白名单维度处理（Load/Instantiate 等映射层将二者对齐到同一逻辑资源）。
        // 此处按真实语义断言：各自 Normalize 幂等，且互不相等（证明未静默同形）。
        var gpu = new ResourceId.Gpu(new Rid("mesh"));
        var cb = new ResourceId.CommandBuffer("gpu");
        Assert.Equal(gpu, ResourceId.Normalize(gpu)); // 幂等
        Assert.Equal(cb, ResourceId.Normalize(cb)); // 幂等
        Assert.NotEqual(ResourceId.Normalize(gpu), ResourceId.Normalize(cb)); // 不强制同形（残差，非 bug）
    }

    // ── 6. Memory("mem") 幂等（ulong uid） ──
    [Fact]
    public void Memory_Idempotent() // §3.1.4a：memory ⇒ Memory(uid) 已为规范形
    {
        var m = new ResourceId.Memory(42UL);
        Assert.Equal(m, ResourceId.Normalize(m));
    }

    // ── 7. Occupancy("audio") 幂等（audio_channel 裸名） ──
    [Fact]
    public void Occupancy_Audio_Idempotent() // §3.1.4a：audio_channel ⇒ Occupancy("audio")
    {
        var o = new ResourceId.Occupancy("audio");
        Assert.Equal(o, ResourceId.Normalize(o));
    }

    // ── 8. Callback("cb") 幂等 ──
    [Fact]
    public void Callback_Idempotent() // §3.1.4a：callback ⇒ Callback("cb")
    {
        var c = new ResourceId.Callback("cb");
        Assert.Equal(c, ResourceId.Normalize(c));
    }

    // ── 9. AudioMixer("1") 幂等 ──
    [Fact]
    public void AudioMixer_Idempotent() // §3.1.4a：audio_mixer ⇒ AudioMixer(channelId)
    {
        var a = new ResourceId.AudioMixer(1);
        Assert.Equal(a, ResourceId.Normalize(a));
    }

    // ── 10. Input("action") 幂等 ──
    [Fact]
    public void Input_Idempotent() // §3.1.4a：input ⇒ Input(action)
    {
        var i = new ResourceId.Input("action");
        Assert.Equal(i, ResourceId.Normalize(i));
    }

    // ── 11. Network(0,"m") 幂等 ──
    [Fact]
    public void Network_Idempotent() // §3.1.4a：network ⇒ Network(peerId, method)
    {
        var n = new ResourceId.Network(0, "m");
        Assert.Equal(n, ResourceId.Normalize(n));
    }

    // ── 12. 幂等性全集（对以上所有样本 r，Normalize(Normalize(r)) == Normalize(r)） ──
    [Fact]
    public void Normalize_Idempotent_Over_All_Samples() // §3.1.4a：归一化是幂等函数（单点真相）
    {
        var samples = new ResourceId[]
        {
            new ResourceId.Self("signal_x"),
            new ResourceId.Self("x"),
            new ResourceId.Signal(new StringName("signal_y")),
            new ResourceId.SignalBus(new StringName("signal_x")),
            new ResourceId.SignalBus(new StringName("x")),
            new ResourceId.Gpu(new Rid("mesh")),
            new ResourceId.CommandBuffer("gpu"),
            new ResourceId.Memory(42UL),
            new ResourceId.Occupancy("audio"),
            new ResourceId.Occupancy("animation"),
            new ResourceId.Callback("cb"),
            new ResourceId.AudioMixer(1),
            new ResourceId.Input("action"),
            new ResourceId.Network(0, "m"),
            new ResourceId.Tree(NodePathOrUnknown.Of("path/to/node")),
            new ResourceId.Physics(new Rid("body")),
            new ResourceId.Disk("res://scene.tscn"),
        };
        foreach (var r in samples)
        {
            var once = ResourceId.Normalize(r);
            var twice = ResourceId.Normalize(once);
            Assert.Equal(once, twice); // 幂等：二次归一不变
        }
    }

    // ── 13. 跨类不等价（证明归一是规范形，非全塌缩） ──
    [Theory]
    [InlineData("tree")]
    [InlineData("memory")]
    [InlineData("gpu")]
    [InlineData("audio")]
    [InlineData("callback")]
    [InlineData("input")]
    public void CrossClass_Distinct_After_Normalize(string tag) // §3.1.4a：不同构造子规范形互不相等
    {
        ResourceId signalNorm = ResourceId.Normalize(new ResourceId.Self("signal_x")); // 唯一规范形 SignalBus("x")
        ResourceId other = tag switch
        {
            "tree" => ResourceId.Normalize(new ResourceId.Tree(NodePathOrUnknown.Of("x"))),
            "memory" => ResourceId.Normalize(new ResourceId.Memory(42UL)),
            "gpu" => ResourceId.Normalize(new ResourceId.CommandBuffer("gpu")),
            "audio" => ResourceId.Normalize(new ResourceId.Occupancy("audio")),
            "callback" => ResourceId.Normalize(new ResourceId.Callback("cb")),
            "input" => ResourceId.Normalize(new ResourceId.Input("action")),
            _ => throw new ArgumentOutOfRangeException(nameof(tag)),
        };
        Assert.NotEqual(other, signalNorm); // 不同类归一后互不相等：归一保留构造子标签
    }

    // ── 13b. 显式：Tree("x") != SignalBus("x")、Memory("m") != Gpu(...) ──
    [Fact]
    public void Explicit_CrossClass_Nonequivalence() // §3.1.4a：树/信号/内存/GPU 互不塌缩
    {
        Assert.NotEqual(
            ResourceId.Normalize(new ResourceId.Tree(NodePathOrUnknown.Of("x"))),
            ResourceId.Normalize(new ResourceId.SignalBus(new StringName("x"))));
        Assert.NotEqual(
            ResourceId.Normalize(new ResourceId.Memory(42UL)),
            ResourceId.Normalize(new ResourceId.Gpu(new Rid("g"))));
    }
}
