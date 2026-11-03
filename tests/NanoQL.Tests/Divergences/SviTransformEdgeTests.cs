// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// SVI's inverse transforms at their edges. QuantLib's inverse (<c>sviinterpolation.hpp:98-108</c>) takes <c>asin</c>
/// of ρ/0.999999, <c>√(σ − 10⁻⁶)</c>, <c>√(a − 10⁻⁶ + bσ√(1−ρ²))</c> and <c>tan</c> of an argument that passes π/2 once
/// b(1+|ρ|) exceeds 4·0.999999, each of which a value its parameters' check accepts can push out of range: on a
/// seven-strike skew (a year, forward 100) QuantLib's fit refuses a fixed ρ of 0.9999995 ("b (nan) must be non
/// negative"), a fixed σ of 5·10⁻⁷ ("sigma (-nan(ind)) must be positive") and an a guessed at −bσ√(1−ρ²) ("a + b sigma
/// sqrt(1-rho^2) (a=-nan(ind), …) must be non negative"), and fits a b guessed at 4/(1+|ρ|) from a wrapped guess (the
/// C++ harness). Here each argument is clamped into its transform's range (<c>asin</c>'s to [−1, 1], each square root's
/// floored at 0, the <c>tan</c> argument capped at π/2), as SABR's and ZABR's transforms are clamped. QuantLib's check:
/// its own fit from the clamped value, which the direct transform maps back to.
/// </summary>
public class SviTransformEdgeTests
{
    private const double Eps1 = 0.000001;
    private const double Eps2 = 0.999999;
    private static readonly double[] Strikes = [70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0];
    private static readonly double[] Vols = [0.32, 0.28, 0.245, 0.22, 0.205, 0.198, 0.197];

    [Fact]
    public void ARhoFixedBeyondTheTransformsRangeFitsAsItsClampedValue() =>
        AssertFit(
            Fit(rho: 0.9999995, rhoIsFixed: true),
            Fit(rho: Eps2, rhoIsFixed: true),
            (0.054126876716736785, 1.987794896561412, 4.482523655047249e-06, 0.999999, 1.733650255238503));

    [Fact]
    public void ASigmaFixedBelowTheTransformsRangeFitsAsItsClampedValue() =>
        AssertFit(
            Fit(sigma: 5e-7, sigmaIsFixed: true),
            Fit(sigma: Eps1, sigmaIsFixed: true),
            (0.04035083766636197, 0.072602321537783, 1e-06, -0.999998999999592, 0.04661412999697509));

    [Fact]
    public void AnAGuessedAtItsConstraintsEdgeFitsAsItsClampedValue()
    {
        double edge = -1.0 * 0.1 * Math.Sqrt(1.0 - (0.4 * 0.4));
        AssertFit(
            Fit(a: edge, b: 1.0, sigma: 0.1, rho: -0.4),
            Fit(a: Eps1 + edge, b: 1.0, sigma: 0.1, rho: -0.4),
            (0.00943443539411884, 0.13371755216225345, 0.2893410735671867, -0.6527313759398947, 0.002681947491301263));
    }

    [Fact]
    public void ABGuessedAtItsConstraintsEdgeFitsAsItsClampedValue() =>

        // QuantLib's own fit from the edge, a wrapped guess: a 0.009434230236992858, b 0.1337178294155512
        AssertFit(
            Fit(b: 4.0 / 1.4, rho: -0.4),
            Fit(b: 4.0 * Eps2 / 1.4, rho: -0.4),
            (0.009434466777737655, 0.13371750954118272, 0.289340941526248, -0.6527316394815342, 0.0026819289009643375));

    // The fit from the edge equals the fit from the clamped value, and that is QuantLib's fit from the clamped value
    // (a, b, σ, ρ, m) (the C++ harness).
    private static void AssertFit(
        SviInterpolation edge, SviInterpolation clamped, (
            double A, double B, double Sigma, double Rho, double M) quantLib)
    {
        Assert.Equal(
            [clamped.A, clamped.B, clamped.Sigma, clamped.Rho, clamped.M],
            [edge.A, edge.B, edge.Sigma, edge.Rho, edge.M]);
        Assert.Equal(quantLib.A, edge.A, Math.Abs(quantLib.A) * 1e-9);
        Assert.Equal(quantLib.B, edge.B, Math.Abs(quantLib.B) * 1e-9);
        Assert.Equal(quantLib.Sigma, edge.Sigma, Math.Abs(quantLib.Sigma) * 1e-9);
        Assert.Equal(quantLib.Rho, edge.Rho, Math.Abs(quantLib.Rho) * 1e-9);
        Assert.Equal(quantLib.M, edge.M, Math.Abs(quantLib.M) * 1e-9);
    }

    private static SviInterpolation Fit(
        double? a = null,
        double? b = null,
        double? sigma = null,
        double? rho = null,
        bool sigmaIsFixed = false,
        bool rhoIsFixed = false) =>
        new(Strikes, Vols, 1.0, 100.0, a, b, sigma, rho, null, false, false, sigmaIsFixed, rhoIsFixed, false);
}
