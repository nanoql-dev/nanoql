// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A smile of zero volatilities fitted with vega weights. Every strike's vega weight is 0, and QuantLib normalizes the
/// weights by their zero sum (<c>xabrinterpolation.hpp:144-160</c>): the weights are NaN, every cost the optimizer sees
/// is NaN, and the fit returns the round-tripped guess with a NaN root-mean-square error, a finite maximum error and no
/// end criteria. On 31 strikes from 3% to 9% and a forward of 3.9%, the wheel's fit keeps α 0.4472135954999579, β
/// 0.5000000000000001, ν 0.6324555320336759 and ρ 0, a volatility of 2.4439735411535746 at 4%. nanoql refuses the
/// smile with <see cref="ArgumentException"/>, "vega weights are all zero".
/// </summary>
public class SabrZeroSmileTests
{
    [Fact]
    public void AZeroSmileWithVegaWeightsIsRefused()
    {
        double[] strikes = [.. Enumerable.Range(0, 31).Select(i => 0.03 + (0.002 * i))];
        double[] zeros = new double[strikes.Length];

        ArgumentException e = Assert.Throws<ArgumentException>(() => new SABRInterpolation(
            strikes, zeros, 1.0, 0.039, Math.Sqrt(0.2), 0.5, Math.Sqrt(0.4), 0.0, false, false, false, false, true));
        Assert.Equal("y", e.ParamName);
        Assert.StartsWith("vega weights are all zero", e.Message, StringComparison.Ordinal);

        // Without vega weights the smile fits, as QuantLib's does.
        var unweighted = new SABRInterpolation(
            strikes, zeros, 1.0, 0.039, Math.Sqrt(0.2), 0.5, Math.Sqrt(0.4), 0.0, false, false, false, false, false);
        Assert.True(double.IsFinite(unweighted.RmsError));
    }
}
