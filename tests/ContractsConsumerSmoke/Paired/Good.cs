using EffectLedger.Contracts;

namespace PairedConsumer;

public sealed class Snapshot : IConstrained<ImmutableValue>
{
    private readonly int _total;
    public Snapshot(int total) { _total = total; }
    public int Total => _total;
}

public sealed class Calc : IConstrained<DeterministicComputation>
{
    public int Double(int x) => x * 2;
}
