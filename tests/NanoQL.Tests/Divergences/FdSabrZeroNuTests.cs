// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference SABR engine at ν = 0 (the S-van plan's VR4 (xxxi), VD28). QuantLib's parameter check accepts
/// ν = 0 (<c>sabr.cpp:157</c>), but the log volatility's grid collapses and its concentrating mesher refuses ("end must be
/// larger than start", <c>fdsabrvanillaengine.cpp:87-95</c>) [read]; so it does at a tiny positive ν, where the grid's
/// span is a few ulps of ln α or none, and a little above that it returns NaN [executed by the reviewer, C4's round 1];
/// at ν = 1e-8 it prices as the CEV engines. SABR at ν = 0 is the CEV process with α its volatility, and wherever ν√T is
/// at most 1e-10·max(1, |ln α|) nanoql's engine prices on the finite-difference CEV engine with its own grids, damping
/// steps, scaling, ε and scheme. QuantLib's check: its FD CEV engine, and its FD SABR engine at ν = 1e-8.
/// </summary>
public class FdSabrZeroNuTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Theory]
    [InlineData(80.0)]
    [InlineData(100.0)]
    [InlineData(120.0)]
    public void AtZeroNuItIsTheCevEngine(double strike)
    {
        // f0 100, β 0.5, α 3 (30% at the forward), a year, r 3%
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + 365));
        double sabr = FdSabrVanillaEngine.Calculate(option, Context, 100.0, 3.0, 0.5, 0.0, 0.3, curve).NPV!.Value;
        double cev = FdCEVVanillaEngine.Calculate(
            option, Context, 100.0, 3.0, 0.5, curve, 50, 400, 0, 1.0, 1e-4, FdmSchemeDesc.Hundsdorfer()).NPV!.Value;
        Assert.Equal(cev, sabr);

        // QuantLib's checks: its FD SABR engine at ν = 1e-8, and the analytic CEV engine
        double nearZero = FdSabrVanillaEngine.Calculate(option, Context, 100.0, 3.0, 0.5, 1e-8, 0.3, curve).NPV!.Value;
        double analytic = AnalyticCEVEngine.Calculate(option, Context, 100.0, 3.0, 0.5, curve).NPV!.Value;
        Assert.Equal(nearZero, sabr, 2e-2);
        Assert.Equal(analytic, sabr, 1e-2);
    }

    [Theory]
    [InlineData(30, 5e-15)] // QuantLib: NaN
    [InlineData(30, 1e-15)] // QuantLib: "end must be larger than start"
    [InlineData(365, 1e-15)] // NaN
    [InlineData(365, 2e-16)] // refused
    [InlineData(3650, 5e-17)] // refused
    public void AtATinyNuItIsTheCevEngine(int days, double nu)
    {
        // f0 100, β 0.5, α 2 (20% at the forward), ρ −0.3, r 3%, at the money
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + days));
        double sabr = FdSabrVanillaEngine.Calculate(option, Context, 100.0, 2.0, 0.5, nu, -0.3, curve).NPV!.Value;
        double zeroNu = FdSabrVanillaEngine.Calculate(option, Context, 100.0, 2.0, 0.5, 0.0, -0.3, curve).NPV!.Value;
        Assert.Equal(zeroNu, sabr);

        // QuantLib's check: its FD SABR engine at ν = 1e-8, on its own 2-D grid (VD28's grid difference)
        double nearZero = FdSabrVanillaEngine.Calculate(option, Context, 100.0, 2.0, 0.5, 1e-8, -0.3, curve).NPV!.Value;
        Assert.InRange(sabr / nearZero, 1.0 - 1e-4, 1.0 + 1e-4);
    }
}
