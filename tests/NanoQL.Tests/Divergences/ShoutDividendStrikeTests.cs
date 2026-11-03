// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference shout engine with a dividend between the shout and expiry. QuantLib's inner value takes the
/// intrinsic value at the spot, the escrowed level less the dividend adjustment, but strikes the residual option at the
/// escrowed level (<c>fdmshoutloginnervaluecalculator.cpp:47-73</c>), handing the dividends' present value back: on
/// <c>testLargeDividendShoutNPV</c>'s option, a call at 80 on 100 with a dividend of 30 at three months, its engine
/// gives 24.90999 = 20 + 4.90999, a shout now whose residual is at the money on the escrowed 70, and its test's
/// reference, a shout without dividends up to the dividend date, prices the same handed-back residual. A shout at τ
/// pays max(S_T − K, S_τ − K) = (S_τ − K) + (S_T − S_τ)⁺, so the residual is struck at the spot S_τ. nanoql strikes it
/// there, its volatility read there; without dividends the two levels are one, and QuantLib's bits stand. The checks:
/// the payoff identity at t₀, and, independently, a binomial programme of the same stopping problem on the escrowed
/// process.
/// </summary>
public class ShoutDividendStrikeTests
{
    private static readonly Date Today = new(21, Month.February, 2021);
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AShoutNowIsTheIntrinsicValueAndACallStruckAtTheSpot()
    {
        // testLargeDividendShoutNPV's option is worth shouting now: (S₀ − K) plus Black's call struck at S₀ = 100 on the
        // escrowed forward 70, r = q = 0, over the six months; QuantLib's 24.90999
        double t = Dc.YearFraction(Today, Today + new Period(6, TimeUnit.Months));
        double shoutNow = 20.0 + BlackFormulaFunctions.BlackFormula(OptionType.Call, 100.0, 70.0, 0.25 * Math.Sqrt(t), 1.0);
        Assert.Equal(shoutNow, Engine(), 2e-3);
    }

    [Fact]
    public void TheValueIsABinomialProgrammesOfTheStoppingProblem()
    {
        // a Cox-Ross-Rubinstein tree of the escrowed level s, a martingale at r = q = 0, over the six months; at each
        // node the shout's value max(0, Black's call struck at S = s + 30 before the dividend date, s after, on s, plus
        // S − K), against continuing
        double t = Dc.YearFraction(Today, Today + new Period(6, TimeUnit.Months));
        double tDividend = Dc.YearFraction(Today, Today + new Period(3, TimeUnit.Months));
        const int steps = 2000;
        double dt = t / steps, u = Math.Exp(0.25 * Math.Sqrt(dt)), d = 1 / u, p = (1 - d) / (u - d);
        double[] values = new double[steps + 1];
        for (int j = 0; j <= steps; ++j)
        {
            values[j] = Exercise(70.0 * Math.Pow(u, (2 * j) - steps), t, t, tDividend);
        }

        for (int i = steps - 1; i >= 0; --i)
        {
            for (int j = 0; j <= i; ++j)
            {
                double s = 70.0 * Math.Pow(u, (2 * j) - i);
                values[j] = Math.Max((p * values[j + 1]) + ((1 - p) * values[j]), Exercise(s, i * dt, t, tDividend));
            }
        }

        Assert.Equal(values[0], Engine(), 5e-3);
    }

    // the shout's exercise value at the escrowed level s and time τ
    private static double Exercise(double s, double tau, double t, double tDividend)
    {
        double spot = tau <= tDividend ? s + 30.0 : s;
        double residual = tau < t
            ? BlackFormulaFunctions.BlackFormula(OptionType.Call, spot, s, 0.25 * Math.Sqrt(t - tau), 1.0)
            : Math.Max(s - spot, 0.0);
        return Math.Max(0.0, residual + (spot - 80.0));
    }

    // testLargeDividendShoutNPV's option on its grid
    private static double Engine()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, Dc), new FlatForward(Today, 0.0, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.25, Dc));
        ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([Today + new Period(3, TimeUnit.Months)], [30.0]);
        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Call, 80.0), new AmericanExercise(Today, Today + new Period(6, TimeUnit.Months)));
        return FdBlackScholesShoutEngine.Calculate(option, Context, process, dividends, 100, 400).NPV!.Value;
    }
}
