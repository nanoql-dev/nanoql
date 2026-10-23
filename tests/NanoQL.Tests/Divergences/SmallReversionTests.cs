// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The short-rate models' other members at small mean reversion (the S-sr plan's SSR6 (iii), SSR3's sweep). G2's
/// swaption pricing function (<c>g2.cpp:119-160</c>) cancels in its mux and sigmax: on the wheel, a one-period payer
/// swaption, which is (1 + Kτ) puts on its bond struck at 1/(1 + Kτ), prices 0.0038981761747141413 at a = b = 1e-4
/// against that bond option's 0.003898173326452401, 0.00017515607132449288 at 1e-6 against 0.003898519697671894, and at
/// 1e-7 refuses "root not bracketed". Hull-White's <c>convexityBias</c> (<c>hullwhite.cpp:150</c>) cuts (1 − e^(−ax))/a
/// at QL_EPSILON and cancels above it: 7.593680693951388e-05 at a = 1e-10, 7.594298930423405e-05 at 1e-12 and
/// 7.801188790257374e-05 at 1e-14, against its own a = 0 value 7.593678809059146e-05. G2's bond option reads
/// <c>sigmaP</c> (<c>g2.cpp:59-87</c>), which cancels likewise: 0.013016600554919378 at a = b = 1e-12 against
/// 0.01301666498797277 at 1e-8. nanoql's are written in SSR3's cancellation-free forms. QuantLib's check: its second
/// routes, the bond option for the swaption and the a = 0 branch for the bias, and, for the bond option, Black's formula
/// on the a → 0 limit of sigmaP, (s − t)√(t(σ² + η² + 2ρση)) at a = b.
/// </summary>
public class SmallReversionTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());

    [Fact]
    public void G2sOnePeriodSwaptionIsItsBondOption()
    {
        var calendar = new TARGET();
        Date exercise = calendar.Advance(Today, new Period(1, TimeUnit.Years));
        Date end = calendar.Advance(exercise, new Period(1, TimeUnit.Years));
        const double K = 0.04;
        var fixedSchedule = new Schedule(
            exercise, end, new Period(1, TimeUnit.Years), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        var floatSchedule = new Schedule(
            exercise, end, new Period(6, TimeUnit.Months), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        var swap = new VanillaSwap(
            SwapType.Payer, 1.0, fixedSchedule, K, new Actual365Fixed(), floatSchedule, new Euribor6M(Curve), 0.0,
            new Actual360(), Context);
        var swaption = new Swaption(swap, new EuropeanExercise(exercise));
        var dc = new Actual365Fixed();
        double t1 = dc.YearFraction(Today, exercise), t2 = dc.YearFraction(Today, end), tau = dc.YearFraction(exercise, end);

        foreach (double a in new[] { 1e-4, 1e-6, 1e-7, 1e-9 })
        {
            var model = new G2(Curve, a, 0.01, a, 0.008, -0.5);
            double bondOption = (1.0 + (K * tau)) * model.DiscountBondOption(OptionType.Put, 1.0 / (1.0 + (K * tau)), t1, t2);
            double price = G2SwaptionEngine.Calculate(swaption, Context, model, 6.0, 16).NPV!.Value;
            Assert.True(Math.Abs((price / bondOption) - 1.0) <= 1e-6, $"a = b = {a}: {price} against {bondOption}");
        }
    }

    [Fact]
    public void HullWhitesConvexityBiasAtSmallReversionIsItsLimit()
    {
        double limit = HullWhite.ConvexityBias(95.0, 1.0, 1.25, 0.01, 0.0);
        Assert.True(Math.Abs((limit / 7.593678809059146e-05) - 1.0) <= 1e-14, $"{limit}");
        foreach (double a in new[] { 1e-10, 1e-12, 1e-14 })
        {
            double bias = HullWhite.ConvexityBias(95.0, 1.0, 1.25, 0.01, a);
            Assert.True(Math.Abs((bias / limit) - 1.0) <= 1e-9, $"a = {a}: {bias} against {limit}");
        }
    }

    [Fact]
    public void G2sBondOptionAtSmallReversionIsBlacksOnTheLimit()
    {
        const double sigma = 0.01, eta = 0.008, rho = -0.5, t = 1.0, s = 5.0, strike = 0.85;
        double v = (s - t) * Math.Sqrt(t * ((sigma * sigma) + (eta * eta) + (2.0 * rho * sigma * eta)));
        double limit = BlackFormulaFunctions.BlackFormula(OptionType.Call, Curve.Discount(t) * strike, Curve.Discount(s), v);
        foreach (double a in new[] { 1e-12, 1e-10 })
        {
            double option = new G2(Curve, a, sigma, a, eta, rho).DiscountBondOption(OptionType.Call, strike, t, s);
            Assert.True(Math.Abs((option / limit) - 1.0) <= 1e-9, $"a = b = {a}: {option} against {limit}");
        }
    }
}
