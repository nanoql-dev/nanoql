// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The basket payoffs over spans (the S-exo plan's ER1 (ii)): an array, a <see cref="Vector"/>'s storage and a stack
/// buffer give one value, the payoff's call on a price passes to its base payoff, and the minimum and maximum keep the
/// first of equal extremes and drop or keep a NaN by position, as <c>std::min_element</c> and <c>std::max_element</c>.
/// </summary>
public class BasketPayoffSpanTests
{
    [Fact]
    public void AnArrayAVectorsStorageAndAStackBufferGiveOneValue()
    {
        var call = new PlainVanillaPayoff(OptionType.Call, 100.0);
        BasketPayoff[] payoffs =
        [
            new MinBasketPayoff(call), new MaxBasketPayoff(call), new AverageBasketPayoff(call, [0.5, 0.3, 0.2]),
            new AverageBasketPayoff(call, 3),
        ];
        double[] array = [90.0, 130.0, 110.0];
        var vector = new Vector(array);
        Span<double> stack = stackalloc double[3];
        array.CopyTo(stack);
        foreach (BasketPayoff p in payoffs)
        {
            Assert.Equal(p.Evaluate(array), p.Evaluate(vector.AsSpan()));
            Assert.Equal(p.Evaluate(array), p.Evaluate(stack));
            Assert.Equal(call.Evaluate(p.Accumulate(array)), p.Evaluate(array));
            Assert.Equal(call.Evaluate(105.0), p.Evaluate(105.0));
            Assert.Equal(call.Name, p.Name);
            Assert.Same(call, p.BasePayoff);
        }

        Assert.Equal(40.0, new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Put, 0.0)).Evaluate([90.0, 130.0]));
    }

    [Fact]
    public void TheExtremesKeepTheFirstAndANaNByPosition()
    {
        var p = new PlainVanillaPayoff(OptionType.Call, 0.0);

        // std::min_element compares a later element with the smallest so far, so a NaN first stays and a NaN later is
        // passed over
        Assert.True(double.IsNaN(new MinBasketPayoff(p).Accumulate([double.NaN, 1.0, 2.0])));
        Assert.Equal(1.0, new MinBasketPayoff(p).Accumulate([1.0, double.NaN, 2.0]));
        Assert.True(double.IsNaN(new MaxBasketPayoff(p).Accumulate([double.NaN, 1.0, 2.0])));
        Assert.Equal(2.0, new MaxBasketPayoff(p).Accumulate([1.0, double.NaN, 2.0]));
        Assert.Equal(double.NegativeZero, new MinBasketPayoff(p).Accumulate([-0.0, 0.0]));
        Assert.Equal(-0.0, new MaxBasketPayoff(p).Accumulate([-0.0, 0.0]));
    }
}
