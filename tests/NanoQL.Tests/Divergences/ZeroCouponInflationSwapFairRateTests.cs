// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="ZeroCouponInflationSwap.FairRate"/> where the legs pay on different days. QuantLib's fair rate is the
/// inflation growth's rate, <c>growth^(1/T) - 1</c> (<c>zerocouponinflationswap.cpp:118-138</c>), whose comment reads
/// "Always means that NPV is zero for _this_ instrument", which fails once the legs' conventions part their payment
/// days (QuantLib's defect, R8). nanoql scales the growth's excess by the ratio of the legs' end discounts, read from
/// the engine's results. On <see cref="InflationFixture"/>'s market, for a payer swap of 1,000,000 from
/// 15 June 2024 to Saturday 31 May 2025, its fixed leg Following (paying 2 June) and its inflation leg Preceding
/// (paying 30 May), QuantLib's fair rate is 0.03078176611785821 and the swap rebuilt at it is worth -9.332619118980801;
/// the root of QuantLib's own NPV, found by QuantLib's <c>Brent</c> through the wheel, is 0.030791894092525723, and it
/// is nanoql's fair rate.
/// </summary>
public class ZeroCouponInflationSwapFairRateTests
{
    [Fact]
    public void TheFairRateZeroesTheSwap()
    {
        EvaluationContext context = InflationFixture.Context();
        FlatForward nominal = InflationFixture.Nominal();
        ZeroCouponInflationSwap swap = SwapAt(0.03);
        Assert.NotEqual(swap.FixedLeg[0].Date, swap.InflationLeg[0].Date);

        double fair = swap.FairRate(context, DiscountingSwapEngine.Calculate(swap, context, nominal));

        // QuantLib's: 0.03078176611785821
        Assert.True(Tolerance.Within(0.030791894092525723, fair), $"fair rate {fair}");
        double npv = DiscountingSwapEngine.Calculate(SwapAt(fair), context, nominal).NPV!.Value;
        Assert.True(Math.Abs(npv) < 1e-6, $"NPV at the fair rate {npv}");
    }

    private static ZeroCouponInflationSwap SwapAt(double rate) =>
        new(
            SwapType.Payer,
            1e6,
            InflationFixture.Today,
            new Date(31, Month.May, 2025),
            new UnitedKingdom(),
            BusinessDayConvention.Following,
            new Actual365Fixed(),
            rate,
            new UKRPI(InflationFixture.ZeroCurve()),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            false,
            new UnitedKingdom(),
            BusinessDayConvention.Preceding);
}
