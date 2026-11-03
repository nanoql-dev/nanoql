// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The internal double-double normal cumulative distribution the full lookback engines evaluate below their small-carry
/// cut: against mpmath's 50-digit values at the double arguments, each given as its nearest double and the remainder's,
/// within 2^-96 relative where N is at least 1/2 or comes from the continued fraction (x ≤ −3√2), the worst 2.2e-30 at
/// x = −36, where the exponential and the last products round, and within 2^-104 absolute on the series' tail side,
/// where erfc is 1 − erf; beyond z = 26 (x ≈ −36.8) the value is 0, as double's range ends. Read through reflection,
/// the type being internal.
/// </summary>
public class DoubleDoubleNormalTests
{
    private static readonly Assembly NanoQL = typeof(COSHestonEngine).Assembly;
    private static readonly Type DD = NanoQL.GetType("NanoQL.Numerics.DoubleDouble", throwOnError: true)!;
    private static readonly MethodInfo Cdf = NanoQL.GetType("NanoQL.Numerics.DoubleDoubleNormal", throwOnError: true)!
        .GetMethod("Cdf", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData(-36.0, 4.182624065797283e-284, 2.8599864938910797e-300)]
    [InlineData(-20.0, 2.7536241186062337e-89, -4.205855543202273e-106)]
    [InlineData(-8.5, 9.479534822203318e-18, 7.041129563898285e-34)]
    [InlineData(-4.25, 1.068852577493442e-05, 5.367763737933911e-23)]
    [InlineData(-3.0, 0.0013498980316300946, -5.053886685858262e-20)]
    [InlineData(-1.0, 0.15865525393145705, 4.9468552901786335e-18)]
    [InlineData(-0.1, 0.460172162722971, 4.236746516162269e-18)]
    [InlineData(0.0, 0.5, 0.0)]
    [InlineData(1e-08, 0.5000000039894228, 1.189765651643976e-17)]
    [InlineData(0.5, 0.6914624612740131, -1.4568778275699303e-17)]
    [InlineData(2.0, 0.9772498680518208, 1.3849763108389696e-18)]
    [InlineData(4.25, 0.9999893114742251, -2.988676418855811e-17)]
    [InlineData(8.0, 0.9999999999999993, 4.403775734791551e-17)]
    [InlineData(-40.0, 0.0, 0.0)]
    public void TheDistributionHoldsThirtyDigits(double x, double hi, double lo)
    {
        object value = Cdf.Invoke(null, [DD.GetMethod("Of", [typeof(double)])!.Invoke(null, [x])])!;
        double vHi = (double)DD.GetProperty("Hi")!.GetValue(value)!;
        double vLo = (double)DD.GetProperty("Lo")!.GetValue(value)!;

        // the two highs are close, so their difference is exact; the lows' adds the remainder's rounding alone
        double error = Math.Abs((vHi - hi) + (vLo - lo));
        bool tailSeries = x < 0.0 && x > -3.0 * Math.Sqrt(2.0);
        double tolerance = tailSeries ? Math.ScaleB(1.0, -104) : Math.ScaleB(Math.Abs(hi), -96);
        Assert.True(error <= tolerance, $"N({x}) = {vHi} + {vLo}: off {error} against {hi} + {lo}");
    }

    [Fact]
    public void NotANumberStaysNotANumber()
    {
        object value = Cdf.Invoke(null, [DD.GetMethod("Of", [typeof(double)])!.Invoke(null, [double.NaN])])!;
        Assert.True(double.IsNaN((double)DD.GetProperty("Hi")!.GetValue(value)!));
    }
}
