// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The forward Hull-White process at small mean reversion: QuantLib's <c>M_T</c> (<c>hullwhiteprocess.cpp:134-146</c>),
/// σ²/a²[(1 − e^(−u)) − ½(e^(−v) − e^(−v−2u))], cuts at QL_EPSILON and above it cancels two terms of order σ²(t − s)/a:
/// on the wheel, on a flat 4% curve with σ = 0.01, M_T(0, 0.5, 10) is 0.005551115122216288 at a = 1e-9, 0 at 1e-12 and
/// 5,551,115,123.125778 at 1e-15, against its limit σ²(t − s)[(T − t)
/// + ½(t − s)] = 4.875e-4, so the half-year expectation from 0.04 is −5,551,115,123.085763 at 1e-15; its drift is NaN
/// at a = 0; and the Monte Carlo cap engine that reads it prices a 5-year cap on Euribor 6M at 3% (notional 1,000,000,
/// on a flat 2.7% discount curve) 13,362.29 ± 129.78 at a = 1e-9, 10,000 antithetic samples, seed 42, on the harness,
/// against the analytic engine's 19,600.69. nanoql's M_T is σ²(t − s)f(u)[(T − t)f(v) + ½e^(−v)(t − s)f(u)], f(x) = (1
/// − e^(−x))/x, every term positive. QuantLib's check: the limit; QuantLib's closed form at ordinary a,
/// 3.0368938327613056e-4 at a = 0.1, within the tolerance; and the analytic engine for the Monte Carlo one.
/// </summary>
public class HullWhiteForwardProcessSmallReversionTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());

    [Fact]
    public void M_TAtSmallReversionIsItsLimit()
    {
        const double limit = 1e-4 * 0.5 * (9.5 + 0.25);
        foreach (double a in new[] { 1e-9, 1e-12, 1e-15 })
        {
            double m = new HullWhiteForwardProcess(Curve, a, 0.01).M_T(0.0, 0.5, 10.0);
            Assert.True(Math.Abs((m / limit) - 1.0) <= 1e-8, $"a = {a}: {m}");
        }

        double ordinary = new HullWhiteForwardProcess(Curve, 0.1, 0.01).M_T(0.0, 0.5, 10.0);
        Assert.True(Harness.Tolerance.Within(3.0368938327613056e-4, ordinary), $"{ordinary}");
    }

    [Fact]
    public void TheDriftAtZeroReversionIsItsLimit()
    {
        // sigma^2 t + f'(t) - (T - t) sigma^2 at t = 1, T = 10, f' QuantLib's difference quotient
        double drift = new HullWhiteForwardProcess(Curve, 0.0, 0.01).WithForwardMeasureTime(10.0).Drift(1.0, 0.04);
        double limit = HullWhiteProcessSmallReversionTests.Limit(1.0) - (9.0 * 0.01 * 0.01);
        Assert.True(Math.Abs(drift - limit) <= 1e-17, $"{drift} against {limit}");
    }

    [Fact]
    public void TheMonteCarloCapAtSmallReversionIsTheAnalyticOnes()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var discount = new FlatForward(Today, 0.027, new Actual365Fixed());
        var forecast = new FlatForward(Today, 0.031, new Actual365Fixed());
        var calendar = new TARGET();
        Date spot = calendar.Advance(Today, 2, TimeUnit.Days);
        var schedule = new Schedule(
            spot, calendar.Advance(spot, new Period(5, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing),
            new Period(6, TimeUnit.Months), calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward, false, context);
        var index = new Euribor6M(forecast);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, index)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(index.DayCounter)
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(2)
            .ToLeg(context);
        var cap = new Cap(leg, [0.03]);
        var model = new HullWhite(discount, 1e-9, 0.01);

        double analytic = AnalyticCapFloorEngine.Calculate(cap, context, model).NPV!.Value;
        InstrumentResults mc = MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(
            cap, context, model, false, true, 10000, null, null, 42);
        Assert.True(
            Math.Abs(mc.NPV!.Value - analytic) <= 3.0 * mc.ErrorEstimate!.Value,
            $"{mc.NPV} ± {mc.ErrorEstimate} against {analytic}");
    }
}
