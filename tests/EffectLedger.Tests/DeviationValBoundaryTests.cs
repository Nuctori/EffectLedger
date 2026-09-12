using EffectLedger;

namespace EffectLedger.Tests;

public sealed class DeviationValBoundaryTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Of_RejectsNonFiniteValues(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviationVal.Of(value));
    }
}
