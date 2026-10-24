// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The asset swap's results record (the S-cms plan's CMD6) compares by value, which QuantLib's results classes don't
/// define: <see cref="AssetSwapResults"/> adds the fair spread, clean price and non-par repayment to
/// <see cref="SwapResults"/>'s written-out equality, and never equals a record of another type; a new record holds no
/// fair values.
/// </summary>
public class AssetSwapResultsTests
{
    [Fact]
    public void TheResultsCompareByValue()
    {
        AssetSwapResults a = Sample() with { FairSpread = 0.0012, FairCleanPrice = 101.5, FairNonParRepayment = 99.5 };
        AssetSwapResults b = Sample() with { FairSpread = 0.0012, FairCleanPrice = 101.5, FairNonParRepayment = 99.5 };
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Each member counts, the arrays element by element.
        Assert.NotEqual(a, a with { FairSpread = 0.0013 });
        Assert.NotEqual(a, a with { FairCleanPrice = null });
        Assert.NotEqual(a, a with { FairNonParRepayment = 99.6 });
        Assert.NotEqual(a, a with { LegNPV = [-100.0, 112.6] });
        Assert.Equal(a, a with { LegNPV = [-100.0, 112.5] });

        Assert.Null(new AssetSwapResults().FairSpread);
        Assert.Null(new AssetSwapResults().FairCleanPrice);
        Assert.Null(new AssetSwapResults().FairNonParRepayment);

        // Records of different types never compare equal, whatever their members.
        Assert.False(a.Equals(Sample(new SwapResults())));
        Assert.NotEqual<SwapResults>(Sample(new ZeroCouponSwapResults()), Sample());
    }

    private static AssetSwapResults Sample() => Sample(new AssetSwapResults());

    private static T Sample<T>(T results)
        where T : SwapResults =>
        results with
        {
            NPV = 12.5,
            ValuationDate = new Date(15, Month.January, 2025),
            LegNPV = [-100.0, 112.5],
            LegBPS = [-0.4, 0.45],
            StartDiscounts = [1.0, 1.0],
            EndDiscounts = [0.9, 0.9],
            NpvDateDiscount = 1.0,
        };
}
