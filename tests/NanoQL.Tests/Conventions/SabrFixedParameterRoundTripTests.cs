// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A SABR fit's fixed parameters are reported through XABR's transforms, as QuantLib reports them: the fit writes back
/// the direct transform of the inverse transform of the fixed value (<c>xabrinterpolation.hpp:198-217, 230-231</c>,
/// <c>sabrinterpolation.hpp:103-129</c>), so a fixed β of 0.5 comes back as 0.5000000000000001 and one of 0.3 as
/// 0.2999999999999999, the wheel's values, while 0.6 comes back as itself. The round trip is four transform calls, not
/// the fit's path, so the reported value is the same whatever the fit does: one-ulp moves of the volatilities leave it
/// unchanged on the wheel.
/// </summary>
public class SabrFixedParameterRoundTripTests
{
    private const double F = 0.039;
    private const double T = 1.0;

    private static readonly double[] Strikes = [.. Enumerable.Range(0, 31).Select(i => 0.03 + (0.002 * i))];

    private static readonly double[] Vols =
        [.. Strikes.Select(k => SabrFunctions.SabrVolatility(k, F, T, 0.3, 0.6, 0.02, 0.01))];

    [Theory]
    [InlineData(0.5, 0.5000000000000001)]
    [InlineData(0.3, 0.2999999999999999)]
    [InlineData(0.6, 0.6)]
    public void AFixedBetaIsReportedThroughTheTransforms(double beta, double reported)
    {
        var s = new SABRInterpolation(Strikes, Vols, T, F, 0.3, beta, 0.02, 0.01, false, true, false, false, false);
        Assert.Equal(reported, s.Beta);
    }
}
