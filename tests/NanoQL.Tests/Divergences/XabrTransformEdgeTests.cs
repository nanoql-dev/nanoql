// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// XABR's parameter transforms at their edges. QuantLib's SABR inverse transform doesn't clamp its arguments
/// (<c>sabrinterpolation.hpp:103-114</c>): a ρ beyond 0.9999 takes <c>asin</c> of a value above 1, a NaN that the direct
/// transform turns into −0.9999, so a fixed ρ of 0.99995 comes back as −0.9999, its sign flipped, and a free guess of
/// 0.99995 fails the fit with "alpha must be positive: nan not allowed"; an α or ν below 1e-7, ν = 0 included, takes
/// the square root of a negative number, a NaN the fit refuses ("alpha must be positive: nan not allowed", "nu must be
/// non negative: nan not allowed"). nanoql's inverse clamps each argument to its direct transform's range, so such an
/// input comes back as the nearest value the transform reaches, bit for bit: ρ = 0.9999, ν = 1e-7, and the fixed β of
/// 0.6. The fits from those values are the wheel's fits from them. Two of them are well posed and agree within the
/// vectors' tolerance; the three that pin a parameter at an edge can't reproduce the smile, so their optimum is flat:
/// when the input volatilities move by one ulp, their free parameters move by up to 2.4e-3 in S7R6's measure, at the
/// vectors' scale of 1, which is up to 24% of the small ones' own values. Those are asserted within 1e-2 in that
/// measure.
/// </summary>
public class XabrTransformEdgeTests
{
    private const double F = 0.039;
    private const double T = 1.0;

    // The flat fits' free parameters: their worst measured move under S7R6's one-ulp moves is 2.4e-3 in S7R6's
    // measure, |a − b| / max(|a|, |b|, 1).
    private const double FlatFit = 1e-2;

    private static readonly double[] Strikes = [.. Enumerable.Range(0, 31).Select(i => 0.03 + (0.002 * i))];

    private static readonly double[] Vols =
        [.. Strikes.Select(k => SabrFunctions.SabrVolatility(k, F, T, 0.3, 0.6, 0.02, 0.01))];

    [Fact]
    public void SabrsEdgesComeBackAsTheTransformsNearestValues()
    {
        // A fixed rho of 0.99995: QuantLib's fit has rho -0.9999, with alpha 0.3000337293515076 and nu
        // 1.039043264998764e-07; nanoql's rho is 0.9999, its fit the wheel's from 0.9999.
        SABRInterpolation s = Fit(0.3, 0.6, 0.02, 0.99995, false, true, false, true);
        Assert.Equal(0.9999, s.Rho);
        Assert.Equal(0.6, s.Beta);
        AssertFlat(0.30000081527915673, s.Alpha);
        AssertFlat(0.00034068250315115255, s.Nu);

        // A free rho guessed at 0.99995: QuantLib throws "alpha must be positive: nan not allowed".
        s = Fit(0.3, 0.6, 0.02, 0.99995, false, true, false, false);
        Assert.Equal(0.6, s.Beta);
        AssertNear(0.2999999999999604, s.Alpha);
        AssertNear(0.019999999993427324, s.Nu);
        AssertNear(0.010000000022157259, s.Rho);

        // nu = 0, fixed and free: QuantLib throws "nu must be non negative: nan not allowed".
        s = Fit(0.3, 0.6, 0.0, 0.01, false, true, true, false);
        Assert.Equal(1e-07, s.Nu);
        Assert.Equal(0.6, s.Beta);
        AssertFlat(0.3000338775759319, s.Alpha);
        AssertFlat(0.009258590353153132, s.Rho);
        s = Fit(0.3, 0.6, 0.0, 0.01, false, true, false, false);
        Assert.Equal(0.6, s.Beta);
        AssertNear(0.30000000000000177, s.Alpha);
        AssertNear(0.020000000000039132, s.Nu);
        AssertNear(0.009999999999816555, s.Rho);

        // A free alpha guessed at 1e-8: QuantLib throws "alpha must be positive: nan not allowed". Its rho runs to the
        // transform's edge, 0.9999.
        s = Fit(1e-8, 0.6, 0.02, 0.01, false, true, false, false);
        Assert.Equal(0.9999, s.Rho);
        Assert.Equal(0.6, s.Beta);
        AssertFlat(0.30000193799401825, s.Alpha);
        AssertFlat(0.00032870084917787534, s.Nu);
    }

    private static SABRInterpolation Fit(
        double a, double b, double n, double r, bool fa, bool fb, bool fn, bool fr) =>
        new(Strikes, Vols, T, F, a, b, n, r, fa, fb, fn, fr, false);

    // Within the vectors' tolerance, at the vectors' scale of 1.
    private static void AssertNear(double expected, double actual) =>
        Assert.True(Tolerance.Within(expected, actual), $"expected {expected} within R7, got {actual}");

    // Within a flat fit's measured instability, in S7R6's measure, at the vectors' scale of 1.
    private static void AssertFlat(double expected, double actual) =>
        Assert.True(Tolerance.Within(expected, actual, 1.0, FlatFit), $"expected {expected} within {FlatFit}, got {actual}");
}
