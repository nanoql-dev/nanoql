// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The FD Heston double knock-in (a refusal lifted). QuantLib refuses "only Knock-Out double barrier options are
/// supported" (<c>fdhestondoublebarrierengine.cpp:52</c>), where its single-barrier FD Heston engine prices knock-ins
/// as vanilla + rebate − knock-out (<c>fdhestonbarrierengine.cpp:165-196</c>). nanoql prices the double knock-in so:
/// QuantLib's FD Heston vanilla less its double knock-out without a rebate, plus the rebate, paid at expiry where no
/// barrier is touched, as R × the engine's cash-or-nothing knock-out at strike 0 (the analytic engines' convention),
/// every leg with the knock-out's leverage function and mixing factor. Checks: at a vanishing vol of vol, QuantLib's
/// own legs, 11.124212 − 1.897300 = 9.226912, and its analytic double knock-in 9.242178, the FD grid's 0.17%; the
/// binary leg against QuantLib's analytic double-barrier binary engine; and parity on a stochastic-local model.
/// </summary>
public class FdHestonDoubleKnockInTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, A365);
    private static readonly EuropeanExercise Exercise = new(Today + 365);
    private static readonly PlainVanillaPayoff Call = new(OptionType.Call, 100.0);

    // the Black-Scholes limit: v0 = θ = 0.0625 (σ 25%), κ 5, a vol of vol of 1e-4
    private static readonly HestonModel BlackScholesLimit = new(new HestonProcess(RiskFree, Dividends, 100.0, 0.0625, 5.0, 0.0625, 1e-4, 0.0));

    private static readonly GeneralizedBlackScholesProcess BlackScholes = new BlackScholesMertonProcess(
        100.0, Dividends, RiskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    [Fact]
    public void WithoutARebateTheKnockInIsTheVanillaLessTheKnockOut()
    {
        double value = FdHestonDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockIn, 80.0, 130.0, 0.0, Call, Exercise), Context, BlackScholesLimit, 200, 400, 50).NPV!.Value;
        double analytic = AnalyticDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockIn, 80.0, 130.0, 0.0, Call, Exercise), Context, BlackScholes).NPV!.Value;
        Assert.Equal(9.242178, analytic, 1e-6);
        Assert.Equal(9.226912, value, 1e-6);
        Assert.Equal(analytic, value, 2e-3 * analytic);
    }

    [Fact]
    public void TheRebateIsTheBinaryKnockOut()
    {
        double Price(DoubleBarrierType type, double rebate, StrikedTypePayoff payoff) => FdHestonDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(type, 80.0, 130.0, rebate, payoff, Exercise), Context, BlackScholesLimit, 100, 200, 50).NPV!.Value;
        var unitCash = new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0);
        double rebatePart = Price(DoubleBarrierType.KnockIn, 2.0, Call) - Price(DoubleBarrierType.KnockIn, 0.0, Call);
        Assert.Equal(2.0 * Price(DoubleBarrierType.KnockOut, 0.0, unitCash), rebatePart, 1e-12);

        // the grid's 0.7%
        double analytic = AnalyticDoubleBarrierBinaryEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockOut, 80.0, 130.0, 0.0, unitCash, Exercise), Context, BlackScholes).NPV!.Value;
        Assert.Equal(2.0 * analytic, rebatePart, 1e-2 * rebatePart);
    }

    [Fact]
    public void EveryLegReadsTheLeverageFunction()
    {
        var model = new HestonModel(new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 2.0, 0.04, 0.5, -0.5));
        var leverage = new LocalConstantVol(Today, 0.8, A365);
        double Price(DoubleBarrierType type, double rebate, StrikedTypePayoff payoff) => FdHestonDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(type, 80.0, 130.0, rebate, payoff, Exercise), Context, model, 50, 100, 25, 0, null, leverage).NPV!.Value;
        double vanilla = FdHestonVanillaEngine.Calculate(
            new VanillaOption(Call, Exercise), Context, model, 50, 100, 25, 0, null, leverage).NPV!.Value;
        Assert.Equal(vanilla, Price(DoubleBarrierType.KnockIn, 0.0, Call) + Price(DoubleBarrierType.KnockOut, 0.0, Call), 1e-12);

        double noTouch = Price(DoubleBarrierType.KnockOut, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0));
        Assert.Equal(2.0 * noTouch, Price(DoubleBarrierType.KnockIn, 2.0, Call) - Price(DoubleBarrierType.KnockIn, 0.0, Call), 1e-12);
    }
}
