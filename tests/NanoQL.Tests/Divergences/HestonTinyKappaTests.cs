// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston density, the PDF engine and exponential fitting at a tiny κ. QuantLib's PDF engine window 8√(θt + (v₀ −
/// θ)(1 − e^{−κt})/κ) (<c>analyticpdfhestonengine.cpp:46-48</c>) and exponential fitting's average variance
/// (<c>exponentialfittinghestonengine.cpp:252</c>) lose the (v₀ − θ) term once 1 − e^{−κt} rounds to 0, and
/// <see cref="HestonRNDCalculator"/>'s density, which the PDF engine integrates, throws "Interval contains no more
/// machine number" at every x from κ = 1e-16, where its values agree to six digits from 1e-8 to 1e-14. nanoql takes
/// −expm1(−κt) below κt = 1e-2 in both windows, and raises the density's κ to 1e-12/t where κt is below 1e-12, the
/// density moving by about κt relative there, and its inverse distribution's starting guess takes the same κ, where
/// QuantLib's (1 − e^{−κt})/(tκ) is NaN at κ 0 and the inversion throws. The check: the density at that κ, bit for bit,
/// QuantLib's within its complex functions' last bits, QuantLib's analytic engine, and the distribution at the
/// inverse's quantiles.
/// </summary>
public class HestonTinyKappaTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly VanillaOption Call = new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void AtATinyKappaThePdfEnginePrices()
    {
        // QuantLib 1.44's PDF engine throws "Interval contains no more machine number" at kappa 1e-16 and 1e-20
        // (QuantLib-Python 1.44)
        HestonModel model = Model(1e-20);
        double analytic = AnalyticHestonEngine.Calculate(Call, Context, model, 192).NPV!.Value;
        Assert.Equal(19.77783250232107, analytic, 1e-13);
        Assert.Equal(analytic, AnalyticPDFHestonEngine.Calculate(Call, Context, model).NPV!.Value, 1e-5);
    }

    [Fact]
    public void BelowTheFloorTheDensityIsQuantLibsAtTheFloor()
    {
        // the density at kappa 1e-20 is the density at kappa 1e-12/t, bit for bit; QuantLib 1.44's HestonRNDCalculator
        // there (QuantLib-Python 1.44), within the complex functions' last bits
        var calculator = new HestonRNDCalculator(Model(1e-20).Process);
        var atTheFloor = new HestonRNDCalculator(Model(1e-12).Process);
        foreach ((double x, double quantLib) in new[] { (4.5, 0.7805470095696564), (4.605170185988092, 0.8089354908158081), (4.8, 0.7586820578313146) })
        {
            Assert.Equal(atTheFloor.Pdf(x, 1.0), calculator.Pdf(x, 1.0));
            Assert.Equal(quantLib, calculator.Pdf(x, 1.0), 1e-15);
        }
    }

    [Fact]
    public void ExponentialFittingIsContinuousAsKappaFalls()
    {
        // QuantLib 1.44 at kappa 1e-20: 19.777832868229584 against the analytic engine's 19.77783250232107, 3.7e-7 off
        // (QuantLib-Python 1.44)
        foreach (double kappa in new[] { 1e-8, 1e-14, 1e-16, 1e-20 })
        {
            HestonModel model = Model(kappa);
            double analytic = AnalyticHestonEngine.Calculate(Call, Context, model, 192).NPV!.Value;
            Assert.Equal(analytic, ExponentialFittingHestonEngine.Calculate(Call, Context, model).NPV!.Value, 1e-11);
        }
    }

    [Fact]
    public void BelowTheFloorTheInverseDistributionInverts()
    {
        // the process at kappa 0 (the model refuses it) and 1e-20, where the unfloored guess is NaN and 4.634 (QuantLib
        // 1.44, on the tree before the fold)
        foreach (double kappa in new[] { 0.0, 1e-20 })
        {
            var calculator = new HestonRNDCalculator(new HestonProcess(
                new FlatForward(Today, 0.03, new Actual365Fixed()), new FlatForward(Today, 0.01, new Actual365Fixed()), 100.0, 0.25, kappa, 0.01, 0.3, -0.5));
            foreach (double p in new[] { 0.1, 0.5, 0.9 })
            {
                Assert.Equal(p, calculator.Cdf(calculator.InvCdf(p, 1.0), 1.0), 1e-6);
            }
        }
    }

    // v0 0.25, theta 0.01, sigma 0.3, rho -0.5, r 3%, q 1%, spot 100
    private static HestonModel Model(double kappa) =>
        new(new HestonProcess(
            new FlatForward(Today, 0.03, new Actual365Fixed()), new FlatForward(Today, 0.01, new Actual365Fixed()), 100.0, 0.25, kappa, 0.01, 0.3, -0.5));
}
