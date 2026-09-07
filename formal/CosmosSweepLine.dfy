// formal/CosmosSweepLine.dfy — QED-P3-D4a 扫换线等价性的阶跃函数核心
// 被建模对象：EffectScript.Audit 的扫换线语义基础——存活集与净额作为时间的**分段常量函数**，
// 只在事件端点（lo/hi）处变化。由此可得「在全部端点处审计 ≡ 在所有时刻审计」（采样充分性）。
// 事件模型：有限寿命 [lo, hi]（端点含闭：lo ≤ t ≤ hi 存活，与 C# Alive 一致）；
// 贡献在事件自身 lo 处入账（create/move/use 为正、release 为负——C# 闭包/扫换线同口径：
// net(t) = Σ_{lo ≤ t} contrib，纯阶跃，只在 lo 端点跳变）。
// 本文件只承载 D4a：恒定性引理。完整三 gates 覆盖定理（peak/conflict 的超集保守性）归 D4b，
// 扫换线增量维护 == 阶跃定义的算法等价归 D4c，C# 实现对照 + CI 归 D4d/D5。
module SweepLineModel
{
  // 事件：寿命 [lo, hi] + 带符号净贡献（在 lo 处入账）
  datatype Ev = Ev(lo: nat, hi: nat, contrib: int)
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
}
