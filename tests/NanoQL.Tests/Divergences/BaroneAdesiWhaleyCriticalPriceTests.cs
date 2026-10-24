// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Barone-Adesi-Whaley's critical price where its Newton iteration leaves its range (the S-van plan's VR4 (vii)). A
/// put's step can leave (0, K) and a call's seed or step (K, ∞), and QuantLib's next Black formula throws "forward +
/// displacement (0 + 0) must be positive" (<c>baroneadesiwhaleyengine.cpp:59-130</c>); Ju's engine calls the same
/// critical price (<c>juquadraticengine.cpp:126</c>) and throws with it [executed on the wheel:
/// <c>scratchpad\c2\oracle_c2.py</c>, section baw]. nanoql takes the root of QuantLib's own equation over the range, by
/// Brent's method. QuantLib's check: the equation's residual at the root, within QuantLib's own tolerance, and its FD
/// engine (2,000 × 2,000) on the option. Where Newton stays in range the critical price is QuantLib's, bit for bit (the
/// engines' vectors).
/// </summary>
public class BaroneAdesiWhaleyCriticalPriceTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(100.0, 1000.0, 0.10, 0.05, 3650, 900.0000000000001, 1e-9)] // QuantLib throws
    [InlineData(100.0, 100.0, 0.50, 0.01, 30, 0.003496218542474376, 0.1)] // QuantLib throws
    public void APutWhoseNewtonStepLeavesZeroToKIsPricedOnTheEquationsRoot(
        double spot, double strike, double r, double vol, int days, double fd, double relativeTolerance)
    {
        // the root solves QuantLib's equation; the value is the FD engine's, deep in the money exactly, and at 30 days
        // within the approximation's own error: 0.00367 beside FD's 0.00350, as the call below
        GeneralizedBlackScholesProcess process = Process(spot, 0.0, r, vol);
        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        AssertRoot(payoff, process, Today + days);
        double value = BaroneAdesiWhaleyApproximationEngine.Calculate(Option(payoff, days), Context, process).NPV!.Value;
        Assert.True(Math.Abs(value - fd) <= relativeTolerance * fd, $"{value} against QuantLib's FD {fd}");
    }

    [Fact]
    public void ACallWhoseSeedLeavesKToInfinityIsPricedOnTheEquationsRoot()
    {
        // r 0, q 10%, volatility 2%, five years: h > 0 takes the seed to -1.7e88 and QuantLib throws. The value,
        // 0.0732, is within the approximation's own error of QuantLib's FD 0.0696: at volatilities of 15% and 20%,
        // where Newton stays in range, Barone-Adesi-Whaley reads 3.6306 and 6.1409 against FD's 3.8449 and 6.3907
        GeneralizedBlackScholesProcess process = Process(100.0, 0.10, 0.0, 0.02);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        AssertRoot(payoff, process, Today + 1825);
        double value = BaroneAdesiWhaleyApproximationEngine.Calculate(Option(payoff, 1825), Context, process).NPV!.Value;
        Assert.True(Math.Abs(value - 0.06958812470326763) <= 0.1 * 0.06958812470326763, $"{value}");
    }

    [Fact]
    public void JusEngineSharesTheRoot()
    {
        // QuantLib's Ju engine throws on the deep put; on the equation's root it gives the FD engine's 900
        GeneralizedBlackScholesProcess process = Process(100.0, 0.0, 0.10, 0.05);
        var payoff = new PlainVanillaPayoff(OptionType.Put, 1000.0);
        double value = JuQuadraticApproximationEngine.Calculate(Option(payoff, 3650), Context, process).NPV!.Value;
        Assert.True(Math.Abs(value - 900.0000000000001) <= 1e-9 * 900.0, $"{value}");
    }

    // QuantLib's equation LHS − RHS at the critical price nanoql returns, within QuantLib's tolerance 1e-6 of the strike,
    // the root inside the range
    private static void AssertRoot(StrikedTypePayoff payoff, GeneralizedBlackScholesProcess process, Date expiry)
    {
        double rfD = process.RiskFreeRate.Discount(expiry), dD = process.DividendYield.Discount(expiry);
        double variance = process.BlackVolatility.BlackVariance(expiry, payoff.Strike);
        double si = BaroneAdesiWhaleyApproximationEngine.CriticalPrice(payoff, rfD, dD, variance);
        double strike = payoff.Strike;
        double n = 2.0 * Math.Log(dD / rfD) / variance;
        double k = -2.0 * Math.Log(rfD) / (variance * (1.0 - rfD));
        if (rfD == 1.0)
        {
            k = 2.0 / variance;
        }

        var cumNormalDist = new CumulativeNormalDistribution();
        double forward = si * dD / rfD;
        double d1 = (Math.Log(forward / strike) + (0.5 * variance)) / Math.Sqrt(variance);
        double black = BlackFormulaFunctions.BlackFormula(payoff.OptionType, strike, forward, Math.Sqrt(variance)) * rfD;
        double root = Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4 * k));
        double residual = payoff.OptionType == OptionType.Call
            ? si - strike - (black + ((1 - (dD * cumNormalDist.Evaluate(d1))) * si / ((-(n - 1.0) + root) / 2)))
            : strike - si - (black - ((1 - (dD * cumNormalDist.Evaluate(-d1))) * si / ((-(n - 1.0) - root) / 2)));
        Assert.True(Math.Abs(residual) / strike <= 1e-6, $"residual {residual} at {si}");
        Assert.True(payoff.OptionType == OptionType.Call ? si > strike : si > 0.0 && si < strike, $"{si}");
    }

    private static GeneralizedBlackScholesProcess Process(double spot, double q, double r, double vol) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(Today, q, A365), new FlatForward(Today, r, A365), new BlackConstantVol(Today, new NullCalendar(), vol, A365));

    private static VanillaOption Option(StrikedTypePayoff payoff, int days) =>
        new(payoff, new AmericanExercise(Today, Today + days));
}
