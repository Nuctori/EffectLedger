// formal/CosmosSweepLine.dfy — QED-P3-D4a 扫换线等价性的阶跃函数核心
// 被建模对象：EffectScript.Audit 的扫换线语义基础——存活集与净额作为时间的**分段常量函数**，
// 只在事件端点（lo/hi）处变化。由此可得「在全部端点处审计 ≡ 在所有时刻审计」（采样充分性）。
// 事件模型：有限寿命 [lo, hi]（端点含闭：lo ≤ t ≤ hi 存活，与 C# Alive 一致）；
// 贡献在事件自身 lo 处入账（create/move/use 为正、release 为负——C# 闭包/扫换线同口径：
// net(t) = Σ_{lo ≤ t} contrib，纯阶跃，只在 lo 端点跳变）。
// 本文件只承载 D4a：恒定性引理。完整三 gates 覆盖定理（peak/conflict 的超集保守性）归 D4b，
// 扫换线增量维护 == 阶跃定义的算法等价归 D4c，C# 实现对照 + CI 归 D4d/D5。
include "CosmosEffectAlgebra.dfy"

module SweepLineModel
{
  import opened CosmosEffectAlgebra // D2 的 Mode/Compatible 单一真源复用
  // 事件：寿命 [lo, hi] + 带符号净贡献（net gate，lo 处入账）+ 峰值权重 w（peak gate，
  // 对应 size.hi×ω ≥ 0）+ 资源身份 r 与模式 m（conflict gate 的配对键与判定）
  datatype Ev = Ev(lo: nat, hi: nat, contrib: int, w: nat, r: nat, m: Mode)
  {
    predicate Valid()
    {
      lo <= hi
    }

    predicate AliveAt(t: nat)
    {
      lo <= t <= hi
    }
  }

  // 时刻 t 的存活事件集
  function AliveSet(evs: seq<Ev>, t: nat): set<Ev>
  {
    set e | e in evs && e.AliveAt(t)
  }

  // 时刻 t 的累积净额：Σ_{lo(e) ≤ t} contrib(e)（按序列归纳定义）
  function NetAt(evs: seq<Ev>, t: nat): int
    decreases |evs|
  {
    if |evs| == 0 then
      0
    else
      (NetAt(evs[..|evs|-1], t)
       + (if evs[|evs|-1].lo <= t then evs[|evs|-1].contrib else 0))
  }

  // ── 单事件存活一致性：[u, v] 上无该事件的存活边界跨越（hi 不在 [u,v)、lo 不在 (u,v]）
  //    ⇒ 两时刻存活判定相同。 ──
  lemma AliveAtAgree(e: Ev, u: nat, v: nat)
    requires u <= v
    requires !(u <= e.hi < v)   // 事件不在 u 活、v 死（hi ∈ [u,v) 会跨界）
    requires !(u < e.lo <= v)   // 事件不在 u 死、v 活（lo ∈ (u,v] 会跨界）
    ensures e.AliveAt(u) == e.AliveAt(v)
  {
    if e.hi < v
    {
      assert e.hi < u; // 由 !(u <= hi < v) 与 hi < v ⇒ 两时刻 hi 都越上界 ⇒ 存活判定同假
    }
    if e.lo > u
    {
      assert e.lo > v; // 由 !(u < lo <= v) 与 lo > u ⇒ 两时刻 lo 都越下界 ⇒ 存活判定同假
    }
  }

  // ── 存活集恒定性（D4a 核心 1）：[u, v] 上无任何事件端点跨越 ⇒ 存活集相同。 ──
  lemma AliveSetAgree(evs: seq<Ev>, u: nat, v: nat)
    requires u <= v
    requires forall e | e in evs :: !(u <= e.hi < v) && !(u < e.lo <= v)
    ensures AliveSet(evs, u) == AliveSet(evs, v)
  {
    forall e | e in evs
      ensures e.AliveAt(u) == e.AliveAt(v)
    {
      AliveAtAgree(e, u, v);
    }
    // 集合外延性：逐元素等价 ⇒ 集合相等（Dafny 4 自动）
  }

  // ── 净额恒定性（D4a 核心 2）：(u, v] 上无事件 lo 端点 ⇒ 累积净额相同——
  //    net 只在 lo 处跳变（release 的负贡献同样在自身 lo 入账），故段内任意时刻
  //    的 net 与段首样本一致：NegativeDip 在段首采样处必然可见（不漏报）。 ──
  // 前缀切片元素必属全序列（NetAtAgree 归纳的桥接引理）
  lemma PrefixElementIn(evs: seq<Ev>, k: nat, e: Ev)
    requires k <= |evs| && e in evs[..k]
    ensures e in evs
  {
    var i :| 0 <= i < k && evs[i] == e;
    assert evs[i] == e;
  }

  lemma NetAtAgree(evs: seq<Ev>, u: nat, v: nat)
    requires u <= v
    requires forall e | e in evs :: !(u < e.lo <= v)
    ensures NetAt(evs, u) == NetAt(evs, v)
  {
    if |evs| == 0
    {
    }
    else
    {
      var rest := evs[..|evs|-1];
      var last := evs[|evs|-1];
      // 归纳前提对 rest 成立（(u,v] 上无 rest 的 lo 端点）
      forall e | e in rest
        ensures !(u < e.lo <= v)
      {
        PrefixElementIn(evs, |evs|-1, e); // 切片元素必属全序列
      }
      NetAtAgree(rest, u, v);
      // 末事件贡献位相同：lo ≤ u 或 lo > v ⇒ 两时刻贡献一致
    }
  }

  // ── 采样充分性·存活集半边（D4a 核心 3）：(a, u] 上无端点 ⇒ 时刻 u 的存活集 ⊆ 样本 a 处的存活集。
  //    峰值/冲突 gate 在样本 a 以「超集」保守评估——段内任何违例必在 a 处可见（不漏报）。
  //    （u 自身是端点时 entering 事件并入 u 的采样，覆盖由下一采样承载。） ──
  lemma AliveSupersetOnSegment(evs: seq<Ev>, a: nat, u: nat)
    requires a <= u
    requires forall e | e in evs :: !(a < e.lo <= u) && !(a < e.hi <= u)
    ensures AliveSet(evs, u) <= AliveSet(evs, a)
  {
    forall e | e in AliveSet(evs, u)
      ensures e in AliveSet(evs, a)
    {
      // e.AliveAt(u)：lo ≤ u ≤ hi；(a, u] 无端点 ⇒ lo ≤ a（否则 lo ∈ (a,u]）且 hi ≥ u > a（hi ∈ (a,u] 才可能 < u）
      assert !(a < e.lo <= u);
      assert !(a < e.hi <= u);
      assert e.lo <= a;
      assert e.hi >= u;
      assert e.AliveAt(a);
    }
  }

  // ── 采样充分性·净额半边（D4a 核心 4）：(a, u] 上无 lo 端点 ⇒ 样本 a 处的净额 == 时刻 u 的净额——
  //    NegativeDip gate 在样本 a 以「精确值」评估（非保守近似），段内任何负陷必在 a 处可见。 ──
  lemma NetAtSampleCoversSegment(evs: seq<Ev>, a: nat, u: nat)
    requires a <= u
    requires forall e | e in evs :: !(a < e.lo <= u)
    ensures NetAt(evs, a) == NetAt(evs, u)
  {
    NetAtAgree(evs, a, u);
  }

  // ── QED-P3-D4b — 三 gates 的段覆盖定理（采样充分性的 gate 级完整化）──
  // 段 (a, u] 内无任何事件端点 ⇒ 该段内任意时刻 u 的三类违例都在样本 a 处（保守或精确地）可见：
  //   gate(1) NegativeDip：NetAt(a) == NetAt(u)（精确，D4a NetAtAgree 推论）；
  //   gate(2) PeakExceeded：PeakAt(u) ≤ PeakAt(a)（超集保守——存活集 ⊆ 样本存活集，权重 ≥ 0）；
  //   gate(3) CompatibleConflict：冲突对存在性见证从 u 迁移到 a（存活超集承载同一对见证）。
  // 三者合成：任一时刻的三类违例 ⇒ 对应段首样本处同现违例 ⇒「全端点采样不漏报任何时刻违例」。

  // 时刻 t 的峰值占用：Σ_{alive(t)} w（按序列归纳，镜像 NetAt）
  function PeakAt(evs: seq<Ev>, t: nat): nat
    decreases |evs|
  {
    if |evs| == 0 then
      0
    else
      (PeakAt(evs[..|evs|-1], t)
       + (if evs[|evs|-1].AliveAt(t) then evs[|evs|-1].w else 0))
  }

  // 段内无端点 ⇒ 存活于 u 的事件必存活于样本 a（gate2/3 共用的迁移引理，D4a AliveSuperset 的逐事件形态）
  lemma AliveAtTransfers(evs: seq<Ev>, a: nat, u: nat, e: Ev)
    requires a <= u && e in evs
    requires forall x | x in evs :: !(a < x.lo <= u) && !(a < x.hi <= u)
    requires e.AliveAt(u)
    ensures e.AliveAt(a)
  {
    assert !(a < e.lo <= u);
    assert !(a < e.hi <= u);
    assert e.lo <= a;
    assert e.hi >= u;
  }

  // gate(1)：段内负陷在样本处精确可见
  lemma SegmentNetCovered(evs: seq<Ev>, a: nat, u: nat)
    requires a <= u
    requires forall x | x in evs :: !(a < x.lo <= u)
    ensures NetAt(evs, a) == NetAt(evs, u)
  {
    NetAtAgree(evs, a, u);
  }

  // gate(2)：段内峰值 ≤ 样本处峰值（权重 ≥ 0 + 存活迁移 ⇒ 超集和单调）
  lemma SegmentPeakCovered(evs: seq<Ev>, a: nat, u: nat)
    requires a <= u
    requires forall x | x in evs :: !(a < x.lo <= u) && !(a < x.hi <= u)
    ensures PeakAt(evs, u) <= PeakAt(evs, a)
    decreases |evs|
  {
    if |evs| == 0
    {
    }
    else
    {
      var rest := evs[..|evs|-1];
      var last := evs[|evs|-1];
      forall x | x in rest
        ensures !(a < x.lo <= u) && !(a < x.hi <= u)
      {
        PrefixElementIn(evs, |evs|-1, x);
      }
      SegmentPeakCovered(rest, a, u);
      if last.AliveAt(u)
      {
        AliveAtTransfers(evs, a, u, last);
        // alive(u) ⇒ alive(a)：两侧各加 w（≥0），不等式保持
        assert PeakAt(evs, u) <= PeakAt(evs, a);
      }
      else
      {
        // !alive(u)：左和不含 w ≤ 右和（alive(a) 侧加 ≥ 0）
        assert PeakAt(evs, u) <= PeakAt(evs, a);
      }
    }
  }

  // gate(3)：段内冲突对见证迁移到样本（存活超集承载同一对见证）
  predicate AliveConflictAt(evs: seq<Ev>, t: nat)
  {
    exists e1, e2 :: e1 in AliveSet(evs, t) && e2 in AliveSet(evs, t) && e1 != e2
        && e1.r == e2.r && InConflict(e1.m, e2.m)
  }

  lemma SegmentConflictCovered(evs: seq<Ev>, a: nat, u: nat)
    requires a <= u
    requires forall x | x in evs :: !(a < x.lo <= u) && !(a < x.hi <= u)
    ensures AliveConflictAt(evs, u) ==> AliveConflictAt(evs, a)
  {
    AliveSupersetOnSegment(evs, a, u);
    if AliveConflictAt(evs, u)
    {
      // 见证提取（AliveConflictAt 定义展开）+ 迁移：e1/e2 ∈ AliveSet(u) ⊆ AliveSet(a)（超集），
      // 配对条件（同资源 + 模式不兼容）与时刻无关
      assert exists e1, e2 :: (e1 in AliveSet(evs, u) && e2 in AliveSet(evs, u)
          && e1 != e2 && e1.r == e2.r && InConflict(e1.m, e2.m));
      var e1, e2 :| e1 in AliveSet(evs, u) && e2 in AliveSet(evs, u)
          && e1 != e2 && e1.r == e2.r && InConflict(e1.m, e2.m);
      assert e1 in AliveSet(evs, a) && e2 in AliveSet(evs, a);
      assert e1 != e2 && e1.r == e2.r && InConflict(e1.m, e2.m);
    }
  }

  // ── D4b 总纲：段 (a, u] 内任意时刻 u 的三类违例 ⇒ 样本 a 处同现（gate1 精确 / gate2 保守 / gate3 见证迁移）──
  lemma SegmentViolationsCovered(evs: seq<Ev>, a: nat, u: nat)
    requires a <= u
    requires forall x | x in evs :: !(a < x.lo <= u) && !(a < x.hi <= u)
    ensures NetAt(evs, a) == NetAt(evs, u)
    ensures PeakAt(evs, u) <= PeakAt(evs, a)
    ensures AliveConflictAt(evs, u) ==> AliveConflictAt(evs, a)
  {
    SegmentNetCovered(evs, a, u);
    SegmentPeakCovered(evs, a, u);
    SegmentConflictCovered(evs, a, u);
  }

  // ── QED-P3-D4c — 扫换线增量维护 == 阶跃定义的算法等价（net 维度）──
  // C# 扫换线按事件逐条增量累加（enter 时加贡献）；D4a 的 NetAt 是「lo ≤ t 全量求和」的定义式。
  // 三条增量定律证明两者逐事件等价——增量累加（算法）与定义和（语义）互为充要形态。

  // 增量律 1（enter 入账）：新增事件 e 且 e.lo ≤ t ⇒ 净额恰好增加 e.contrib——
  // 这就是扫换线 enter 处理器的数学内容（加法精确、无遗漏无重复）。
  lemma NetAtAddEventEnter(evs: seq<Ev>, e: Ev, t: nat)
    requires e.lo <= t
    ensures NetAt(evs + [e], t) == NetAt(evs, t) + e.contrib
  {
    calc {
      NetAt(evs + [e], t);
      NetAt((evs + [e])[..|evs|], t) + (if (evs + [e])[|evs|].lo <= t then (evs + [e])[|evs|].contrib else 0);
      NetAt(evs, t) + e.contrib;
    }
  }

  // 增量律 2（未入账）：新增事件 e 且 e.lo > t ⇒ 净额不变（未来事件不影响当前净额）。
  lemma NetAtAddEventFuture(evs: seq<Ev>, e: Ev, t: nat)
    requires e.lo > t
    ensures NetAt(evs + [e], t) == NetAt(evs, t)
  {
    calc {
      NetAt(evs + [e], t);
      NetAt((evs + [e])[..|evs|], t) + (if (evs + [e])[|evs|].lo <= t then (evs + [e])[|evs|].contrib else 0);
      NetAt(evs, t) + 0;
    }
  }

  // 增量律 3（存活集同步）：扫换线同时维护的存活集也按同一事件增删——
  // enter 并入 {e}（或忽略），与峰值/冲突 gate 的增量维护同构（gate2/3 的算法面）。
  lemma AliveSetAddEvent(evs: seq<Ev>, e: Ev, t: nat)
    ensures AliveSet(evs + [e], t)
         == (if e.AliveAt(t) then AliveSet(evs, t) + {e} else AliveSet(evs, t))
  {
    assert forall x :: x in AliveSet(evs + [e], t) <==>
      (x in evs && x.AliveAt(t)) || (x == e && e.AliveAt(t));
  }

  // 前缀累加（扫换线的增量形态）与定义和（NetAt 的全量形态）逐前缀一致——
  // 「增量累加（算法）== 定义求和（语义）」的直接等价定理。
  function SweepAcc(evs: seq<Ev>, k: nat, t: nat): int
    requires k <= |evs|
    decreases k
  {
    if k == 0 then 0
    else SweepAcc(evs, k-1, t) + (if evs[k-1].lo <= t then evs[k-1].contrib else 0)
  }

  lemma SweepAccMatchesNetAt(evs: seq<Ev>, k: nat, t: nat)
    requires k <= |evs|
    ensures SweepAcc(evs, k, t) == NetAt(evs[..k], t)
    decreases k
  {
    if k == 0
    {
      assert evs[..0] == [];
    }
    else
    {
      SweepAccMatchesNetAt(evs, k-1, t);
      assert (evs[..k])[..k-1] == evs[..k-1];           // 前缀的前缀仍为前缀
      assert evs[..k][k-1] == evs[k-1];                 // 前缀末元素 == 原序列第 k 项
      assert (evs[..k])[|evs[..k]|-1] == evs[k-1];      // 同上（长度视角）
    }
  }

  // 总等价：处理全部事件的增量累加 == 定义和（扫换线终态 == 逐点定义在任意 t 处的值）
  lemma SweepTotalMatchesNetAt(evs: seq<Ev>, t: nat)
    ensures SweepAcc(evs, |evs|, t) == NetAt(evs, t)
  {
    SweepAccMatchesNetAt(evs, |evs|, t);
    assert evs[..|evs|] == evs; // 全前缀切片 == 原序列
  }
}
