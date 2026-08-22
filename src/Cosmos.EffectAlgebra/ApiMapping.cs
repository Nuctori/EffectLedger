// ApiMapping.cs — PDR §7（§7.1–§7.10）实现：Godot API → Claim 白名单；§8.1 release-class。LANDING_PLAN §3：L1 数据层（零 Godot）。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra;

// 迭代03：§7 Godot API ↔ Claim 白名单 + §8.1 release-class 数据层。
// L1 零 Godot 依赖：API 名用字符串、resource 用 ResourceId 构造子、scope 用 ScopeId.Shell()/Global() 表示
// shell_scope / global_scope。裸名（gpu/memory/command_buffer/signal_bus/...）按 §3.1.4a 归一映射
// （运行时由 ResourceId.Normalize 完成，白名单不重复写死）。
// 每条映射带来源 §7.x 注释；类型字段即边界。

/// <summary>§7 — 单条 API 映射：Godot 方法名 + 其 Claim 集合（按 L1 类型建模）。</summary>
public readonly record struct ApiMapping
{
    /// <summary>§7 — 稳定 API 键（Godot 方法名；同名异义以 "Audio.Play" / "Anim.Play" 消歧）。</summary>
    public string GodotApi { get; }

    /// <summary>§7 — 该 API 引发的 Claim 集合（已 Normalize，§3.1.4a）。</summary>
    public ImmutableArray<Claim> Claims { get; }

    /// <summary>§7 — 构造单条 API 映射（API 名 + Claim 集合）。</summary>
    public ApiMapping(string godotApi, ImmutableArray<Claim> claims)
    {
        GodotApi = godotApi;
        Claims = claims;
    }
}

/// <summary>§7.1–§7.10 全表白名单（强类型数据；类型即边界，来源注释承载数学语义）。</summary>
public static class GodotApiWhitelist
{
    // ── 作用域/资源/Claim 构造帮助器（保持编码紧凑、与 §7 表逐格对齐）──
    static ScopeId Shell() => new ScopeId.Shell();
    static ScopeId Global() => new ScopeId.Global();

    static ResourceId Tree(string p) => new ResourceId.Tree(NodePathOrUnknown.Of(p));
    static ResourceId Self(string c) => new ResourceId.Self(c);
    static ResourceId Phys(string b) => new ResourceId.Physics(new Rid(b));
    static ResourceId Mem() => new ResourceId.Memory(0); // 通用内存资源哨兵；真实 UID 由映射层运行时填入（§3.1.4a）
    static ResourceId Disk(string p) => new ResourceId.Disk(p);
    static ResourceId SigBus(string n) => new ResourceId.SignalBus(new StringName(n));
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ResourceId CmdBuf() => new ResourceId.CommandBuffer("gpu");   // §7 裸 command_buffer
    static ResourceId AudioMx() => new ResourceId.AudioMixer(0);          // 通用音频混音通道哨兵
    static ResourceId Occ(string ch) => new ResourceId.Occupancy(ch);    // audio_channel / animation_state
    static ResourceId Cb() => new ResourceId.Callback("cb");             // §7 Connect callback
    static ResourceId Net(int peer, string m) => new ResourceId.Network(peer, m);
    static ResourceId Inp(string a) => new ResourceId.Input(a);          // §7.8 input

    static Claim Rd(ResourceId r, Mode m, ScopeId s, Interval sz = default) => new Claim(Kind.Read, r, m, s, sz).Normalize();
    static Claim Wr(ResourceId r, Mode m, ScopeId s, Interval sz = default) => new Claim(Kind.Write, r, m, s, sz).Normalize();
    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz = default) => new Claim(Kind.Occupy, r, m, s, sz).Normalize();

    static ApiMapping M(string api, params Claim[] claims) => new(api, claims.ToImmutableArray());

    /// <summary>§7.1–§7.10 白名单（逐条对应 PDR 映射表）。</summary>
    public static ImmutableArray<ApiMapping> All { get; } = Build();

    static ImmutableArray<ApiMapping> Build()
    {
        var items = new System.Collections.Generic.List<ApiMapping>();

        // §7.1 场景树操作
        items.Add(M("GetNode", Rd(Tree("path"), Mode.Use, Shell())));                              // §7.1 读场景树
        items.Add(M("GetTree", Rd(Tree("root"), Mode.Use, Shell())));                              // §7.1 读场景树根
        items.Add(M("AddChild",                                                                            // §7.1 写树 + 占用
            Wr(Tree("node.id"), Mode.Create, Shell()),
            Oc(Tree("node.id"), Mode.Create, Shell(), Interval.Exact(1))));
        items.Add(M("RemoveChild",                                                                         // §7.1 写树 + 释放
            Wr(Tree("node.id"), Mode.Release, Shell()),
            Oc(Tree("node.id"), Mode.Release, Shell(), Interval.Exact(1))));
        items.Add(M("QueueFree",                                                                           // §7.1 mode=release（iter27：net 计入 −size）
            Wr(Tree("node.id"), Mode.Release, Shell()),                                              // 与 AddChild 的 Wr(Tree node.id, Create) 对称
            Oc(Tree("node.id"), Mode.Release, Shell(), Interval.Exact(1)),                          // 与 AddChild 的 Oc(Tree node.id, Create, Exact1) 对称
            Oc(Mem(), Mode.Release, Shell(), Interval.Dynamic)));
        items.Add(M("MoveChild", Wr(Tree("node.id"), Mode.Use, Shell())));                            // §7.1 写树

        // §7.2 属性访问
        items.Add(M("Position.get", Rd(Self("transform"), Mode.Use, Shell())));                       // §7.2 读自身
        items.Add(M("Position.set", Wr(Self("transform"), Mode.Use, Shell())));                       // §7.2 写自身
        items.Add(M("GlobalPosition.get",                                                                  // §7.2 读自身 + 父节点
            Rd(Self("transform"), Mode.Use, Shell()),
            Rd(Tree("parent_path"), Mode.Use, Shell())));
        items.Add(M("Rotation.getset",                                                                     // §7.2 读写自身
            Rd(Self("transform"), Mode.Use, Shell()),
            Wr(Self("transform"), Mode.Use, Shell())));
        items.Add(M("Scale.getset",                                                                        // §7.2 读写自身
            Rd(Self("transform"), Mode.Use, Shell()),
            Wr(Self("transform"), Mode.Use, Shell())));

        // §7.3 物理操作
        items.Add(M("MoveAndSlide",                                                                        // §7.3 读写物理 + 读碰撞
            Rd(Phys("self.body_id"), Mode.Use, Shell()),
            Wr(Phys("self.body_id"), Mode.Use, Shell()),
            Rd(Tree("collision_shapes"), Mode.Use, Shell())));
        items.Add(M("ApplyForce", Wr(Phys("self.body_id"), Mode.Use, Shell())));                       // §7.3 写物理
        items.Add(M("ApplyImpulse", Wr(Phys("self.body_id"), Mode.Use, Shell())));                     // §7.3 写物理
        items.Add(M("GetSlideCollisionCount", Rd(Phys("self.body_id"), Mode.Use, Shell())));           // §7.3 读物理
        items.Add(M("GetSlideCollision", Rd(Phys("self.body_id"), Mode.Use, Shell())));               // §7.3 读物理

        // §7.4 资源加载
        items.Add(M("Load",                                                                                // §7.4 读磁盘 + 占用内存(global)
            Rd(Disk("path"), Mode.Use, Shell()),
            Oc(Mem(), Mode.Create, Global(), Interval.Dynamic)));
        items.Add(M("LoadInteractive", Rd(Disk("path"), Mode.Use, Shell())));                           // §7.4 读磁盘
        items.Add(M("Instantiate",                                                                         // §7.4 读场景 + 创建 + 占用
            Rd(Mem(), Mode.Use, Shell()),
            Wr(Tree("new_id"), Mode.Create, Shell()),
            Oc(Mem(), Mode.Create, Shell(), Interval.Dynamic)));
        items.Add(M("Preload",                                                                             // §7.4 读磁盘 + 占用内存(global)
            Rd(Disk("path"), Mode.Use, Shell()),
            Oc(Mem(), Mode.Create, Global(), Interval.Dynamic)));

        // §7.5 信号系统
        items.Add(M("EmitSignal",                                                                          // §7.5 写信号 + 读订阅者
            Wr(SigBus("signal"), Mode.Create, Shell()),
            Rd(Tree("subscribers_signal"), Mode.Use, Shell())));
        items.Add(M("Connect",                                                                             // §7.5 写信号(self.signal_*) + 占用回调
            Wr(Self("signal_x"), Mode.Create, Shell()),
            Oc(Cb(), Mode.Create, Shell(), Interval.Dynamic)));
        items.Add(M("Disconnect",                                                                          // §7.5 写信号 + 释放回调
            Wr(Self("signal_x"), Mode.Release, Shell()),
            Oc(Cb(), Mode.Release, Shell(), Interval.Dynamic)));
        items.Add(M("IsConnected", Rd(Self("signal_x"), Mode.Use, Shell())));                            // §7.5 读信号

        // §7.6 渲染操作
        items.Add(M("DrawMesh",                                                                            // §7.6 读 GPU + 写命令 + 读材质
            Rd(Gpu("mesh"), Mode.Use, Shell()),
            Wr(CmdBuf(), Mode.Create, Shell()),
            Rd(Gpu("material"), Mode.Use, Shell())));
        items.Add(M("DrawRect", Wr(CmdBuf(), Mode.Create, Shell())));                                    // §7.6 写命令
        items.Add(M("SetMaterialOverride",                                                                 // §7.6 写材质 + 读材质
            Wr(Self("material"), Mode.Use, Shell()),
            Rd(Gpu("material"), Mode.Use, Shell())));

        // §7.7 音频操作
        items.Add(M("Audio.Play",                                                                          // §7.7 写混音器 + 读音频 + 占用通道
            Wr(AudioMx(), Mode.Create, Shell()),
            Rd(Mem(), Mode.Use, Shell()),
            Oc(Occ("audio"), Mode.Create, Shell(), Interval.Exact(1))));
        items.Add(M("Audio.Stop",                                                                          // §7.7 写混音器 + 释放通道
            Wr(AudioMx(), Mode.Release, Shell()),
            Oc(Occ("audio"), Mode.Release, Shell(), Interval.Exact(1))));
        items.Add(M("Audio.SetVolumeDb", Wr(AudioMx(), Mode.Use, Shell())));                             // §7.7 写混音器

        // §7.8 输入操作
        items.Add(M("IsActionPressed", Rd(Inp("action"), Mode.Use, Shell())));                           // §7.8 读输入
        items.Add(M("IsActionJustPressed", Rd(Inp("action"), Mode.Use, Shell())));                       // §7.8 读输入
        items.Add(M("GetMousePosition", Rd(Inp("mouse"), Mode.Use, Shell())));                           // §7.8 读输入

        // §7.9 网络操作
        items.Add(M("Rpc",                                                                                 // §7.9 写网络 + 读内存
            Wr(Net(0, "method"), Mode.Create, Shell()),
            Rd(Mem(), Mode.Use, Shell())));
        items.Add(M("RpcId",                                                                               // §7.9 写网络 + 读内存
            Wr(Net(0, "method"), Mode.Create, Shell()),
            Rd(Mem(), Mode.Use, Shell())));

        // §7.10 动画操作
        items.Add(M("Anim.Play",                                                                           // §7.10 写动画 + 读内存 + 占用状态
            Wr(Self("animation"), Mode.Create, Shell()),
            Rd(Mem(), Mode.Use, Shell()),
            Oc(Occ("animation"), Mode.Create, Shell(), Interval.Exact(1))));
        items.Add(M("Anim.Stop",                                                                           // §7.10 写动画 + 释放状态
            Wr(Self("animation"), Mode.Release, Shell()),
            Oc(Occ("animation"), Mode.Release, Shell(), Interval.Exact(1))));
        items.Add(M("Anim.Seek", Wr(Self("animation"), Mode.Use, Shell())));                             // §7.10 写动画

        return items.ToImmutableArray();
    }
}

/// <summary>
/// §8.1 — release-class 白名单（强制 emit release/occupy-release，不落默认 Unknown 规则）。
/// 来源：Godot 开源源码核对（scene/main/node.cpp + Object），2026-08-20 实测枚举：
/// queue_free / Object.free 递归释放 children；remove_child 释放 tree 占用；
/// disconnect 释放信号/回调占用；remove_from_group 释放 group 成员占用；
/// cancel_free 取消挂起 queue_free；free_children_in_group 批量释放组内 child 占用。
/// </summary>
public static class ReleaseClass
{
    /// <summary>§8.1 权威 release-class 清单（7 个，与 PDR 严格一致）。</summary>
    public static ImmutableHashSet<string> Names { get; } = ImmutableHashSet.Create(
        "queue_free", "free", "remove_child", "disconnect", "remove_from_group", "cancel_free", "free_children_in_group");

    /// <summary>§8.1 — 判定 API（小写）是否属于 release-class；是则映射层必须 emit release/occupy-release。</summary>
    public static bool IsRelease(string api) => !string.IsNullOrEmpty(api) && Names.Contains(api.ToLowerInvariant());

    /// <summary>§8.1 — 全部 release-class 名（不可变数组视图）。</summary>
    public static ImmutableArray<string> All => Names.ToImmutableArray();
}
