// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="BMASwapResults"/> compares by value, which QuantLib's BMA swap, whose inspectors derive from its swap
/// results, doesn't define: it adds the fair Libor fraction and spread to <see cref="SwapResults"/>'s written-out
/// equality, NaN comparing equal to itself as synthesized record equality has it, and never equals a record of another
/// type. Its leg inspectors read the per-leg arrays, the Libor leg first.
/// </summary>
public class BMASwapResultsEqualityTests
{
    private static BMASwapResults Sample() => new()
    {
        NPV = -5155.6,
        ValuationDate = new Date(15, Month.January, 2025),
        LegNPV = [1.2e5, -1.25e5],
        LegBPS = [196.2, -195.8],
        StartDiscounts = [1.0, 1.0],
        EndDiscounts = [0.94, 0.94],
        NpvDateDiscount = 1.0,
        FairLiborFraction = 0.779,
        FairLiborSpread = double.NaN,
    };

    [Fact]
    public void BMASwapResultsCompareByValue()
    {
        BMASwapResults a = Sample(), b = Sample();
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.True(((SwapResults)a).Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Each member counts, the arrays element by element.
        Assert.NotEqual(a, a with { FairLiborFraction = null });
        Assert.NotEqual(a, a with { FairLiborSpread = 0.0026 });
        Assert.NotEqual(a, a with { LegNPV = [1.2e5, -1.26e5] });
        Assert.Equal(a, a with { LegBPS = [196.2, -195.8] });
        Assert.False(a.Equals(null));

        // The inspectors read the per-leg arrays: the Libor leg first, the BMA leg second.
        Assert.Equal(1.2e5, a.LiborLegNPV);
        Assert.Equal(196.2, a.LiborLegBPS);
        Assert.Equal(-1.25e5, a.BmaLegNPV);
        Assert.Equal(-195.8, a.BmaLegBPS);
        Assert.Null(new BMASwapResults().LiborLegBPS);
        Assert.Null(new BMASwapResults().BmaLegNPV);

        // A record of another type never equals it.
        Assert.False(((SwapResults)a).Equals(new SwapResults { NPV = a.NPV, LegNPV = a.LegNPV, LegBPS = a.LegBPS }));
        Assert.NotEqual<SwapResults>(new SwapResults { NPV = 1.0 }, new BMASwapResults { NPV = 1.0 });
    }
}
