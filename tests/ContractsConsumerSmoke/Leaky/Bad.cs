using System;
using EffectLedger.Contracts;

namespace LeakyConsumer;

// 反例：包链加载后必须报 EBC2001（分析器未落 analyzers/ 或未加载时此工程会假绿）。
public sealed class ClockReader : IConstrained<DeterministicComputation>
{
    public DateTime Stamp() => DateTime.UtcNow;
}
