// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The year-on-year cap and floor engines' forward. QuantLib's engines take every optionlet's forward from the index's
/// curve at the coupon's fixing date, <c>yoyTS-&gt;yoyRate(fixingDate)</c> (<c>inflationcapfloorengines.cpp:81</c>),
/// and read no fixing, while QuantLib's own year-on-year pricer prices the same optionlet on the coupon's fixing
/// (<c>inflationcouponpricer.cpp:99-109</c>; QuantLib's defect). nanoql's engines read each optionlet's forward as its
/// coupon's own fixing, <see cref="CPI.LaggedYoYRate"/> at its accrual end with its lag and interpolation, and keep
/// QuantLib's zero deviation for a fixing at or before the volatility's base date. QuantLib's check is the wheel's
/// capped year-on-year coupon priced by its <c>BlackYoYInflationCouponPricer</c>, the caplet's rate times its accrual,
/// nominal and discount. On <see cref="InflationFixture"/>'s market, a ratio of UK RPI and a Black volatility of 15%
/// lagged two months, each case a one-coupon cap of 1,000,000, Actual/365 (Fixed):
/// <list type="bullet">
/// <item>a coupon from 1 July 2023 to 1 July 2024, lagged three months, struck at 3.2%, fixed at 0.033084673723716485
/// on 1 April 2024 where the curve reads 3%: QuantLib's engine prices it at 0.0, its pricer and nanoql at
/// 1,085.739999233565;</item>
/// <item>the same coupon on a curve based in May 2024: QuantLib's engine refuses "date (April 1st, 2024) is before base
/// date (May 1st, 2024)", its pricer and nanoql price it at 1,085.739999233565;</item>
/// <item>a coupon from 15 July 2024 to 15 July 2025, linear, struck at 2.5%, on a curve falling from 3% in April 2024
/// to 2% in April 2028: the engine's forward is the curve's rate at May 2025's start, 0.02729637234770705, the coupon's
/// 0.027200547570157427; QuantLib's engine prices it at 2,896.7205361166657, its pricer and nanoql at
/// 2,829.215810328031;</item>
/// <item>a coupon from 3 July 2025 to 3 July 2026 with five fixing days, struck at 2.5%, fixing on 28 April 2026, on
/// the same curve: the engine's forward is April's, 0.025003422313483917, the coupon's May's, 0.02479808350444901;
/// QuantLib's engine prices it at 1,947.2600270718465, its pricer and nanoql at 1,846.091044619172.</item>
/// </list>
/// </summary>
public class YoYCapFloorEngineFixingTests
{
    private static readonly Date[] FlatDates = [new(1, Month.April, 2024), new(1, Month.April, 2034)];

    private static readonly double[] FlatRates = [0.03, 0.03];

    private static readonly Date[] SlopedDates = [new(1, Month.April, 2024), new(1, Month.April, 2028), new(1, Month.April, 2034)];

    private static readonly double[] SlopedRates = [0.03, 0.02, 0.02];

    [Fact]
    public void AFixedCapletPricesOnItsFixing()
    {
        // QuantLib's engine: 0.0
        AssertOptionlet(
            FlatDates, FlatRates, new Date(1, Month.July, 2023), new Date(1, Month.July, 2024), 3, CPIInterpolationType.Flat, 0, 0.032, 1085.739999233565);
    }

    [Fact]
    public void AFixingBeforeTheCurvesBaseDateIsPriced()
    {
        // QuantLib's engine: "date (April 1st, 2024) is before base date (May 1st, 2024)"
        AssertOptionlet(
            [new(1, Month.May, 2024), new(1, Month.May, 2034)],
            FlatRates,
            new Date(1, Month.July, 2023),
            new Date(1, Month.July, 2024),
            3,
            CPIInterpolationType.Flat,
            0,
            0.032,
            1085.739999233565);
    }

    [Fact]
    public void ALinearObservationInterpolatesTheForward()
    {
        // QuantLib's engine: 2896.7205361166657
        AssertOptionlet(
            SlopedDates, SlopedRates, new Date(15, Month.July, 2024), new Date(15, Month.July, 2025), 2, CPIInterpolationType.Linear, 0, 0.025, 2829.215810328031);
    }

    [Fact]
    public void FixingDaysMoveTheFixingDateAlone()
    {
        // QuantLib's engine: 1947.2600270718465
        AssertOptionlet(
            SlopedDates, SlopedRates, new Date(3, Month.July, 2025), new Date(3, Month.July, 2026), 2, CPIInterpolationType.Flat, 5, 0.025, 1846.091044619172);
    }

    private static void AssertOptionlet(
        Date[] curveDates,
        double[] curveRates,
        Date start,
        Date end,
        int lagMonths,
        CPIInterpolationType interpolation,
        int fixingDays,
        double strike,
        double expected)
    {
        EvaluationContext context = InflationFixture.Context();
        Calendar uk = new UnitedKingdom();
        var index = new YoYInflationIndex(new UKRPI(), InflationFixture.YoYCurve(curveDates, curveRates));
        var schedule = new Schedule([start, end], uk);
        ImmutableArray<CashFlow> leg = new YoYInflationLeg(schedule, uk, index, new Period(lagMonths, TimeUnit.Months), interpolation)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual365Fixed())
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(fixingDays)
            .ToLeg();
        ConstantYoYOptionletVolatility vol = ConstantYoYOptionletVolatility.Create(
            0.15,
            0,
            uk,
            BusinessDayConvention.ModifiedFollowing,
            new Actual365Fixed(),
            new Period(2, TimeUnit.Months),
            Frequency.Monthly,
            false,
            context);

        InstrumentResults results = YoYInflationBlackCapFloorEngine.Calculate(
            new YoYInflationCap(leg, [strike]), context, index, vol, InflationFixture.Nominal());

        double price = results.Result<ImmutableArray<double>>("optionletsPrice")[0];
        Assert.True(Tolerance.Within(expected, price), $"optionlet price {price}");
        Assert.True(Tolerance.Within(expected, results.NPV!.Value), $"NPV {results.NPV}");
    }
}
