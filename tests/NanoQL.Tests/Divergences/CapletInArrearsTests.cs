// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An in-arrears caplet on the short-rate cap engines: QuantLib's analytic and tree engines value a caplet still to be
/// fixed at its start on its accrual period, reading neither its later fixing nor its index's own period
/// (<c>analyticcapfloorengine.cpp:94-112</c>, <c>discretizedcapfloor.cpp:53-55</c>), so they price an in-arrears cap as
/// the cap in advance; its Monte Carlo engine forecasts the rate from the bond to the coupon's start, before the fixing
/// (<c>mchullwhiteengine.cpp:80-90</c>). On the wheel, a 1-year semiannual cap at 4% on Euribor 6M in arrears, from six
/// months after the spot date, two fixing days, notional 1,000,000, its coupons on a Black pricer at 20%, on Hull-White
/// (0.1, 0.01) on a flat 4% Actual/365 (Fixed) curve on 15 January 2025: the analytic engine 3,024.081283636271 and the
/// tree (200 steps) 3,019.994708839063, the in-advance cap's prices; QuantLib's Monte Carlo route (the port's before
/// the fix, which reproduces it on its vectors) 4,063.1343135221905 ± 6.317811808654881 (200,000 antithetic samples,
/// seed 42). No QuantLib route prices it, so the check is independent: each caplet's value at its fixing is a
/// one-dimensional Gaussian integral under the fixing's forward measure, 3,885.098792896677 by quadrature to about
/// 1e-15. nanoql's trees value an in-arrears caplet at its fixing on every model from three bonds, to its payment and
/// to its index period's start and end; its Monte Carlo engine forecasts on the index's period and values the payment
/// at the fixing; its analytic engine refuses it.
/// </summary>
public class CapletInArrearsTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());
    private static readonly HullWhite Model = new(Curve, 0.1, 0.01);

    // The Gaussian integrals: the cap and the floor at 4% for a year from six months after the spot date, and the cap
    // for two years from a year after it.
    [Fact]
    public void TheTreeConvergesToTheGaussianIntegral()
    {
        foreach ((string name, CapFloor capFloor, double integral) in new (string, CapFloor, double)[]
                 {
                     ("1Y cap", new Cap(Leg(6, 1), [0.04]), 3885.098792896677),
                     ("1Y floor", new Floor(Leg(6, 1), [0.04]), 3977.741703897591),
                     ("2Y cap", new Cap(Leg(12, 2), [0.04]), 9572.809568687382),
                 })
        {
            double e200 = (TreeCapFloorEngine.Calculate(capFloor, Context, Model, 200).NPV!.Value / integral) - 1.0;
            double e1600 = (TreeCapFloorEngine.Calculate(capFloor, Context, Model, 1600).NPV!.Value / integral) - 1.0;
            Assert.True(Math.Abs(e1600) <= 2e-4, $"{name}: {e200:E}, {e1600:E}");
            Assert.True(Math.Abs(e1600) < Math.Abs(e200), $"{name}: {e200:E}, {e1600:E}");
        }
    }

    [Fact]
    public void TheMonteCarloEngineAgreesWithTheGaussianIntegral()
    {
        var cap = new Cap(Leg(6, 1), [0.04]);
        InstrumentResults mc = MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(
            cap, Context, Model, false, true, 200000, null, null, 42);
        double error = mc.ErrorEstimate!.Value;
        Assert.True(Math.Abs(mc.NPV!.Value - 3885.098792896677) <= 2.0 * error, $"{mc.NPV} ± {error}");
        Assert.True(Math.Abs(4063.1343135221905 - 3885.098792896677) > 3.0 * error, $"{error}");
    }

    // The floor at 4%: its forward on the index's period from the curve, its payoff discounted from its payment,
    // 143.39689517106365, which both engines reach as the volatility vanishes.
    [Fact]
    public void AtZeroVolatilityTheValueIsTheDeterministicPayoff()
    {
        var floor = new Floor(Leg(6, 1), [0.04]);
        var model = new HullWhite(Curve, 0.1, 1e-7);
        double tree = TreeCapFloorEngine.Calculate(floor, Context, model, 200).NPV!.Value;
        double mc = MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(
            floor, Context, model, false, true, 2000, null, null, 42).NPV!.Value;
        Assert.True(Math.Abs((tree / 143.39689517106365) - 1.0) <= 1e-9, $"{tree}");
        Assert.True(Math.Abs((mc / 143.39689517106365) - 1.0) <= 1e-9, $"{mc}");
    }

    [Fact]
    public void TheAnalyticEngineRefusesIt()
    {
        var cap = new Cap(Leg(6, 1), [0.04]);
        NanoQLException e = Assert.Throws<NanoQLException>(() => AnalyticCapFloorEngine.Calculate(cap, Context, Model));
        Assert.Equal(
            "an in-arrears caplet still to be fixed (on January 15th, 2026) has no closed form: price it on the tree or the "
            + "Monte Carlo engine",
            e.Message);
    }

    private static ImmutableArray<CashFlow> Leg(int forwardMonths, int years)
    {
        var calendar = new TARGET();
        Date spot = calendar.Advance(Today, 2, TimeUnit.Days);
        Date start = calendar.Advance(spot, new Period(forwardMonths, TimeUnit.Months));
        var schedule = new Schedule(
            start, calendar.Advance(start, new Period(years, TimeUnit.Years)), new Period(6, TimeUnit.Months), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, new Euribor6M(Curve)).WithNotionals(1e6).InArrears().ToLeg(Context);
        var volatility = new ConstantOptionletVolatility(
            Today, calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        return CouponPricerFunctions.SetCouponPricer(leg, new BlackIborCouponPricer(Context, volatility));
    }
}
