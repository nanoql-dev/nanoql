// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The continuous geometric Asian Heston engine's <c>Phi</c> and start date (the S-xeq plan's XER5 (xv), fixed under
/// R8). QuantLib's coefficient of w, <c>a4_ = logS0*qdcf/dcf − ρv₀/σ + ρκθτ/σ</c>
/// (<c>analytic_cont_geom_av_price_heston.cpp:245</c>), multiplies the log spot by the carry factor where log S_T's
/// constant adds log(D_q/D_r), and adds ρκθτ/σ where that constant, as QuantLib's own a3 for the average, subtracts it:
/// its <c>Phi(0, 1)</c>, the expected terminal spot, reads 81.92 against the forward 103.05 [executed: the planner's
/// <c>xp_c1.cpp asian</c>]. The prices read w = 0 only, so they are QuantLib's. And QuantLib's engine prices an option
/// with a start date as unseasoned from today, where QuantLib's Black-Scholes continuous engine refuses one; nanoql
/// refuses it with that engine's text. Checks: the forward; at a vanishing vol of vol, Black-Scholes's moments of the
/// average and the spot.
/// </summary>
public class HestonContinuousGeometricAsianTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(0.09, 1.15, 0.09, 0.39, -0.64)] // QuantLib's Phi(0, 1) 81.9186097307
    [InlineData(0.09, 3.0, 0.05, 0.8, 0.5)]
    [InlineData(0.04, 2.0, 0.04, 0.003, 0.0)]
    public void PhiAtTheSpotsArgumentIsTheForward(double v0, double kappa, double theta, double sigma, double rho)
    {
        AnalyticContinuousGeometricAveragePriceAsianHestonEngine engine = Engine(v0, kappa, theta, sigma, rho);
        // within the rounding of the series' 1/σ² terms at the smallest vol of vol
        Assert.Equal(100.0 * Math.Exp(0.03), engine.Phi(0, 1, 1.0).Real, 1e-9);
    }

    [Fact]
    public void AtAVanishingVolOfVolPhisMomentsAreBlackScholess()
    {
        // v₀ = θ = 0.04, vol of vol 0.003: the average's mean S₀ e^((r − q − σ²/2)/2 + σ²/6) and the spot's second moment
        // S₀² e^(2(r − q) + σ²) of a Black-Scholes market at a volatility of 0.2
        AnalyticContinuousGeometricAveragePriceAsianHestonEngine engine = Engine(0.04, 2.0, 0.04, 0.003, 0.0);
        Assert.Equal(100.0 * Math.Exp((0.5 * (0.03 - (0.04 / 2))) + (0.04 / 6)), engine.Phi(1, 0, 1.0).Real, 1e-7);
        Assert.Equal(1e4 * Math.Exp((2 * 0.03) + 0.04), engine.Phi(0, 2, 1.0).Real, 1e-3);
    }

    [Fact]
    public void ThePricesAreQuantLibs()
    {
        // QuantLib's 6.9101224984 (the planner's xp_c1.cpp asian)
        Assert.Equal(6.9101224984, Engine(0.09, 1.15, 0.09, 0.39, -0.64).Calculate(Option(null), Context).NPV!.Value, 1e-10);
    }

    [Fact]
    public void AnOptionWithAStartDateIsRefused()
    {
        // QuantLib's Heston engine prices it from today, unseasoned
        AnalyticContinuousGeometricAveragePriceAsianHestonEngine engine = Engine(0.09, 1.15, 0.09, 0.39, -0.64);
        var e = Assert.Throws<NanoQLException>(() => engine.Calculate(Option(Today - 30), Context));
        Assert.StartsWith("seasoned continuous geometric Asian options not yet supported", e.Message);
        Assert.Throws<NanoQLException>(() => engine.Calculate(Option(Today + 30), Context));
    }

    [Fact]
    public void AnAverageStartingTodayIsTheUnseasonedOne()
    {
        // the code review's O1-6: on curves dated today a start today is the model's time 0, QuantLib's own average
        AnalyticContinuousGeometricAveragePriceAsianHestonEngine engine = Engine(0.09, 1.15, 0.09, 0.39, -0.64);
        Assert.Equal(engine.Calculate(Option(null), Context).NPV!.Value, engine.Calculate(Option(Today), Context).NPV!.Value);
    }

    // the planner's market: S 100, r 0.05, q 0.02 on Actual/365F
    private static AnalyticContinuousGeometricAveragePriceAsianHestonEngine Engine(
        double v0, double kappa, double theta, double sigma, double rho) =>
        new(new HestonProcess(
            new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, v0, kappa, theta, sigma, rho));

    private static ContinuousAveragingAsianOption Option(Date? startDate)
    {
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var exercise = new EuropeanExercise(Today + 365);
        return startDate is Date start
            ? new ContinuousAveragingAsianOption(AverageType.Geometric, start, payoff, exercise)
            : new ContinuousAveragingAsianOption(AverageType.Geometric, payoff, exercise);
    }
}
