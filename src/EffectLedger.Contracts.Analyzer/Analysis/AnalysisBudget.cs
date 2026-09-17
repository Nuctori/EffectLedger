// AnalysisBudget.cs — P2.7 资源与取消预算。
// 达到预算必须返回 Unknown，不是成功（不可妥协要求 7）。
// 默认上限来自 P6 规模探针前的保守初值，可由配置覆盖；不临时放大以掩盖算法问题。

namespace EffectLedger.Contracts.Analyzer.Analysis;

public sealed class AnalysisBudget
{
    public int MaxOperationsPerMethod { get; init; } = 50_000;
    public int MaxCallGraphNodes { get; init; } = 5_000;

    public static AnalysisBudget Default => new();

    /// <summary>消耗一次操作遍历预算；返回 false 表示预算耗尽（必须转 Unknown）。</summary>
    public bool ConsumeOperation(ref int counter)
    {
        if (counter >= MaxOperationsPerMethod) return false;
        counter++;
        return true;
    }
}
