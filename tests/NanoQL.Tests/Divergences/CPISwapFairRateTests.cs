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
/// <see cref="CPISwap"/>'s fair rate. QuantLib derives it as <c>fixedRate - NPV / (legBPS[0] / 1bp)</c>
/// (<c>cpiswap.cpp:195-199</c>), but a CPI coupon's sensitivity to its rate carries its index ratio, which the BPS
/// leaves out, so the swap repriced at QuantLib's fair rate isn't worth zero (QuantLib's defect, R8). nanoql's
/// <see cref="DiscountingSwapEngine"/> gives the rate that zeroes the swap, <c>fixedRate - NPV / (payer[0] S)</c>, S
/// the NPV of the swap's CPI coupons rebuilt at a unit rate. On <see cref="InflationFixture"/>'s market, for a
/// five-year payer swap of 1,000,000 against a 6-month Ibor leg on a curve flat at 4.5% plus 0.1%, at 2% on a base CPI
/// of 306.1, QuantLib's fair rate is 0.011610586633227834 and the swap rebuilt at it is worth -4,112.158481168008; the
/// root of QuantLib's own NPV, found by QuantLib's <c>Brent</c> through the wheel, is 0.012444035321727333, where the
/// swap is worth 0.0, and it is nanoql's fair rate.
/// </summary>
public class CPISwapFairRateTests
{
    [Fact]
    public void TheFairRateZeroesTheSwap()
    {
        EvaluationContext context = InflationFixture.Context();
        FlatForward nominal = InflationFixture.Nominal();

        double fair = DiscountingSwapEngine.Calculate(SwapAt(0.02, context), context, nominal).FairRate!.Value;

        // QuantLib's: 0.011610586633227834
        Assert.True(Tolerance.Within(0.012444035321727333, fair), $"fair rate {fair}");
        double npv = DiscountingSwapEngine.Calculate(SwapAt(fair, context), context, nominal).NPV!.Value;
        Assert.True(Math.Abs(npv) < 1e-6, $"NPV at the fair rate {npv}");
    }

    private static CPISwap SwapAt(double rate, EvaluationContext context)
    {
        Calendar uk = new UnitedKingdom();
        Date today = InflationFixture.Today;
        var end = new Date(15, Month.June, 2029);
        var floatSchedule = new Schedule(
            today,
            end,
            new Period(6, TimeUnit.Months),
            uk,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);
        var fixedSchedule = new Schedule(
            today,
            end,
            new Period(1, TimeUnit.Years),
            uk,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);
        var ibor = new IborIndex(
            "GbpIbor",
            new Period(6, TimeUnit.Months),
            0,
            null,
            uk,
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual365Fixed(),
            new FlatForward(today, 0.045, new Actual365Fixed()));
        return new CPISwap(
            SwapType.Payer,
            1e6,
            true,
            0.001,
            new Actual365Fixed(),
            floatSchedule,
            BusinessDayConvention.ModifiedFollowing,
            0,
            ibor,
            rate,
            306.1,
            new Actual365Fixed(),
            fixedSchedule,
            BusinessDayConvention.ModifiedFollowing,
            new Period(3, TimeUnit.Months),
            new UKRPI(InflationFixture.ZeroCurve()),
            context,
            CPIInterpolationType.Flat);
    }
}
