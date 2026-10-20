// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The CPI and year-on-year swaps' results records compare by value, which QuantLib's results classes don't define:
/// <see cref="CPISwapResults"/> and <see cref="YearOnYearInflationSwapResults"/> each add the fair rate and spread to
/// <see cref="SwapResults"/>'s written-out equality, and never equal a record of another type; their accessors read the
/// per-leg arrays, the fixed or CPI leg first.
/// </summary>
public class InflationSwapResultsEqualityTests
{
    [Fact]
    public void CpiSwapResultsCompareByValue()
    {
        CPISwapResults a = Sample(new CPISwapResults()) with { FairRate = 0.031, FairSpread = null };
        CPISwapResults b = Sample(new CPISwapResults()) with { FairRate = 0.031, FairSpread = null };
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Each member counts, the arrays element by element.
        Assert.NotEqual(a, a with { FairRate = 0.032 });
        Assert.NotEqual(a, a with { FairSpread = 0.0 });
        Assert.NotEqual(a, a with { LegNPV = [-100.0, 112.6] });
        Assert.Equal(a, a with { LegNPV = [-100.0, 112.5] });

        Assert.Equal(-100.0, a.FixedLegNPV);
        Assert.Equal(112.5, a.FloatLegNPV);
        Assert.Null(new CPISwapResults().FixedLegNPV);
    }

    [Fact]
    public void YearOnYearSwapResultsCompareByValue()
    {
        YearOnYearInflationSwapResults a = Sample(new YearOnYearInflationSwapResults()) with { FairRate = 0.029, FairSpread = 0.001 };
        YearOnYearInflationSwapResults b = Sample(new YearOnYearInflationSwapResults()) with { FairRate = 0.029, FairSpread = 0.001 };
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        Assert.NotEqual(a, a with { FairRate = 0.03 });
        Assert.NotEqual(a, a with { FairSpread = null });
        Assert.NotEqual(a, a with { EndDiscounts = [0.9, 0.9] });

        Assert.Equal(-100.0, a.FixedLegNPV);
        Assert.Equal(112.5, a.YoYLegNPV);
        Assert.Null(new YearOnYearInflationSwapResults().YoYLegNPV);

        // Records of different types never compare equal, whatever their members.
        CPISwapResults cpi = Sample(new CPISwapResults()) with { FairRate = 0.029, FairSpread = 0.001 };
        Assert.NotEqual<SwapResults>(cpi, a);
        Assert.False(a.Equals(Sample(new SwapResults())));
    }

    private static T Sample<T>(T results)
        where T : SwapResults =>
        results with
        {
            NPV = 12.5,
            ValuationDate = new Date(15, Month.June, 2024),
            LegNPV = [-100.0, 112.5],
            LegBPS = [-0.4, 0.45],
            StartDiscounts = [1.0, 1.0],
            EndDiscounts = [0.9, null],
            NpvDateDiscount = 1.0,
        };
}
