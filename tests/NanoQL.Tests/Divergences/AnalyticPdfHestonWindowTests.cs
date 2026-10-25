// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic PDF Heston engine's integration window (the S-hes plan's HR7 (vii), under R8). QuantLib centres the
/// window on x₀ + ln(D_r/D_q) (<c>analyticpdfhestonengine.cpp:54</c>), the forward's mirror about the spot, where the
/// density of ln S_T centres on ln F = x₀ + ln(D_q/D_r), as <see cref="NanoQL.Methods.HestonRNDCalculator"/> reads it: at
/// a carry of 10% over ten years with v₀ = θ = 0.0025 the window misses the density and the call at 100 prices 0.00025
/// where the analytic engine gives 63.24. nanoql centres the window on ln F, QuantLib's window where the carry is 0.
/// The check: QuantLib's analytic engine.
/// </summary>
public class AnalyticPdfHestonWindowTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AtAHighCarryThePdfEngineMatchesTheAnalyticEngine()
    {
        // QuantLib 1.44's PDF engine prices 0.0004589966081416726, 0.0002494372667844883 and 0.0 [executed on the wheel]
        HestonModel model = Model(0.10, 0.0);
        foreach ((double strike, double analytic) in new[] { (80.0, 70.59382410396654), (100.0, 63.242280430033674), (150.0, 44.86448294494562) })
        {
            VanillaOption call = Call(strike, 10);
            Assert.Equal(analytic, AnalyticHestonEngine.Calculate(call, Context, model, 192).NPV!.Value, 1e-12);
            Assert.Equal(analytic, AnalyticPDFHestonEngine.Calculate(call, Context, model).NPV!.Value, 1e-4);
        }
    }

    [Fact]
    public void AtANegativeCarryThePdfEngineMatchesTheAnalyticEngine()
    {
        // q 10%, r 0, ten years, the forward 36.79: QuantLib 1.44's PDF engine, its window wholly off the density, prices
        // 0.0006561169490718366, 0.0005782569470985725, 0.0005263502791163967 and 0.00046146694413867696 [executed on
        // the wheel]
        HestonModel model = Model(0.0, 0.10);
        foreach ((double strike, double analytic) in new[]
                 {
                     (30.0, 7.010163853540327), (36.0, 2.6692148013603183), (40.0, 1.1195649214386618), (45.0, 0.31247438397606886),
                 })
        {
            VanillaOption call = Call(strike, 10);
            Assert.Equal(analytic, AnalyticHestonEngine.Calculate(call, Context, model, 192).NPV!.Value, 1e-12);
            Assert.Equal(analytic, AnalyticPDFHestonEngine.Calculate(call, Context, model).NPV!.Value, 1e-5);
        }
    }

    // v0 = theta 0.0025, kappa 1, sigma 0.05, rho 0, spot 100
    private static HestonModel Model(double r, double q) =>
        new(new HestonProcess(new FlatForward(Today, r, new Actual365Fixed()), new FlatForward(Today, q, new Actual365Fixed()), 100.0, 0.0025, 1.0, 0.0025, 0.05, 0.0));

    private static VanillaOption Call(double strike, int years) =>
        new(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + new Period(years, TimeUnit.Years)));
}
