// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// SVI fits with b or a fixed (the S-rx plan's SRXR3 (xvii), fixed under R8). QuantLib's direct transform keeps a free
/// b inside b(1+|ρ|) &lt; 4 and a free a inside a + bσ√(1−ρ²) &gt; 0, but copies a fixed one as given while ρ, σ and b
/// move (<c>sviinterpolation.hpp:112-128</c>), and every cost evaluation builds a section whose parameters' check
/// refuses the excess (<c>xabrinterpolation.hpp:291-306</c>, <c>svismilesection.cpp:39-46</c>): on a seven-strike skew
/// (a year, forward 100) QuantLib's fit throws midway with b fixed at 3.5 ("b(1+|rho|) must be less than or equal to 4,
/// (b=3.5, rho=0.250507)"), with a, b and σ fixed at −0.05, 1 and 0.1 (at ρ 0.929163), and with a and σ fixed at −0.1
/// and 0.05 (at b 0.069537) [executed on the harness]. Here, where a trial would fail QuantLib's check, the free
/// parameters move onto the set it admits, each rounded inward: ρ capped first at the tightest bound the fixed
/// parameters set, then σ or b raised to meet a's constraint. A fixed set no ρ admits is refused before the fit, with
/// QuantLib's text. A trial the check passes is QuantLib's, so a fit QuantLib completes is QuantLib's. QuantLib's
/// check: a fresh section at the reported parameters reproduces the fitted volatilities and error.
/// </summary>
public class SviFixedParameterTests
{
    private static readonly double[] Strikes = [70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0];
    private static readonly double[] Vols = [0.32, 0.28, 0.245, 0.22, 0.205, 0.198, 0.197];

    [Fact]
    public void AFitWithBFixedAt3Point5Completes()
    {
        SviInterpolation fit = Fit(b: 3.5, rho: 0.1, bIsFixed: true);
        Assert.Equal(3.5, fit.B);
        Assert.True(fit.B * (1.0 + Math.Abs(fit.Rho)) <= 4.0);
        AssertSectionReproducesTheFit(fit);
    }

    [Fact]
    public void AFitWithABAndSigmaFixedCapsRho()
    {
        SviInterpolation fit = Fit(
            a: -0.05, b: 1.0, sigma: 0.1, rho: -0.3, aIsFixed: true, bIsFixed: true, sigmaIsFixed: true);
        Assert.Equal(-0.05, fit.A);
        Assert.True(Math.Abs(fit.Rho) <= Math.Sqrt(1.0 - (0.5 * 0.5)));
        AssertSectionReproducesTheFit(fit);
    }

    [Fact]
    public void AFitWithAAndSigmaFixedRaisesBAndCapsRho()
    {
        SviInterpolation fit = Fit(a: -0.1, b: 2.5, sigma: 0.05, rho: 0.3, aIsFixed: true, sigmaIsFixed: true);
        Assert.Equal(-0.1, fit.A);

        // ρ within (16σ² − a²)/(16σ² + a²) = 0.6, where the largest b meets a's constraint
        Assert.True(Math.Abs(fit.Rho) <= 0.6);
        Assert.True(fit.A + (fit.B * fit.Sigma * Math.Sqrt(1.0 - (fit.Rho * fit.Rho))) >= 0.0);
        AssertSectionReproducesTheFit(fit);
    }

    [Fact]
    public void AFixedBAbove4IsRefusedBeforeTheFit()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => Fit(b: 4.5, rho: 0.0, bIsFixed: true));
        Assert.StartsWith(
            "b(1+|rho|) must be less than or equal to 4, (b=4.5, rho=0)", e.Message, StringComparison.Ordinal);
    }

    // QuantLib's check: a fresh section at the fit's parameters gives the fit's values at the strikes, and its error,
    // √(n Σ wᵢ eᵢ² / (n − 1)) on the fit's weights.
    private static void AssertSectionReproducesTheFit(SviInterpolation fit)
    {
        var section = new SviSmileSection(1.0, 100.0, [fit.A, fit.B, fit.Sigma, fit.Rho, fit.M]);
        Assert.Equal(Strikes.Select(k => fit.Evaluate(k)), Strikes.Select(section.Volatility));

        double[] weights = [.. fit.InterpolationWeights()];
        double squared = 0.0;
        for (int i = 0; i < Strikes.Length; ++i)
        {
            double error = section.Volatility(Strikes[i]) - Vols[i];
            squared += error * error * weights[i];
        }

        int n = Strikes.Length;
        Assert.Equal(Math.Sqrt(n * squared / (n - 1)), fit.RmsError);
    }

    private static SviInterpolation Fit(
        double? a = null,
        double? b = null,
        double? sigma = null,
        double? rho = null,
        bool aIsFixed = false,
        bool bIsFixed = false,
        bool sigmaIsFixed = false) =>
        new(Strikes, Vols, 1.0, 100.0, a, b, sigma, rho, null, aIsFixed, bIsFixed, sigmaIsFixed, false, false);
}
