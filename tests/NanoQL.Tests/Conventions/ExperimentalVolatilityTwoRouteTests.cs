// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Two routes to one value for the experimental volatility structures: the no-arbitrage SABR section against the SABR
/// section (Hagan's 2002 expansion) where both are arbitrage-free; the SVI interpolated section against the SVI section
/// it was fitted to; the extended Black variance curve and surface on fixed values against the Black variance curve and
/// surface; the abcd curve's volatility against its adjustment factor times the abcd Black volatility of its fitted
/// coefficients.
/// </summary>
public class ExperimentalVolatilityTwoRouteTests
{
    private static readonly Date Today = new(15, Month.May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheNoArbitrageSabrIsHagansWhereBothAreArbitrageFree()
    {
        // QuantLib's interpolation case's market, ν small: prices within QuantLib's consistency measure, 1e-5
        double[] p = [0.2, 0.6, 0.02, 0.01];
        var sabr = new SabrSmileSection(1.0, 0.039, p);
        var noArb = new NoArbSabrSmileSection(1.0, 0.039, p);
        for (double k = 0.02; k <= 0.09 + 1e-12; k += 0.005)
        {
            Assert.True(Math.Abs(sabr.OptionPrice(k) - noArb.OptionPrice(k)) <= 1e-5, $"strike {k}");
        }
    }

    [Fact]
    public void TheSviInterpolatedSectionIsTheSviSectionItIsFittedTo()
    {
        // vols from the SVI section at a, b, σ, ρ, m; the fit, every parameter free, recovers them within 1e-6
        double t = 1.0, forward = 0.03;
        double[] parameters = [0.0005, 0.01, 0.1, -0.3, 0.0];
        var svi = new SviSmileSection(t, forward, parameters);
        double[] strikes = [.. Enumerable.Range(0, 11).Select(i => forward * (0.7 + (0.06 * i)))];
        double[] vols = [.. strikes.Select(k => svi.Volatility(k))];
        Date optionDate = Today + 365;
        SviInterpolatedSmileSection fitted = SviInterpolatedSmileSection.Create(
            optionDate, forward, strikes, false, 0.0, vols, null, null, null, null, null, Context,
            false, false, false, false, false, vegaWeighted: false);
        foreach (double k in strikes)
        {
            Assert.True(Math.Abs(svi.Volatility(k) - fitted.Volatility(k)) <= 1e-6, $"strike {k}");
        }
    }

    [Fact]
    public void TheExtendedCurveAndSurfaceAreTheLandedOnesOnFixedValues()
    {
        Date[] dates = [Today + 91, Today + 365, Today + 730];
        double[] strikes = [80.0, 100.0, 120.0];
        double[,] grid = { { 0.25, 0.24, 0.23 }, { 0.20, 0.21, 0.22 }, { 0.22, 0.225, 0.23 } };
        var matrix = new Matrix(grid);
        double[] flat = [.. Enumerable.Range(0, 3).SelectMany(i => Enumerable.Range(0, 3).Select(j => grid[i, j]))];
        var extended = new ExtendedBlackVarianceSurface(Today, null, dates, strikes, flat, A365);
        var landed = new BlackVarianceSurface(Today, null, dates, strikes, matrix, A365);
        double[] curveVols = [0.20, 0.21, 0.22];
        var extendedCurve = new ExtendedBlackVarianceCurve(Today, dates, curveVols, A365);
        var landedCurve = new BlackVarianceCurve(Today, dates, curveVols, A365);
        foreach (int days in new[] { 30, 91, 200, 365, 500, 730, 1000 })
        {
            Date d = Today + days;
            foreach (double k in new[] { 70.0, 80.0, 95.0, 100.0, 110.0, 120.0, 130.0 })
            {
                Assert.Equal(landed.BlackVariance(d, k, true), extended.BlackVariance(d, k, true));
                Assert.Equal(landedCurve.BlackVariance(d, k, true), extendedCurve.BlackVariance(d, k, true));
            }
        }
    }

    [Fact]
    public void TheAbcdCurvesVolatilityIsItsFactorTimesItsFittedFunction()
    {
        // QuantLib's atmVolImpl: k(t) times the fit's Black volatility at t, the coefficients a, b, c and d read back
        Period[] tenors =
            [new(1, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        AbcdAtmVolCurve curve = AbcdAtmVolCurve.Create(0, new TARGET(), tenors, [0.20, 0.24, 0.26, 0.255, 0.24], Context);
        foreach (double t in new[] { 0.1, 0.3, 0.75, 1.5, 2.0 })
        {
            double expected = curve.K(t) * AbcdFunctions.AbcdBlackVolatility(t, curve.A, curve.B, curve.C, curve.D);
            Assert.Equal(expected, curve.AtmVol(t), 1e-15);
        }
    }
}
