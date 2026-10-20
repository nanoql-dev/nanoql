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
/// <para>
/// ZABR's inverse (<c>zabrinterpolation.hpp:78-88</c>) doesn't clamp either: α, β and ρ invert as SABR's do, so a ρ
/// above 0.9999 takes <c>asin</c> of a value beyond 1, a NaN its direct transform turns into −0.9999, and an α below
/// 1e-7 the square root of a negative number, a NaN the model then refuses ("alpha must be positive: nan not allowed");
/// ν and γ invert through <c>tan(π(y/5 − 0.5))</c> and <c>tan(π(y/1.9 − 0.5))</c>, whose period is π, so a ν above 5
/// or a γ above 1.9 comes back as another value inside the range the direct transform's comments name ("limit nu to
/// 5.00", "limit gamma to 1.9", <c>zabrinterpolation.hpp:97-103</c>). On the wheel's ZABR cube a fixed ρ of 0.99995
/// came back as −0.9999, ν 6.0 as 0.9999999999999998 and γ 2.0 as 0.09999999999999978. nanoql's inverse clamps its
/// arguments to the direct transform's range, so each comes back as the nearest value the transform reaches, bit for
/// bit: ρ 0.9999, ν 5, γ 1.9, α 1e-7; and the fit from it is the fit from that value, bit for bit, whose parameters the
/// wheel gives. Under S7R6's one-ulp moves of the volatilities, together and each alone, the γ fit's free parameters
/// and the ν fit's α move by at most 5.4e-11, asserted within the vectors' tolerance; the others move by up to 7.7e-6,
/// asserted within 1e-2, but for the α fit's ρ, which moves by 9.1e-2 and so isn't asserted (the wheel's is
/// −0.026977128413889717).
/// </para>
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

    private static readonly double[] ZabrStrikes = [0.02, 0.025, 0.03, 0.035, 0.04];

    private static readonly double[] ZabrVolatilities = [0.25, 0.22, 0.2, 0.19, 0.195];

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

    [Fact]
    public void ZabrsTransformKeepsEachEdgeInItsRange()
    {
        // ρ fixed above 0.9999: QuantLib's sign flips (the wheel's cube: −0.9999); nanoql's ρ is 0.9999, and the fit is
        // the fit from 0.9999, the wheel's (its ZABR interpolated section): α 0.03554185152378224, ν 2.1642491440654332e-05.
        ZabrInterpolation<ZabrShortMaturityLognormal> rho = ZabrFit(0.2, 0.5, 0.4, 0.99995, 1.0, false, false, true, true);
        Assert.Equal(0.9999, rho.Rho);
        AssertSameFit(ZabrFit(0.2, 0.5, 0.4, 0.9999, 1.0, false, false, true, true), rho);
        AssertFlat(0.03554185152378224, rho.Alpha);
        AssertFlat(2.1642491440654332e-05, rho.Nu);

        // ρ fixed below −0.9999: −0.9999 in both.
        ZabrInterpolation<ZabrShortMaturityLognormal> rhoBelow = ZabrFit(0.2, 0.5, 0.4, -0.99995, 1.0, false, false, true, true);
        Assert.Equal(-0.9999, rhoBelow.Rho);
        AssertSameFit(ZabrFit(0.2, 0.5, 0.4, -0.9999, 1.0, false, false, true, true), rhoBelow);

        // ν fixed at 6: QuantLib's tan wraps it (the wheel's cube and section: 0.9999999999999998); nanoql's is 5, and the
        // fit is the fit from 5, the wheel's: α 0.003760513723405991, ρ 0.09305589423006091.
        ZabrInterpolation<ZabrShortMaturityLognormal> nu = ZabrFit(0.2, 0.5, 6.0, 0.0, 1.0, false, true, false, true);
        Assert.Equal(5.0, nu.Nu);
        AssertSameFit(ZabrFit(0.2, 0.5, 5.0, 0.0, 1.0, false, true, false, true), nu);
        AssertNear(0.003760513723405991, nu.Alpha);
        AssertFlat(0.09305589423006091, nu.Rho);

        // ν fixed inside the range: its round trip, QuantLib's (the wheel's cube: 0.4000000000000001).
        Assert.Equal(0.4000000000000001, ZabrFit(0.2, 0.5, 0.4, 0.0, 1.0, false, true, false, true).Nu);

        // γ fixed at 2: QuantLib's tan wraps it (the wheel's cube: 0.09999999999999978); nanoql's is 1.9, and the fit is
        // the fit from 1.9, the wheel's: α 0.03462247561965941, ν 0.41817772079967036.
        ZabrInterpolation<ZabrShortMaturityLognormal> gamma = ZabrFit(0.2, 0.5, 0.4, 0.0, 2.0, false, false, false, true);
        Assert.Equal(1.9, gamma.Gamma);
        AssertSameFit(ZabrFit(0.2, 0.5, 0.4, 0.0, 1.9, false, false, false, true), gamma);
        AssertNear(0.03462247561965941, gamma.Alpha);
        AssertNear(0.41817772079967036, gamma.Nu);

        // α fixed below 1e-7: QuantLib's fit fails on the NaN (the wheel: "alpha must be positive: nan not allowed");
        // nanoql's α is 1e-7, and the fit is the fit from 1e-7, the wheel's: ν 4.998428958137255 (its ρ is flat, the
        // wheel's −0.026977128413889717).
        ZabrInterpolation<ZabrShortMaturityLognormal> alpha = ZabrFit(1e-8, 0.5, 0.4, 0.0, 1.0, true, false, false, true);
        Assert.Equal(1e-7, alpha.Alpha);
        AssertSameFit(ZabrFit(1e-7, 0.5, 0.4, 0.0, 1.0, true, false, false, true), alpha);
        AssertFlat(4.998428958137255, alpha.Nu);
    }

    private static SABRInterpolation Fit(
        double a, double b, double n, double r, bool fa, bool fb, bool fn, bool fr) =>
        new(Strikes, Vols, T, F, a, b, n, r, fa, fb, fn, fr, false);

    // A ZABR fit of the smile with β fixed, the given parameter fixed with it.
    private static ZabrInterpolation<ZabrShortMaturityLognormal> ZabrFit(
        double alpha, double beta, double nu, double rho, double gamma, bool alphaIsFixed, bool nuIsFixed, bool rhoIsFixed, bool gammaIsFixed) =>
        new(ZabrStrikes, ZabrVolatilities, 1.0, 0.03, alpha, beta, nu, rho, gamma, alphaIsFixed, true, nuIsFixed, rhoIsFixed, gammaIsFixed);

    // Two fits of the same inputs give the same computation: bit for bit.
    private static void AssertSameFit(ZabrInterpolation<ZabrShortMaturityLognormal> expected, ZabrInterpolation<ZabrShortMaturityLognormal> actual)
    {
        Assert.Equal(expected.Alpha, actual.Alpha);
        Assert.Equal(expected.Beta, actual.Beta);
        Assert.Equal(expected.Nu, actual.Nu);
        Assert.Equal(expected.Rho, actual.Rho);
        Assert.Equal(expected.Gamma, actual.Gamma);
        Assert.Equal(expected.RmsError, actual.RmsError);
        Assert.Equal(expected.MaxError, actual.MaxError);
    }

    // Within the vectors' tolerance, at the vectors' scale of 1.
    private static void AssertNear(double expected, double actual) =>
        Assert.True(Tolerance.Within(expected, actual), $"expected {expected} within R7, got {actual}");

    // Within a flat fit's measured instability, in S7R6's measure, at the vectors' scale of 1.
    private static void AssertFlat(double expected, double actual) =>
        Assert.True(Tolerance.Within(expected, actual, 1.0, FlatFit), $"expected {expected} within {FlatFit}, got {actual}");
}
