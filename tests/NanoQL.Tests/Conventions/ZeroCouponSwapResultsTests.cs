// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The zero-coupon swap's results record (the S-cms plan's CMD7) compares by value, which QuantLib's results classes
/// don't define: <see cref="ZeroCouponSwapResults"/> adds the fair payment to <see cref="SwapResults"/>'s written-out
/// equality, and never equals a record of another type; its accessors read the per-leg arrays, the fixed leg first.
/// </summary>
public class ZeroCouponSwapResultsTests
{
    [Fact]
    public void TheResultsCompareByValue()
    {
        ZeroCouponSwapResults a = Sample() with { FairFixedPayment = 1.6776e7 };
        ZeroCouponSwapResults b = Sample() with { FairFixedPayment = 1.6776e7 };
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Each member counts, the arrays element by element.
        Assert.NotEqual(a, a with { FairFixedPayment = 1.6777e7 });
        Assert.NotEqual(a, a with { FairFixedPayment = null });
        Assert.NotEqual(a, a with { LegNPV = [-100.0, 112.6] });
        Assert.Equal(a, a with { LegNPV = [-100.0, 112.5] });

        Assert.Equal(-100.0, a.FixedLegNPV);
        Assert.Equal(112.5, a.FloatingLegNPV);
        Assert.Null(new ZeroCouponSwapResults().FixedLegNPV);
        Assert.Null(new ZeroCouponSwapResults().FairFixedPayment);

        // Records of different types never compare equal, whatever their members.
        Assert.False(a.Equals(Sample(new SwapResults())));
        Assert.NotEqual<SwapResults>(Sample(new CPISwapResults()), Sample());
    }

    private static ZeroCouponSwapResults Sample() => Sample(new ZeroCouponSwapResults());

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
