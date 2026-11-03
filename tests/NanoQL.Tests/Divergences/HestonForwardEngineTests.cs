// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic Heston forward engine's propagator and outer integral. QuantLib's propagator e^(−(Bν + Λ)/2) ·
/// I(√(ΛBν)) underflows times overflows to NaN for resets just above its 1e-3 cut, at a vol of vol of 0.2 for resets of
/// 0.00101 to 0.005 years and forward-start prices of 1 to 7 days
/// (<c>analytichestonforwardeuropeanengine.cpp:218-231</c>) (QuantLib 1.44); at κ̂ = κ − ρσ = 0 its B is 0/0 and its R
/// 0·∞ (<c>:115-117</c>, <c>:222</c>). nanoql takes the propagator through QuantLib's exponentially weighted Bessel
/// function where QuantLib's product isn't finite and nonzero, and B through x/(−expm1(−x)) below |κ̂t| of 1e-2. That
/// exposes QuantLib's outer integral: its 128 nodes over [0, max(2, ...)] in the reset variance carry the propagator's
/// mass to 1.188 at a one-day reset (vol of vol 0.2), pricing 10.376, and where 2κθ &lt; σ² lose it, 0.979 at 182 days
/// (vol of vol 0.5), pricing 5.100. Where QuantLib's nodes miss the propagator's unit mass by more than 1e-6 nanoql
/// takes the expectation in closed form: QuantLib's characteristic function from the reset is affine in the reset
/// variance, so its expectation is the CIR transition's moment-generating function, each probability one Fourier
/// integral. Checks: the Monte Carlo forward Heston engine within three standard errors on resets of a day to six
/// months and R from 6 to 0.05, an independent quadrature of the closed form, the propagator's mass, QuantLib's values
/// where its product is finite and where its nodes carry the mass, continuity at κ̂ = 0 and at a reset today.
/// QuantLib's inner integral, 128 nodes to φ = 100, also fails short tenors (a one-day call at the money 0.297 against
/// the vanilla's 0.420, calls out of the money below 0): its probabilities stand only where within 1e-7 of the resolved
/// route's, the resolved integral on pieces scaled to the strike factor's period. Checks: the analytic Heston vanilla
/// at a reset today, an independent split into pieces, no call below 0 at short tenors.
/// </summary>
public class HestonForwardEngineTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(1.5, 0.2, -0.5, 1, 20000, 365, 10.37618934)] // R 6, QuantLib's nodes; QuantLib itself NaN
    [InlineData(1.5, 0.2, -0.5, 2, 20000, 365, 8.94143180)]
    [InlineData(1.5, 0.5, -0.5, 182, 60000, 100, 5.10032872)] // 2κθ < σ², R 0.96: QuantLib's nodes lose 2.1% of the mass
    [InlineData(1.0, 1.0, -0.7, 7, 60000, 365, double.NaN)] // R 0.16, a week
    [InlineData(1.0, 1.0, -0.7, 30, 60000, 365, double.NaN)] // R 0.16, a month
    [InlineData(0.5, 1.0, -0.5, 182, 100000, 365, 1.9807)] // R 0.08; QuantLib 1.44's value
    [InlineData(0.3125, 1.0, -0.5, 182, 100000, 365, double.NaN)] // R 0.05
    public void TheForwardStartPriceAgreesWithMonteCarlo(
        double kappa, double sigma, double rho, int resetDays, int samples, int stepsPerYear, double quantLibNodes)
    {
        HestonProcess process = Process(kappa, sigma, rho);
        ForwardVanillaOption option = Option(resetDays);

        double analytic = AnalyticHestonForwardEuropeanEngine.Calculate(option, Context, process).NPV!.Value;
        OneAssetOptionResults mc = new MakeMCForwardEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(process, 42)
            .WithStepsPerYear(stepsPerYear)
            .WithSamples(samples)
            .WithAntitheticVariate()
            .ToEngine()(option, Context);
        double band = 3 * mc.ErrorEstimate!.Value;
        Assert.True(Math.Abs(analytic - mc.NPV!.Value) <= band, $"{analytic} against {mc.NPV} ± {mc.ErrorEstimate}");
        if (!double.IsNaN(quantLibNodes))
        {
            Assert.True(Math.Abs(quantLibNodes - mc.NPV!.Value) > band, $"{quantLibNodes} against {mc.NPV} ± {mc.ErrorEstimate}");
        }
    }

    [Theory]
    [InlineData(1.5, 0.2, -0.5, 1, 8.690243296)] // R 6
    [InlineData(1.5, 0.2, -0.5, 2, 8.675963215)]
    [InlineData(1.5, 0.5, -0.5, 182, 5.168048679)] // R 0.96
    [InlineData(1.0, 1.0, -0.7, 7, 6.457763696)] // R 0.16
    [InlineData(1.0, 1.0, -0.7, 30, 5.964529139)]
    [InlineData(1.0, 1.0, -0.7, 182, 3.569174640)]
    [InlineData(0.5, 1.0, -0.5, 182, 3.119541691)] // R 0.08
    [InlineData(0.3125, 1.0, -0.5, 182, 2.848660495)] // R 0.05
    public void WhereQuantLibsNodesMissTheMassThePriceIsTheClosedFormsIndependentQuadrature(
        double kappa, double sigma, double rho, int resetDays, double independent)
    {
        // the closed form by Heston's 1993 probabilities in the little-trap form and scipy's adaptive quadrature, to
        // its own accuracy, about 1e-8
        double price = AnalyticHestonForwardEuropeanEngine.Calculate(Option(resetDays), Context, Process(kappa, sigma, rho)).NPV!.Value;
        Assert.Equal(independent, price, 3e-8);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void ThePriceIsContinuousAtAResetToday(double sigma)
    {
        // a year's tenor: resets of one and two days extrapolated linearly to a reset today, within a fiftieth of a day's
        // move of QuantLib's reset-today route (its vanilla price)
        HestonProcess process = Process(1.5, sigma, -0.5);
        double At(int resetDays) => AnalyticHestonForwardEuropeanEngine.Calculate(
            new ForwardVanillaOption(
                1.0, Today + resetDays, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + resetDays + 365)),
            Context,
            process).NPV!.Value;
        double p0 = At(0), p1 = At(1), p2 = At(2);
        Assert.True(Math.Abs((2 * p1) - p2 - p0) <= 0.02 * Math.Abs(p1 - p0), $"{(2 * p1) - p2} against {p0}, a day's move {p1 - p0}");
    }

    [Theory]
    [InlineData(0.00101)]
    [InlineData(0.002)]
    [InlineData(0.005)]
    public void ThePropagatorCarriesUnitMassWhereQuantLibsIsNaN(double resetTime)
    {
        // a vol of vol of 0.2: QuantLib's propagator NaN at these resets; 64-point Gauss-Legendre on 200 panels over
        // [0, 0.1], the density's support
        var engine = new AnalyticHestonForwardEuropeanEngine(Process(0.2));
        Assert.True(double.IsFinite(engine.Propagator(resetTime, 0.04)));
        var gl = new GaussLegendreIntegration(64);
        double mass = 0.0, w = 0.1 / 200;
        for (int k = 0; k < 200; ++k)
        {
            double lo = k * w, hi = lo + w;
            mass += gl.Evaluate(u => engine.Propagator(resetTime, (0.5 * w * u) + (0.5 * (lo + hi)))) * 0.5 * w;
        }

        Assert.Equal(1.0, mass, 1e-8);
    }

    [Theory]
    [InlineData(0.00101, 125.551)]
    [InlineData(0.002, 89.2346)]
    [InlineData(0.01, 39.9574)]
    public void ThePropagatorIsQuantLibsWhereItsProductIsFinite(double resetTime, double quantLib)
    {
        // a vol of vol of 0.5, QuantLib's values to six digits
        var engine = new AnalyticHestonForwardEuropeanEngine(Process(0.5));
        Assert.Equal(quantLib, engine.Propagator(resetTime, 0.04), 5e-6 * quantLib);
    }

    [Theory]
    [InlineData(3, 8.119213403)]
    [InlineData(7, 8.050980337)]
    public void TheForwardStartPriceIsQuantLibsWhereItsNodesCarryTheMass(int resetDays, double quantLib)
    {
        // a vol of vol of 0.5, QuantLib's values
        Assert.Equal(
            quantLib, AnalyticHestonForwardEuropeanEngine.Calculate(Option(resetDays), Context, Process(0.5)).NPV!.Value, 1e-9);
    }

    [Fact]
    public void ThePriceIsContinuousAtAZeroKappaHat()
    {
        // κ = ρσ: QuantLib's B is 0/0 and its R 0·∞
        double At(double kappaHat)
        {
            const double sigma = 0.2, rho = 0.5;
            var process = new HestonProcess(
                new FlatForward(Today, 0.03, A365), new FlatForward(Today, 0.01, A365), 100.0, 0.04, (rho * sigma) + kappaHat,
                0.04, sigma, rho);
            return AnalyticHestonForwardEuropeanEngine.Calculate(Option(182), Context, process).NPV!.Value;
        }

        double zero = At(0.0);
        Assert.True(double.IsFinite(zero));
        Assert.Equal(At(1e-9), zero, 1e-9);
        Assert.Equal(At(-1e-9), zero, 1e-9);
        Assert.True(At(-1e-9) < zero && zero < At(1e-9));
    }

    [Fact]
    public void AResetTodayIsTheAnalyticHestonVanilla()
    {
        // QuantLib's own case checks this at 5e-4 of the spot; here 2e-6
        HestonProcess process = Process(0.2);
        double forward = AnalyticHestonForwardEuropeanEngine.Calculate(Option(0), Context, process).NPV!.Value;
        double vanilla = AnalyticHestonEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365)), Context,
            new HestonModel(process)).NPV!.Value;
        Assert.Equal(vanilla, forward, 2e-6);
    }

    [Theory]
    [InlineData(1.0, 1.0, -0.7, 182, 30, 1.2, 0.049013333420887255)] // R 0.16; Gauss-Lobatto at 1e-14 threw
    [InlineData(1.0, 1.0, -0.7, 182, 30, 0.8, 20.072110169144644)]
    [InlineData(1.5, 0.5, -0.5, 182, 7, 0.5, 49.760840422951894)] // R 0.96
    public void ShortTenorsArePricedOnTheResolvedIntegral(
        double kappa, double sigma, double rho, int resetDays, int tenorDays, double moneyness, double independent)
    {
        // The same closed form on independent Gauss-Legendre pieces
        double price = AnalyticHestonForwardEuropeanEngine.Calculate(
            Forward(moneyness, resetDays, tenorDays), Context, Process(kappa, sigma, rho)).NPV!.Value;
        Assert.Equal(independent, price, 1e-8);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.2)]
    public void AtAResetTodayShortExpiriesAreTheAnalyticHestonVanilla(double sigma)
    {
        // QuantLib's inner integral, 128 nodes to φ = 100, prices a one-day at-the-money call at 0.297 against 0.420
        // and calls out of the money below 0; QuantLib's value stands where its probabilities agree within 1e-7, a
        // price within about 1e-7 of spot plus strike (1.4e-6 at 30 days, moneyness 0.8)
        HestonProcess process = Process(1.5, sigma, -0.5);
        foreach (int days in new[] { 1, 7, 30 })
        {
            foreach (double moneyness in new[] { 0.8, 1.0, 1.2, 2.0 })
            {
                double forward = AnalyticHestonForwardEuropeanEngine.Calculate(Forward(moneyness, 0, days), Context, process).NPV!.Value;
                double vanilla = AnalyticHestonEngine.Calculate(
                    new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0 * moneyness), new EuropeanExercise(Today + days)),
                    Context,
                    new HestonModel(process)).NPV!.Value;
                Assert.True(
                    Math.Abs(forward - vanilla) <= 1e-7 * 100.0 * (1.0 + moneyness),
                    $"{days} days, moneyness {moneyness}: {forward} against {vanilla}");
            }
        }
    }

    [Theory]
    [InlineData(1.5, 0.5, -0.5)] // R 0.96
    [InlineData(1.0, 1.0, -0.7)] // R 0.16
    [InlineData(0.3125, 1.0, -0.5)] // R 0.05
    public void NoCallIsPricedBelowZeroOnShortTenors(double kappa, double sigma, double rho)
    {
        // a stress grid at its short corner: QuantLib's routes price −0.2677 at a one-day reset and tenor
        HestonProcess process = Process(kappa, sigma, rho);
        foreach (int resetDays in new[] { 1, 30 })
        {
            foreach (int tenorDays in new[] { 1, 7 })
            {
                foreach (double moneyness in new[] { 0.5, 2.0 })
                {
                    double price = AnalyticHestonForwardEuropeanEngine.Calculate(
                        Forward(moneyness, resetDays, tenorDays), Context, process).NPV!.Value;
                    Assert.True(
                        double.IsFinite(price) && price >= -1e-12,
                        $"reset {resetDays}, tenor {tenorDays}, moneyness {moneyness}: {price}");
                }
            }
        }
    }

    // a forward-start call at a moneyness, its expiry the reset plus a tenor
    private static ForwardVanillaOption Forward(double moneyness, int resetDays, int tenorDays) =>
        new(moneyness, Today + resetDays, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + resetDays + tenorDays));

    // the market: S 100, r 0.03, q 0.01, v0 0.04, κ 1.5, θ 0.04, ρ −0.5 on Actual/365F
    private static HestonProcess Process(double sigma) => Process(1.5, sigma, -0.5);

    // the market at another κ, σ and ρ (R = 4κθ/σ²)
    private static HestonProcess Process(double kappa, double sigma, double rho) =>
        new(new FlatForward(Today, 0.03, A365), new FlatForward(Today, 0.01, A365), 100.0, 0.04, kappa, 0.04, sigma, rho);

    // an at-the-money forward-start call expiring in a year
    private static ForwardVanillaOption Option(int resetDays) =>
        new(1.0, Today + resetDays, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
}
