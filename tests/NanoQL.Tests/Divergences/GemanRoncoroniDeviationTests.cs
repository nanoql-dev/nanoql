// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Geman-Roncoroni process's standard deviation over a step at small mean reversion θ₁, in its cancellation-free
/// form through expm1 below 2θ₁Δt 1e-2, σ√Δt at θ₁ = 0 (fixed). QuantLib's √(σ²(1 − e^(−2θ₁Δt))/(2θ₁)) cancels as θ₁
/// falls and is 0/0 at 0. On QuantLib 1.44 (σ² 10, a 0, Δt 0.004) the deviation is 0.19999999994736442 at θ₁ 1e-6,
/// 0.20000056338475591 at 1e-9, 0.19992005623875167 at 1e-12 and NaN at 0, for 0.1999999996, 0.1999999999996,
/// 0.1999999999999996 and 0.2; nanoql's meets 40-digit references (mpmath) to a few ulps at every θ₁, QuantLib's
/// formula at 35.
/// </summary>
public class GemanRoncoroniDeviationTests
{
    [Theory]
    [InlineData(35.0, 0.186783395768725550152257943128)]
    [InlineData(1e-6, 0.19999999960000000274833484265)]
    [InlineData(1e-9, 0.199999999999600002081668837801)]
    [InlineData(1e-12, 0.199999999999999602081668171181)]
    [InlineData(1e-15, 0.200000000000000001681668173818)]
    [InlineData(0.0, 0.200000000000000002081668171172)]
    public void TheDeviationMeetsItsReference(double theta1, double reference)
    {
        var process = new GemanRoncoroniProcess(
            3.3, 3.1, 0.05, -0.09, 0.07, -0.40, -0.40, 1.6, 1.0, 0.5, 10.0, 0.0, -0.3, theta1, 9.0, 0.10, 1.9);
        double sd = process.StdDeviation(0.0, 3.3, 0.004);
        Assert.True(Math.Abs(sd - reference) <= 4e-16 * reference, $"{sd} for {reference}");
    }
}
