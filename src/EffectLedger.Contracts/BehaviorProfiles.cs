// BehaviorProfiles.cs — 可选类型行为约束的用户面声明（P1）。
// 本文件是**纯声明**：不含业务成员、不含运行时实例、不含分析逻辑。
// 语义由 EffectLedger.Contracts.Analyzer 检查；本包单独安装只表达意图，不提供任何保证。
//
// 【R-SEM-01】声明产生验证义务，不自动完成义务。
// 实现 IConstrained<TProfile> 只是主张；外部程序集/开放泛型上的 marker 不构成实现已验证的证据。

namespace EffectLedger.Contracts;

/// <summary>
/// 行为角色标记基接口（P1）。
/// 不定义业务成员：实现者不需要 Run/Combine/Behavior 之类与领域无关的方法。
/// 用户自定义角色本期不受支持——分析器按精确符号识别内建角色，未知角色报 EBC0001。
/// </summary>
public interface IBehaviorProfile
{
}

/// <summary>
/// 为用户类型选择一个行为角色（P1）。
/// <para>
/// 本接口**没有成员**。它不把类型塞进 pipeline，也不要求任何执行框架；
/// 唯一语义是：该类型的实现（及其可解析的依赖）必须符合 <typeparamref name="TProfile"/> 的规则，
/// 由 EffectLedger.Contracts.Analyzer 检查。
/// </para>
/// <para>
/// 首版声明面只接受非静态 sealed class / record class 与受支持的值类型；
/// static class 不能实现接口（编译器限制），其辅助方法仍可被依赖分析（见计划 B 节）。
/// </para>
/// </summary>
public interface IConstrained<TProfile> where TProfile : IBehaviorProfile
{
}

/// <summary>
/// 内建角色：深层不可变值（P3）。
/// <para>
/// 承诺：在受支持的托管操作模型下，实例构造完成后，公开可观察的实例值及其可达数据保持稳定。
/// 检查：构造函数之后的字段/可达对象写入、内部可变别名外泄、构造期 this 逃逸、值访问的稳定性。
/// </para>
/// <para>
/// 不承诺：Equals/GetHashCode 正确性、非空、业务不变量、序列化往返、构造不抛异常；
/// 也不包含反射篡改 / unsafe / 恶意宿主的防御（R-BOUNDARY-04）。
/// </para>
/// </summary>
public sealed class ImmutableValue : IBehaviorProfile
{
}

/// <summary>
/// 内建角色：确定性计算（P4）。
/// <para>
/// 承诺：在输入的逻辑值与已声明依赖相同、且输入在求值期间稳定的前提下，
/// 计算不依赖未声明的变化来源，也不产生外部可观察写入；允许修改不逃逸的局部对象。
/// </para>
/// <para>
/// 不承诺：终止性（Total）、不抛异常、跨进程/跨运行时版本一致性、线程安全，
/// 也不包含交换律/幂等律等代数性质（P8 后续范围）。
/// </para>
/// </summary>
public sealed class DeterministicComputation : IBehaviorProfile
{
}
