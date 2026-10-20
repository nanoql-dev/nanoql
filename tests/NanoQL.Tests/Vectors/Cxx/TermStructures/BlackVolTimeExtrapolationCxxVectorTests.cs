// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="BlackVolTimeExtrapolation"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/equityfx/blackvoltimeextrapolation.json</c>: its two overloads
/// called directly on the harness's own variances, a surface w(t, k) = t s(k)² + c t² with s(k) = sigma0 + slope (k −
/// 100) / 100 and a curve w(t) = t sigma0² + c t², computed here as the harness's C++ computes them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackVolTimeExtrapolationCxxVectorTests
{
    [Fact]
    public void BlackVolTimeExtrapolationCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/equityfx/blackvoltimeextrapolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var type = (BlackVolTimeExtrapolationType)c.Int("type");
            double[] times = c.Doubles("times"), ts = c.Doubles("ts"), strikes = c.Doubles("strikes");
            double sigma0 = c.Double("sigma0"), slope = c.Double("slope"), cc = c.Double("c");
            check.Output(
                c,
                "curve",
                ts.Select(t => Lazy(() => BlackVolTimeExtrapolation.ExtrapolatedVariance(
                    type, t, times, u => (u * sigma0 * sigma0) + (cc * u * u)))).ToArray());
            if (c.Expected.TryGetProperty("surface", out _))
            {
                check.Output(
                    c,
                    "surface",
                    ts.Select(t => (object?)strikes.Select(k => Lazy(() => BlackVolTimeExtrapolation.ExtrapolatedVariance(
                        type, t, k, times, (u, kk) => Surface(u, kk, sigma0, slope, cc)))).ToArray()).ToArray());
            }
        }

        check.Complete();
    }

    // The harness's surface: s = sigma0 + slope (k - 100) / 100; u s s + c u u, in the C++'s order.
    private static double Surface(double u, double k, double sigma0, double slope, double c)
    {
        double s = sigma0 + (slope * (k - 100.0) / 100.0);
        return (u * s * s) + (c * u * u);
    }
}
