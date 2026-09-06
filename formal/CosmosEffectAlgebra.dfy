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

  // ═══ QED-P3-D2 — ScopeId ⊆* 偏序 + Compatible 全函数 + 对称律 ═══
  // 被建模对象：Objects.cs 的 ScopeId（8 构造子 + IncludedIn，§3.1.3b 单一真源）
  // 与 Algebra.cs 的 Compatible.IsCompatible（§3.2.3 全函数）。
  // C# 侧性质钉（ScopeOrderTests 8 标签穷举 / CompatibleMatrixTests 25 组合矩阵）在此
  // 升级为全称定理——随机采样钉覆盖有限实例，定理覆盖全域。

  // §3.1.3b — ScopeId 判别联合：契约面 4（Method/Type/Scene/Global）+ C# 超集 4
  // （Shell/Loop/Conditional/Async，P1-B4a 后 internal，模型不区分可见性——偏序律与可见性正交）。
  // datatype 自带结构相等 =「同构造子同字段」，即 C# record Equals 的数学对应物。
  datatype ScopeId = Method(name: string) | Type(name: string) | Scene(name: string)
                   | Global | Loop(id: string) | Conditional(branch: string)
                   | Async(id: string) | Shell
  {
    // §3.1.3b ⊑（ScopeId.IncludedIn 单一真源）：结构相等（自反）∨ other 为 Global。
    // 【QED-A6 单向包含定稿】无「Global ⊑ X」反向析取支；跨标签/同标签异名 ⇒ false（不可比较）。
    predicate Leq(other: ScopeId)
    {
      this == other || other.Global?
    }
  }

  // ── 偏序三律 + Global 唯一最大元（ScopeOrderTests 的全称升级）──
  lemma LeqReflexive(a: ScopeId)
    ensures a.Leq(a)
  {
  }

  lemma LeqAntisymmetric(a: ScopeId, b: ScopeId)
    requires a.Leq(b) && b.Leq(a)
    ensures a == b
  {
  }

  lemma LeqTransitive(a: ScopeId, b: ScopeId, c: ScopeId)
    requires a.Leq(b) && b.Leq(c)
    ensures a.Leq(c)
  {
  }

  // Global 唯一最大元：一切 X ⊑ Global；且 Global ⊑ X ⇒ X = Global
  // （反向不存在 = QED-A6 对 iter55 F4 双向包含违反反对称的收口）
  lemma GlobalUniqueMaximum(a: ScopeId)
    ensures a.Leq(Global) && (Global.Leq(a) ==> a == Global)
  {
  }

  // ⊑ 的可比对恰为 {(x,x)} ∪ {(x,Global)}——「按上表机械查表无未定义项、跨标签不可比」的全称形态
  lemma LeqExactlyReflexiveOrTop(a: ScopeId, b: ScopeId)
    ensures a.Leq(b) <==> (a == b || b.Global?)
  {
  }

  // §3.1.1/§3.2.3 — Mode 五值（C# enum Mode 的数学对应物；datatype 构造子穷举无未定义值）
  datatype Mode = Use | Create | Release | Move | Unknown

  // §3.2.3 P4【QED-A3 定稿】— Unknown 解析为 Use（最弱兼容，fail-open；
  // Algebra.cs Compatible.Resolve 单一真源）
  function Resolve(m: Mode): Mode
  {
    if m == Unknown then Use else m
  }

  // §3.2.3 — CONFLICT 集闭合式：解析后同为非 Use 的同一 mode
  // （= {(Create,Create),(Move,Move),(Release,Release)}；Use 对角与 Unknown→Use 不冲突）
  predicate InConflict(a: Mode, b: Mode)
  {
    var aa := Resolve(a);
    var bb := Resolve(b);
    aa == bb && aa != Use
  }

  // §3.2.3 — Compatible 全函数（正枚举形态，与 Algebra.cs IsCompatible 逐条对应；
  // Dafny 函数天然全定义 = P2「16+9 对全覆盖无未定义项」的构造事实）
  function IsCompatible(a: Mode, b: Mode): bool
  {
    var aa := Resolve(a);
    var bb := Resolve(b);
    aa == Use || bb == Use
    || (aa == Create && bb == Release) || (aa == Release && bb == Create)
    || (aa == Create && bb == Move)    || (aa == Move && bb == Create)
    || (aa == Release && bb == Move)   || (aa == Move && bb == Release)
  }

  // (P2) 闭包式：Compatible ⟺ ¬CONFLICT——PDR「闭合性可机械验证」的机器证明
  lemma CompatibleClosedForm(a: Mode, b: Mode)
    ensures IsCompatible(a, b) <==> !InConflict(a, b)
  {
  }

  // (P1) 对称律
  lemma CompatibleSymmetric(a: Mode, b: Mode)
    ensures IsCompatible(a, b) == IsCompatible(b, a)
  {
  }

  // (P4) Unknown ≡ Use（QED-A3 定稿的 Compatible 维契约；CompatibleMatrixTests 全 (Unknown,*) 行）
  lemma UnknownEqualsUse(a: Mode)
    ensures IsCompatible(Unknown, a) == IsCompatible(Use, a)
  {
  }

  // (P3) 良性生命周期配对：create+release 不误判冲突（iter23 修正的机器证明）
  lemma BenignLifecyclePairs()
    ensures IsCompatible(Create, Release) && IsCompatible(Release, Create)
  {
  }

  // CONFLICT 集恰为非 Use/Unknown 的对角三对（CompatibleMatrixTests 25 组合矩阵的全称升级）
  lemma ConflictIsDiagonal(a: Mode, b: Mode)
    ensures InConflict(a, b) <==> (a == b && a != Use && a != Unknown)
  {
  }

  // ═══ QED-P3-D3 — SignedNet 守恒律（区间含 0 ⇔ 守恒）═══
  // 被建模对象：SignedNet.cs 的 ZStar（ℤ* = ℤ ∪ {⊤}，溢出⇒⊤ 保守闭合 R4-F1）与
  // SignedInterval（[lo,hi]、ContainsZero fail-closed、Add 真求和），以及
  // Algebra.cs NetTable 的 release 取负 [-hi,-lo] 与 IsConserved（Missing/⊤ fail-closed）。
  // 无符号 size 复用 D1 的 NatStar/Interval 模型（create/move 贡献正号、release 贡献负号）。

  // C# long 值域（ZStar.Value:long 的数学边界）
  const LONG_MAX: int := 9223372036854775807
  const LONG_MIN: int := -9223372036854775808

  predicate ZInRange(v: int)
  {
    LONG_MIN <= v <= LONG_MAX
  }

  // §3.3.1 — ZStar：ℤ ∪ {⊤}。FinZ 承载可负有符号值；TopZ 为未知（R4-F1：溢出/超域不静默回卷）
  datatype ZStar = FinZ(n: int) | TopZ

  // C# 可构造态：TopZ 或值域内 FinZ（private 构造子 + long 字段保证）
  predicate ZLegal(v: ZStar)
  {
    v.TopZ? || ZInRange(v.n)
  }

  // §3.3.1 加法律：任一 ⊤ ⇒ ⊤；有限+有限 越值域 ⇒ ⊤（保守，实现以同号符号翻转检测等价判定）
  function ZAdd(a: ZStar, b: ZStar): ZStar
  {
    if a.TopZ? || b.TopZ? then TopZ
    else if ZInRange(a.n + b.n) then FinZ(a.n + b.n)
    else TopZ
  }

  // §3.3.1 — 有符号 net 区间 [lo, hi]（构造子不变量：仅两端有限时校验 lo ≤ hi）
  datatype SignedInterval = SI(lo: ZStar, hi: ZStar)
  {
    predicate Valid()
    {
      (!lo.TopZ? && !hi.TopZ?) ==> lo.n <= hi.n
    }

    // §3.3.1/DO-9 — 区间含 0：任一端 ⊤ ⇒ false（fail-closed，需人工界定）；否则 lo ≤ 0 ≤ hi
    predicate ContainsZero()
    {
      !lo.TopZ? && !hi.TopZ? && lo.n <= 0 && 0 <= hi.n
    }

    // §3.3.1 — net 真求和：逐端 ZAdd（同资源 create(+) 与 release(−) 自然抵消；⊤/溢出 ⇒ ⊤）
    function AddZ(o: SignedInterval): SignedInterval
    {
      SI(ZAdd(lo, o.lo), ZAdd(hi, o.hi))
    }
  }

  // §3.3.1 — 零区间 [0,0]（缺省净效应，含 0）
  const ZeroSI := SI(FinZ(0), FinZ(0))

  // Algebra.NetTable.ToSigned 的模型：端 ⊤ 或超 long 域 ⇒ ZStar.TopZ（R4-F1 禁 (long) 静默强转）
  function ConvZ(v: NatStar): ZStar
  {
    if v.Top? || v.n > LONG_MAX then TopZ else FinZ(v.n)
  }

  // Algebra.NetTable.Negate 的端点变换：-v（v ≤ LONG_MAX ⇒ -v ≥ LONG_MIN 恒在域内）
  // 实现注意：v.n 为 nat，须显式放宽为 int 再取负（Dafny 4 对 nat 一元负号仍按 nat 子类型检查）
  function ConvNeg(v: NatStar): ZStar
  {
    if v.Top? || v.n > LONG_MAX then TopZ else FinZ(0 - (v.n as int))
  }

  // create/move 的正号贡献 [lo, hi]
  function ToSigned(lo: NatStar, hi: NatStar): SignedInterval
  {
    SI(ConvZ(lo), ConvZ(hi))
  }

  // release 的负号贡献 [-hi, -lo]
  function NegSigned(lo: NatStar, hi: NatStar): SignedInterval
  {
    SI(ConvNeg(hi), ConvNeg(lo))
  }

  // §3.3.1 — net 记录：某资源无净效应记录（Missing）或有符号区间（Entry）。
  // IsConserved 的 fail-closed 双闸在此显式化：Missing ⇒ false；ContainsZero 内嵌 ⊤ ⇒ false。
  datatype NetEntry = Missing | Entry(s: SignedInterval)

  function IsConserved(e: NetEntry): bool
  {
    e.Entry? && e.s.ContainsZero()
  }

  // ── ℤ* 加法律（net 与 claim 枚举序无关的代数前提——NetTable.Compute 依赖之）──
  lemma ZAddTopAbsorbing(a: ZStar)
    ensures ZAdd(a, TopZ) == TopZ && ZAdd(TopZ, a) == TopZ
  {
  }

  // R4-F1 直接形态：越域 ⇒ ⊤（不静默回卷翻转符号）
  lemma ZAddOverflowYieldsTop(x: int, y: int)
    requires ZInRange(x) && ZInRange(y) && !ZInRange(x + y)
    ensures ZAdd(FinZ(x), FinZ(y)) == TopZ
  {
  }

  // 加法不产出非法态（no-silent-wraparound 的类型面）
  lemma ZAddPreservesLegal(a: ZStar, b: ZStar)
    requires ZLegal(a) && ZLegal(b)
    ensures ZLegal(ZAdd(a, b))
  {
  }

  lemma ZAddCommutative(a: ZStar, b: ZStar)
    ensures ZAdd(a, b) == ZAdd(b, a)
  {
  }

  // 结合律只在无溢出域内成立——溢出保守代数中朴素结合律为假
  // （反例：ZAdd(ZAdd(MAX,MAX),−MAX)=⊤ 而 ZAdd(MAX,ZAdd(MAX,−MAX))=FinZ(MAX)，
  //   全部输入合法；中途溢出的折叠序产生保守 ⊤，另一序得精确有限和——二者分歧方向恒为
  //   「⊤ 是 fail-closed」，可靠性由 FoldZSound/FoldZTopOnTotalOverflow 承载）
  lemma ZAddAssociativeNoOverflow(a: ZStar, b: ZStar, c: ZStar)
    requires a.FinZ? && b.FinZ? && c.FinZ?
         && ZInRange(a.n + b.n) && ZInRange(b.n + c.n) && ZInRange(a.n + b.n + c.n)
    ensures ZAdd(ZAdd(a, b), c) == ZAdd(a, ZAdd(b, c))
  {
  }

  lemma ZAddIdentity(a: ZStar)
    requires ZLegal(a)
    ensures ZAdd(a, FinZ(0)) == a
  {
  }

  // ── net 折叠的可靠性（NetTable.Compute 按 AllClaims 迭代序累加的正当性）──
  // 溢出⇒⊤ 使朴素结合律失效 ⇒ 序无关性不取「任意序同结果」强形态，而取可靠形态：
  // 有限折叠结果恒等于精确数学和（真⇒可信）；真和越域 ⇒ 任何折叠序都得 ⊤（fail-closed 恒检出）。

  // 全端有限且在值域内（合法 claim size 序列的形态）
  predicate AllInRange(s: seq<ZStar>)
  {
    forall i | 0 <= i < |s| :: !s[i].TopZ? && ZInRange(s[i].n)
  }

  function SumZ(s: seq<ZStar>): int
  {
    if |s| == 0 then 0
    else SumZ(s[..|s|-1]) + (if s[|s|-1].TopZ? then 0 else s[|s|-1].n)
  }

  function FoldZ(s: seq<ZStar>): ZStar
  {
    if |s| == 0 then FinZ(0)
    else ZAdd(FoldZ(s[..|s|-1]), s[|s|-1])
  }

  // 可靠性主定理：折叠结果要么 ⊤（保守报警），要么恰为精确数学和（有限结果可信）
  lemma FoldZSound(s: seq<ZStar>)
    requires AllInRange(s)
    ensures FoldZ(s) == TopZ || FoldZ(s) == FinZ(SumZ(s))
  {
    if |s| > 0
    {
      AllInRangeBounded(s, |s| - 1);
      FoldZSound(s[..|s|-1]);
    }
  }

  // 真和越值域 ⇒ 任何折叠序都检出（R4-F1 不静默回卷的全称形态）。
  // 前缀和可在域内而总和越域（末步溢出）——正确性不靠同前提前缀递归，靠 FoldZSound 分支分析。
  lemma FoldZTopOnTotalOverflow(s: seq<ZStar>)
    requires AllInRange(s) && !ZInRange(SumZ(s))
    ensures FoldZ(s) == TopZ
  {
    if |s| > 0
    {
      AllInRangeBounded(s, |s| - 1);
      FoldZSound(s[..|s|-1]);
      assert SumZ(s) == SumZ(s[..|s|-1]) + s[|s|-1].n;
    }
  }

  // AllInRange 对有界前缀封闭（引理递归的前置搬运；k ≤ |s| 显式约束防空序列负界）
  lemma AllInRangeBounded(s: seq<ZStar>, k: nat)
    requires AllInRange(s) && k <= |s|
    ensures AllInRange(s[..k])
  {
  }

  // 区间级加法交换（NetTable.Compute 按 AllClaims 迭代序累加，结果必须与序无关）
  lemma AddZCommutative(x: SignedInterval, y: SignedInterval)
    ensures x.AddZ(y) == y.AddZ(x)
  {
    ZAddCommutative(x.lo, y.lo);
    ZAddCommutative(x.hi, y.hi);
  }

  // ── ContainsZero 契约面：「区间含 0 ⇔ 守恒」的判定⇔ 与 fail-closed 闸 ──
  lemma ContainsZeroIff(s: SignedInterval)
    ensures s.ContainsZero() <==> (!s.lo.TopZ? && !s.hi.TopZ? && s.lo.n <= 0 && 0 <= s.hi.n)
  {
  }

  lemma TopEndFailsClosed(s: SignedInterval)
    requires s.lo.TopZ? || s.hi.TopZ?
    ensures !s.ContainsZero()
  {
  }

  lemma ZeroSIContainsZero()
    ensures ZeroSI.ContainsZero()
  {
  }

  // ── 转换保持合法性（有限端单调；超域端 ⇒ TopZ，构造子不比较 ⊤ 端）──
  lemma ToSignedValid(lo: NatStar, hi: NatStar)
    requires lo.Le(hi)
    ensures (ToSigned(lo, hi)).Valid()
  {
  }

  lemma NegSignedValid(lo: NatStar, hi: NatStar)
    requires lo.Le(hi)
    ensures (NegSigned(lo, hi)).Valid()
  {
  }

  // ── 守恒律本体：create/release 精确配对 ⇒ net 区间含 0（DO-9 不报警的数学根据）──
  // net = [a−b, b−a]，a ≤ b ⇒ 含 0（配对同 size 时进一步坍缩为 [0,0]）
  lemma ExactPairingContainsZero(a: NatStar, b: NatStar)
    requires !a.Top? && !b.Top? && a.n <= b.n && b.n <= LONG_MAX
    ensures (ToSigned(a, b)).AddZ(NegSigned(a, b)).ContainsZero()
  {
  }

  lemma ExactPairingNetsZero(a: NatStar)
    requires !a.Top? && a.n <= LONG_MAX
    ensures (ToSigned(a, a)).AddZ(NegSigned(a, a)) == ZeroSI
  {
  }

  // 「区间含 0 ⇔ 守恒」全 ⇔（含 Missing / ⊤ 端 fail-closed 双闸的显式化）
  lemma ConservationIff(e: NetEntry)
    ensures IsConserved(e) <==>
      (e.Entry? && !e.s.lo.TopZ? && !e.s.hi.TopZ? && e.s.lo.n <= 0 && 0 <= e.s.hi.n)
    ensures !IsConserved(Missing)
  {
  }
}
