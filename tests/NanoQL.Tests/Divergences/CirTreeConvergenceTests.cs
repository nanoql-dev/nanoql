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
/// The Cox-Ingersoll-Ross trees: since 1.23 QuantLib's tree reads the model's dynamics, <c>CoxIngersollRossProcess</c>
/// on the short rate itself (<c>coxingersollross.cpp:126-131</c>), whose variance depends on the state, but the
/// trinomial tree spaces its nodes by the process's standard deviation at its initial value, so its prices stall away
/// from the model's own analytic ones: on the wheel, a 3-year semiannual cap at 4% on Euribor 6M from six months after
/// 15 January 2025, its caplets fixed on their starts, notional 1,000,000, on a flat 4% curve, prices
/// 19,590.94789923626, 19,596.379468056322 and 19,571.725938834417 on CIR (r0 = θ = 0.04, k = 0.3, σ = 0.1) with 50,
/// 200 and 800 steps, 2.3% above the analytic engine's 19,147.73327146658; and 20,232.01252305427, 20,225.765498780183
/// and 20,193.198962616287 on extended CIR (θ = 0.04, k = 0.3, σ = 0.1, x0 = 0.04), 2.8% above 19,674.220099061884.
/// nanoql's trees are QuantLib's before 1.23, on y = √r, whose diffusion σ/2 is constant, the short rate y², the
/// extended model's shift fitted numerically on the tree. QuantLib's check: its own analytic engine, which the trees
/// converge to at first order. The caplets are fixed on their starts, where the analytic engine's option, which on CIR
/// keeps QuantLib's expiry at the start, and the tree's, at the fixing, expire alike; QuantLib's engines don't read a
/// caplet's fixing lag, so its prices are those of the two fixing days.
/// </summary>
public class CirTreeConvergenceTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());

    [Fact]
    public void TheCoxIngersollRossTreeConvergesToTheAnalyticPrice()
    {
        var model = new CoxIngersollRoss(0.04, 0.04, 0.3, 0.1);
        Converges(model, Curve, 19147.73327146658);
    }

    [Fact]
    public void TheExtendedCoxIngersollRossTreeConvergesToTheAnalyticPrice()
    {
        var model = new ExtendedCoxIngersollRoss(Curve, 0.04, 0.3, 0.1, 0.04);
        Converges(model, null, 19674.220099061884);
    }

    private static void Converges(CoxIngersollRoss model, YieldTermStructure? termStructure, double quantLibAnalytic)
    {
        var cap = new Cap(Leg(), [0.04]);
        double analytic = AnalyticCapFloorEngine.Calculate(cap, Context, model, termStructure).NPV!.Value;
        Assert.True(Harness.Tolerance.Within(quantLibAnalytic, analytic, 1e6), $"{analytic}");
        double[] errors = [.. new[] { 50, 200, 800 }.Select(n =>
            Math.Abs((TreeCapFloorEngine.Calculate(cap, Context, model, n, termStructure).NPV!.Value / analytic) - 1.0))];
        Assert.True(errors[2] <= 2e-3, $"errors {string.Join(", ", errors)}");
        Assert.True(errors[1] < errors[0] && errors[2] < errors[1], $"errors {string.Join(", ", errors)}");
    }

    private static ImmutableArray<CashFlow> Leg()
    {
        var calendar = new TARGET();
        Date start = calendar.Advance(Today, new Period(6, TimeUnit.Months));
        var schedule = new Schedule(
            start, start + new Period(3, TimeUnit.Years), new Period(6, TimeUnit.Months), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        return new IborLeg(schedule, new Euribor6M(Curve))
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual360())
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(0)
            .ToLeg(Context);
    }
}
