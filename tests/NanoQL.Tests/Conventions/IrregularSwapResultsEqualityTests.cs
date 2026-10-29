// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="IrregularSwapResults"/> compares by value, which QuantLib's irregular swap, whose inspectors derive from
/// its swap results, doesn't define: it adds the fair rate and spread to <see cref="SwapResults"/>'s written-out
/// equality, NaN comparing equal to itself as synthesized record equality has it, and never equals a record of another
/// type. Its leg inspectors read the per-leg arrays, the fixed leg first.
/// </summary>
public class IrregularSwapResultsEqualityTests
{
    private static IrregularSwapResults Sample() => new()
    {
        NPV = 1.92,
        ValuationDate = new Date(15, Month.January, 2025),
        LegNPV = [-13.7, 15.62],
        LegBPS = [-4.6, 0.02],
        StartDiscounts = [0.97, 0.97],
        EndDiscounts = [0.83, 0.83],
        NpvDateDiscount = 1.0,
        FairRate = 0.0342,
        FairSpread = double.NaN,
    };

    [Fact]
    public void IrregularSwapResultsCompareByValue()
    {
        IrregularSwapResults a = Sample(), b = Sample();
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.True(((SwapResults)a).Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Each member counts, the arrays element by element.
        Assert.NotEqual(a, a with { FairRate = null });
        Assert.NotEqual(a, a with { FairSpread = 0.0011 });
        Assert.NotEqual(a, a with { LegNPV = [-13.7, 15.63] });
        Assert.Equal(a, a with { LegBPS = [-4.6, 0.02] });
        Assert.False(a.Equals(null));

        // The inspectors read the per-leg arrays: the fixed leg first, the floating leg second.
        Assert.Equal(-13.7, a.FixedLegNPV);
        Assert.Equal(-4.6, a.FixedLegBPS);
        Assert.Equal(15.62, a.FloatingLegNPV);
        Assert.Equal(0.02, a.FloatingLegBPS);
        Assert.Null(new IrregularSwapResults().FixedLegNPV);
        Assert.Null(new IrregularSwapResults().FloatingLegBPS);

        // A record of another type never equals it.
        Assert.False(((SwapResults)a).Equals(new SwapResults { NPV = a.NPV, LegNPV = a.LegNPV, LegBPS = a.LegBPS }));
        Assert.NotEqual<SwapResults>(new SwapResults { NPV = 1.0 }, new IrregularSwapResults { NPV = 1.0 });
    }
}
