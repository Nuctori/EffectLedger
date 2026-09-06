// formal/CosmosEffectAlgebra.dfy — QED-P3-D1 形式化最小切片（选型 Dafny 4.11）
// 被建模对象：L1 纯代数核心 Numeric.cs 的 NatStar（ℕ* = ℕ ∪ {⊤}，溢出⇒⊤ 保守闭合）
// 与 Interval（[lo,hi]，lo≤hi 不变量、Default[1,1]、Merge join-semilattice）。
// 选型理由（ROADMAP D1）：Dafny 语义贴近 C#（纯函数 + 前置/后置条件），算术模型可直接表达
// ulong 溢出⇒⊤ 的保守闭合；Lean4 表达力更强但与 C# 无直接通道、建模范式成本高一个量级。
// 本文件是被验证的**可执行规约**：D5（实现对照）以本模型为 oracle 做性质测试；CI 接线归 D5。
// 验证：dafny verify --solver-path <z3-4.12.1> formal/CosmosEffectAlgebra.dfy ⇒ 0 错误。
module CosmosEffectAlgebra
{
  // C# ulong.MaxValue——NatStar 有限值的值域上界
  const ULONG_MAX: nat := 18446744073709551615

  // §3.1.5a — ℕ* = ℕ ∪ {⊤}；⊤ 为上界标记（非 IEEE ∞）
  datatype NatStar = Finite(n: nat) | Top
  {
    // 全序 ≤（⊤ 为最大元）：Finite ≤ Finite 按数值；Finite ≤ ⊤；⊤ ≤ ⊤；⊤ ≤ Finite 恒假
    predicate Le(other: NatStar)
    {
      (!Top? && !other.Top? && n <= other.n)
      || other.Top?
    }
  }

  // §3.1.5a 加法：⊤ 闭合；有限+有限 的数学和超 ulong 上界 ⇒ ⊤（保守，实现以环绕检测等价判定）
  function Add(a: NatStar, b: NatStar): NatStar
  {
    if a.Top? || b.Top? then Top
    else if a.n + b.n <= ULONG_MAX then Finite(a.n + b.n)
    else Top
  }

  // §3.1.5a 乘法：同上加法式保守闭合（数学积超上界 ⇒ ⊤）
  function Mul(a: NatStar, b: NatStar): NatStar
  {
    if a.Top? || b.Top? then Top
    else if a.n * b.n <= ULONG_MAX then Finite(a.n * b.n)
    else Top
  }

  // ── ⊤ 闭合律 ──
  lemma AddTopAbsorbing(a: NatStar)
    ensures Add(a, Top) == Top && Add(Top, a) == Top
  {
  }

  lemma MulTopAbsorbing(a: NatStar)
    ensures Mul(a, Top) == Top && Mul(Top, a) == Top
  {
  }

  // ── 交换律（含溢出分支的对称性） ──
  lemma AddCommutative(a: NatStar, b: NatStar)
    ensures Add(a, b) == Add(b, a)
  {
  }

  lemma MulCommutative(a: NatStar, b: NatStar)
    ensures Mul(a, b) == Mul(b, a)
  {
    if !a.Top? && !b.Top?
    {
      assert a.n * b.n == b.n * a.n; // 非线性提示
    }
  }

  // ── 溢出⇒⊤ 的直接形态（实现「环绕检测」的数学等价） ──
  lemma AddOverflowYieldsTop(x: nat, y: nat)
    requires x <= ULONG_MAX && y <= ULONG_MAX && x + y > ULONG_MAX
    ensures Add(Finite(x), Finite(y)) == Top
  {
  }

  lemma MulOverflowYieldsTop(x: nat, y: nat)
    requires x <= ULONG_MAX && y <= ULONG_MAX && x * y > ULONG_MAX
    ensures Mul(Finite(x), Finite(y)) == Top
  {
  }

  // ── 全序性（MinN/MaxN 律的前提） ──
  lemma LeTotal(a: NatStar, b: NatStar)
    ensures a.Le(b) || b.Le(a)
  {
  }

  lemma LeAntisymmetric(a: NatStar, b: NatStar)
    requires a.Le(b) && b.Le(a)
    ensures a == b
  {
  }

  lemma LeTransitive(a: NatStar, b: NatStar, c: NatStar)
    requires a.Le(b) && b.Le(c)
    ensures a.Le(c)
  {
  }

  function MinN(a: NatStar, b: NatStar): NatStar
  {
    if a.Le(b) then a else b
  }

  function MaxN(a: NatStar, b: NatStar): NatStar
  {
    if a.Le(b) then b else a
  }

  lemma MinNComm(a: NatStar, b: NatStar)
    ensures MinN(a, b) == MinN(b, a)
  {
    LeTotal(a, b);
  }

  lemma MaxNComm(a: NatStar, b: NatStar)
    ensures MaxN(a, b) == MaxN(b, a)
  {
    LeTotal(a, b);
  }

  lemma MinNAssoc(a: NatStar, b: NatStar, c: NatStar)
    ensures MinN(MinN(a, b), c) == MinN(a, MinN(b, c))
  {
    LeTotal(a, b);
    LeTotal(b, c);
    LeTotal(a, c);
  }

  lemma MaxNAssoc(a: NatStar, b: NatStar, c: NatStar)
    ensures MaxN(MaxN(a, b), c) == MaxN(a, MaxN(b, c))
  {
    LeTotal(a, b);
    LeTotal(b, c);
    LeTotal(a, c);
  }

  // §3.1.5b — 区间 [lo, hi]；不变量 lo ≤ hi（[⊤,⊤] 合法；[⊤, finite] 非法——与 C# 构造器一致）
  datatype Interval = Interval(lo: NatStar, hi: NatStar)
  {
    predicate Valid()
    {
      lo.Le(hi)
    }

    // §3.1.5b merge_I：[min(lo₁,lo₂), max(hi₁,hi₂)]（join-semilattice 的 join）
    function Merge(other: Interval): Interval
    {
      Interval(MinN(lo, other.lo), MaxN(hi, other.hi))
    }
  }

  // §3.1.5 — 缺省区间 [1,1]（「精确 1，既非未知 ⊤ 也非 0 预算」）
  const DefaultInterval := Interval(Finite(1), Finite(1))

  lemma DefaultIntervalValid()
    ensures DefaultInterval.Valid()
  {
  }

  // ── merge_I 的半格律（join：幂等/交换/结合）+ 不变量保持 ──
  lemma MergePreservesValid(a: Interval, b: Interval)
    requires a.Valid() && b.Valid()
    ensures (a.Merge(b)).Valid()
  {
    // 目标 MinN(a.lo, b.lo) ≤ MaxN(a.hi, b.hi)：分两侧各走一条 ≤ 链
    LeTotal(a.lo, b.lo);
    LeTotal(a.hi, b.hi);
    assert MinN(a.lo, b.lo).Le(a.lo) && MinN(a.lo, b.lo).Le(b.lo);
    assert a.hi.Le(MaxN(a.hi, b.hi)) && b.hi.Le(MaxN(a.hi, b.hi));
    if MinN(a.lo, b.lo) == a.lo
    {
      LeTransitive(a.lo, a.hi, MaxN(a.hi, b.hi));
      LeTransitive(MinN(a.lo, b.lo), a.lo, MaxN(a.hi, b.hi));
    }
    else
    {
      LeTransitive(b.lo, b.hi, MaxN(a.hi, b.hi));
      LeTransitive(MinN(a.lo, b.lo), b.lo, MaxN(a.hi, b.hi));
    }
  }

  lemma MergeIdempotent(x: Interval)
    ensures x.Merge(x) == x
  {
  }

  lemma MergeCommutative(a: Interval, b: Interval)
    ensures a.Merge(b) == b.Merge(a)
  {
    MinNComm(a.lo, b.lo);
    MaxNComm(a.hi, b.hi);
  }

  lemma MergeAssociative(a: Interval, b: Interval, c: Interval)
    ensures a.Merge(b).Merge(c) == a.Merge(b.Merge(c))
  {
    MinNAssoc(a.lo, b.lo, c.lo);
    MaxNAssoc(a.hi, b.hi, c.hi);
  }
}
