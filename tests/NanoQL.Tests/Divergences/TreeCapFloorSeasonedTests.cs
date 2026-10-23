// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The tree cap engine on a seasoned cap (the S-sr plan's SSR6 (iv)): QuantLib's <c>DiscretizedCapFloor::mandatoryTimes</c>
/// keeps the started caplets' negative start times (<c>discretizedcapfloor.cpp:46-51</c>), where the discretized swap's
/// filters <c>t >= 0</c>, so the grid refuses them and the engine prices nothing: on the wheel, a 3-year semiannual cap
/// at 3% on Euribor 6M, started on 15 July 2024, its fixings stored at 3.5% and 3.25%, notional 1,000,000, on
/// Hull-White (0.05, 0.01) on a flat 3% curve on 15 January 2025, refuses "negative times not allowed" on the tree,
/// where the analytic engine prices it 8,985.844563131719. nanoql keeps the non-negative times, as the discretized swap
/// does, and pays a caplet fixed on or before the reference date its known amount. QuantLib's check: the analytic
/// engine's price, which the tree converges to; both value a caplet still to be fixed at its fixing (the plan's B1-1,
/// <c>CapletFixingExpiryTests</c>), where QuantLib's analytic price with those caplets on its five-argument bond option at
/// their fixings is 8,956.86225257607 on the wheel.
/// </summary>
public class TreeCapFloorSeasonedTests
{
    [Fact]
    public void TheSeasonedCapConvergesToTheAnalyticPrice()
    {
        var today = new Date(15, Month.January, 2025);
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var start = new Date(15, Month.July, 2024);
        var context = new EvaluationContext
        {
            EvaluationDate = today,
            Fixings = IndexFixings.Empty
                .AddFixing(index, index.FixingDate(start), 0.035)
                .AddFixing(index, new Date(13, Month.January, 2025), 0.0325),
        };
        var schedule = new Schedule(
            start, start + new Period(3, TimeUnit.Years), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, index)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual360())
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(2)
            .ToLeg(context);
        var cap = new Cap(leg, [0.03]);
        var model = new HullWhite(curve, 0.05, 0.01);

        double analytic = AnalyticCapFloorEngine.Calculate(cap, context, model).NPV!.Value;
        Assert.True(Harness.Tolerance.Within(8956.86225257607, analytic, 1e6), $"{analytic}");
        double e100 = (TreeCapFloorEngine.Calculate(cap, context, model, 100).NPV!.Value / analytic) - 1.0;
        double e400 = (TreeCapFloorEngine.Calculate(cap, context, model, 400).NPV!.Value / analytic) - 1.0;
        Assert.True(Math.Abs(e400) <= 1e-3, $"{e100:E}, {e400:E}");
        Assert.True(Math.Abs(e400) < Math.Abs(e100), $"{e100:E}, {e400:E}");
    }

    // QuantLib on the wheel, a 2-year semiannual cap at 3% in arrears on Euribor 6M from 15 October 2024, its first
    // caplet fixed on 11 April 2025, on Hull-White (0.1, 0.01) on a flat 4% curve on 15 January 2025, its coupons on a
    // Black pricer at 20%: the tree refuses "negative times not allowed", the first caplet's start, and the analytic
    // engine "negative time (-0.252055) given". nanoql's tree values each in-arrears caplet at its fixing, its start unread
    // (the plan's B2-1 and B3-1, CapletInArrearsTests), and converges to the cap's Gaussian integral, 20,582.494695037763
    // (the S-sr run's b31_seasoned.py).
    [Fact]
    public void ASeasonedCapInArrearsConvergesToItsGaussianIntegral()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var calendar = new TARGET();
        var schedule = new Schedule(
            new Date(15, Month.October, 2024), new Date(15, Month.October, 2026), new Period(6, TimeUnit.Months), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, new Euribor6M(curve))
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual360())
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(2)
            .InArrears()
            .ToLeg(context);
        var volatility = new ConstantOptionletVolatility(
            today, calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        leg = CouponPricerFunctions.SetCouponPricer(leg, new BlackIborCouponPricer(context, volatility));
        var cap = new Cap(leg, [0.03]);
        var model = new HullWhite(curve, 0.1, 0.01);

        double e200 = (TreeCapFloorEngine.Calculate(cap, context, model, 200).NPV!.Value / 20582.494695037763) - 1.0;
        double e1600 = (TreeCapFloorEngine.Calculate(cap, context, model, 1600).NPV!.Value / 20582.494695037763) - 1.0;
        Assert.True(Math.Abs(e1600) <= 2e-4, $"{e200:E}, {e1600:E}");
        Assert.True(Math.Abs(e1600) < Math.Abs(e200), $"{e200:E}, {e1600:E}");
    }
}
