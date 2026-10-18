// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The swap results records QuantLib's fixed-vs-floating and overnight-indexed swaps get compare by value, which
/// QuantLib's results classes don't define: <see cref="FixedVsFloatingSwapResults"/> adds the fair rate and spread to
/// <see cref="SwapResults"/>'s written-out equality, and <see cref="OvernightIndexedSwapResults"/>, which holds no
/// state of its own, compares as its base does, but never equals a record of another type.
/// </summary>
public class SwapResultsEqualityTests
{
    private static T Sample<T>(T results)
        where T : FixedVsFloatingSwapResults =>
        results with
        {
            NPV = 12.5,
            ValuationDate = new Date(15, Month.January, 2025),
            LegNPV = [-100.0, 112.5],
            LegBPS = [-0.4, 0.45],
            StartDiscounts = [1.0, 1.0],
            EndDiscounts = [0.9, null],
            NpvDateDiscount = 1.0,
            FairRate = 0.031,
            FairSpread = null,
        };

    [Fact]
    public void SwapResultsCompareByValue()
    {
        FixedVsFloatingSwapResults a = Sample(new FixedVsFloatingSwapResults());
        FixedVsFloatingSwapResults b = Sample(new FixedVsFloatingSwapResults());
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.True(((SwapResults)a).Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Each member counts, the arrays element by element.
        Assert.NotEqual(a, a with { FairRate = 0.032 });
        Assert.NotEqual(a, a with { FairSpread = 0.0 });
        Assert.NotEqual(a, a with { LegBPS = [-0.4, 0.46] });
        Assert.Equal(a, a with { LegBPS = [-0.4, 0.45] });

        // The accessors read the per-leg arrays: the fixed leg first, the floating leg second.
        Assert.Equal(-0.4, a.FixedLegBPS);
        Assert.Equal(-100.0, a.FixedLegNPV);
        Assert.Equal(0.45, a.FloatingLegBPS);
        Assert.Equal(112.5, a.FloatingLegNPV);
        Assert.Null(new FixedVsFloatingSwapResults().FixedLegBPS);

        // The overnight record compares as its base, and only with its own type.
        OvernightIndexedSwapResults x = Sample(new OvernightIndexedSwapResults());
        OvernightIndexedSwapResults y = Sample(new OvernightIndexedSwapResults());
        Assert.Equal(x, y);
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
        Assert.NotEqual(x, x with { FairRate = 0.0 });
        Assert.Equal(0.45, x.OvernightLegBPS);
        Assert.Equal(112.5, x.OvernightLegNPV);
        Assert.False(x.Equals((FixedVsFloatingSwapResults)a));
        Assert.False(a.Equals(x));
        Assert.NotEqual<SwapResults>(new SwapResults { NPV = 12.5 }, new FixedVsFloatingSwapResults { NPV = 12.5 });
    }
}
