// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference CEV and SABR engines near β = 1. QuantLib's mesher reads the CEV density's quantiles through
/// Boost's non-central chi-squared, which throws within the band ("Series did not converge" at β 0.9999 on a year at α
/// 0.75, f0 2.1; "beta can not be one" at 1) (QuantLib 1.44). Within |β − 1| &lt; 1e-3 nanoql's mesher takes its bounds
/// from the lognormal limit's quantiles, the CEV density at β = 1, and the engines' upper boundary is the analytic
/// engine's value, Black's formula at Hagan's equivalent volatility there (<c>Divergences/CevNearOneTests.cs</c>). The
/// checks: the lognormal's quantiles in closed form, and the analytic CEV engine.
/// </summary>
public class FdCevNearOneTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Theory]
    [InlineData(0.9995)]
    [InlineData(1.0)]
    [InlineData(1.0009)]
    public void TheMeshersBoundsAreTheLognormalLimitsQuantiles(double beta)
    {
        // f0 100, α 0.3, a year, ε 1e-4, scale 1.5: f0·exp(±α·N⁻¹(1 − ε) − α²/2) widened by the scale
        var mesher = new FdmCEV1dMesher(50, 100.0, 0.3, beta, 1.0, 1e-4, 1.5);
        double q = new MaddockInverseCumulativeNormal().Evaluate(1 - 1e-4);
        double upper = 1.5 * 100.0 * Math.Exp((0.3 * q) - (0.5 * 0.3 * 0.3));
        double lower = 100.0 * Math.Exp((-0.3 * q) - (0.5 * 0.3 * 0.3)) / 1.5;
        Assert.Equal(upper, mesher.Location(49), 1e-9);
        Assert.Equal(lower, mesher.Location(0), 1e-9);
    }

    [Theory]
    [InlineData(0.9995, 90.0)]
    [InlineData(0.9995, 110.0)]
    [InlineData(0.9999, 100.0)]
    [InlineData(1.0005, 100.0)]
    public void TheEnginesInTheBandAreTheAnalyticEngines(double beta, double strike)
    {
        // α 0.3·100^(1 − β), about 30%, a year, r 3%, on testFdmCevOp's grid, where the engine is within 6e-5 of the
        // analytic engine in the band as at β 0.9 outside it; FD SABR at ν 1e-3, ρ 0, on testFdmSabrCevPricing's grid,
        // below 1 only, as QuantLib requires
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        double alpha = 0.3 * Math.Pow(100.0, 1 - beta);
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + 365));
        double analytic = AnalyticCEVEngine.Calculate(option, Context, 100.0, alpha, beta, curve).NPV!.Value;
        double fd = FdCEVVanillaEngine.Calculate(option, Context, 100.0, alpha, beta, curve, 100, 1000, 1, 1.0, 1e-6).NPV!.Value;
        Assert.Equal(analytic, fd, 2e-4);
        if (beta < 1.0)
        {
            double sabr = FdSabrVanillaEngine.Calculate(option, Context, 100.0, alpha, beta, 1e-3, 0.0, curve, 100, 400, 3).NPV!.Value;
            Assert.Equal(analytic, sabr, 1e-2);
        }
    }
}
