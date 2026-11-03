// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A coupon between its fixing and its start on the trees. QuantLib's discretized cap knows a caplet by its start: it
/// values the caplet as an option on its bond whenever the start is on the grid (<c>discretizedcapfloor.cpp:55</c>) and
/// reads the fixing only when the start is past (<c>:91</c>); its discretized swap likewise knows a floating coupon by
/// its reset, the accrual start (<c>discretizedswap.cpp:28-33</c>, <c>:81-90</c>). So between a coupon's fixing and its
/// start the trees ignore the fixing. nanoql knows a caplet, and a swap's coupon behind the tree swap engine and
/// <see cref="DiscretizedSwap"/>'s public constructor, by its fixing date, and pays its amount; the tree swaption keeps
/// QuantLib's start-date rule, its par convention at exercise. QuantLib's check: the analytic cap engine and the
/// discounting swap engine, which the trees then converge to or equal; and QuantLib's own tree swaption.
/// </summary>
public class TreeKnownCouponTests
{
    // QuantLib on the wheel, a 3-year semiannual cap at 3% on Euribor 6M from 17 March 2025, notional 100, its first
    // fixing (13 March) stored at 5%, on Hull-White (0.1, 0.01) on a flat 4% curve from the evaluation date: one
    // business day after the fixing, the analytic engine 3.561453558384388 and the tree (200 steps) 3.0539157443699025;
    // on the fixing date, 3.5615917095494933 and 3.054109220073771. Both of nanoql's engines value the caplets still to
    // be fixed at their fixings (CapletFixingExpiryTests): QuantLib's analytic price with those caplets on its
    // five-argument bond option at their fixings is 3.55973215363614 and 3.559869850972133 on the wheel.
    [Fact]
    public void ACapAfterItsFirstFixingIsTheAnalyticEngines()
    {
        foreach ((Date evaluation, double atFixing) in new[]
                 {
                     (new Date(14, Month.March, 2025), 3.55973215363614),
                     (new Date(13, Month.March, 2025), 3.559869850972133),
                 })
        {
            var curve = new FlatForward(evaluation, 0.04, new Actual365Fixed());
            var index = new Euribor6M(curve);
            var start = new Date(17, Month.March, 2025);
            var context = new EvaluationContext
            {
                EvaluationDate = evaluation,
                Fixings = IndexFixings.Empty.AddFixing(index, new Date(13, Month.March, 2025), 0.05),
            };
            var cap = new Cap(Leg(start, 3, index, 100.0, context), [0.03]);
            var model = new HullWhite(curve, 0.1, 0.01);
            double analytic = AnalyticCapFloorEngine.Calculate(cap, context, model).NPV!.Value;
            Assert.True(Tolerance.Within(atFixing, analytic), $"{analytic}");
            double tree = TreeCapFloorEngine.Calculate(cap, context, model, 200).NPV!.Value;
            Assert.True(Math.Abs((tree / analytic) - 1.0) <= 2e-3, $"{evaluation}: {tree} against {analytic}");
        }
    }

    // QuantLib on the C++ harness, a 5-year payer at 3% from the spot date on Euribor 6M, notional 1,000,000, its first
    // fixing (the evaluation date, 15 January 2025) stored at 5%, on a flat 2.7% curve that discounts and forecasts:
    // the tree swap engine on Hull-White (0.05, 0.01) -12,075.466363109406 at 100 and 400 steps, the discounting engine
    // -572.4283633555751.
    [Fact]
    public void ASwapAfterItsFirstFixingIsTheDiscountingEngines()
    {
        (VanillaSwap swap, EvaluationContext context, YieldTermStructure curve) = SpotSwap(SwapType.Payer);
        double discounting = DiscountingSwapEngine.Calculate(swap, context, curve).NPV!.Value;
        Assert.True(Tolerance.Within(-572.4283633555751, discounting, 1e6), $"{discounting}");
        double tree = TreeVanillaSwapEngine.Calculate(swap, context, new HullWhite(curve, 0.05, 0.01), 400).NPV!.Value;
        Assert.True(Math.Abs(tree - discounting) <= 1e-6 * 1e6, $"{tree} against {discounting}");
    }

    // QuantLib on the wheel: the receiver swaption on that swap, exercised on the spot date, on the tree (100 and 400
    // steps), 12,075.466363109406: the coupon valued as a forward on the tree's bonds, as at the par convention of
    // exercise.
    [Fact]
    public void TheTreeSwaptionKeepsQuantLibsStartDateRule()
    {
        (VanillaSwap swap, EvaluationContext context, YieldTermStructure curve) = SpotSwap(SwapType.Receiver);
        var swaption = new Swaption(swap, new EuropeanExercise(swap.FloatingSchedule.Dates[0]));
        double price = TreeSwaptionEngine.Calculate(swaption, context, new HullWhite(curve, 0.05, 0.01), 100).NPV!.Value;
        Assert.True(Tolerance.Within(12075.466363109406, price, 1e6), $"{price}");
    }

    [Fact]
    public void DiscretizedSwapsPublicConstructorKnowsACouponByItsFixingDate()
    {
        (VanillaSwap swap, EvaluationContext context, YieldTermStructure curve) = SpotSwap(SwapType.Payer);
        var model = new HullWhite(curve, 0.05, 0.01);
        var asset = new DiscretizedSwap(swap, curve.ReferenceDate, curve.DayCounter, context);
        ImmutableArray<double> times = asset.MandatoryTimes();
        Lattice lattice = model.Tree(new TimeGrid([.. times], 400));
        asset.Initialize(lattice, times.Max());
        asset.Rollback(0.0);
        double engine = TreeVanillaSwapEngine.Calculate(swap, context, model, lattice).NPV!.Value;
        Assert.Equal(engine, asset.PresentValue());
        Assert.True(Math.Abs(asset.PresentValue() - (-572.4283633555751)) <= 1e-6 * 1e6, $"{asset.PresentValue()}");
    }

    private static (VanillaSwap Swap, EvaluationContext Context, YieldTermStructure Curve) SpotSwap(SwapType type)
    {
        var today = new Date(15, Month.January, 2025);
        var curve = new FlatForward(today, 0.027, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var context = new EvaluationContext { EvaluationDate = today, Fixings = IndexFixings.Empty.AddFixing(index, today, 0.05) };
        var calendar = new TARGET();
        Date spot = calendar.Advance(today, 2, TimeUnit.Days);
        Date maturity = spot + new Period(5, TimeUnit.Years);
        var fixedSchedule = new Schedule(
            spot, maturity, new Period(1, TimeUnit.Years), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var floatSchedule = new Schedule(
            spot, maturity, new Period(6, TimeUnit.Months), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var swap = new VanillaSwap(
            type, 1e6, fixedSchedule, 0.03, new Thirty360(Thirty360Convention.BondBasis), floatSchedule, index, 0.0,
            new Actual360(), context);
        return (swap, context, curve);
    }

    private static ImmutableArray<CashFlow> Leg(Date start, int years, IborIndex index, double notional, EvaluationContext context)
    {
        var schedule = new Schedule(
            start, start + new Period(years, TimeUnit.Years), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        return new IborLeg(schedule, index)
            .WithNotionals(notional)
            .WithPaymentDayCounter(new Actual360())
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(2)
            .ToLeg(context);
    }
}
