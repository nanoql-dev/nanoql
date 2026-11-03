// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The bivariate Student t-distribution beside the line h = ρk at |ρ| = 1: QuantLib's x(m, h, k) returns 0 wherever its
/// denominator is below 1e-8, which flips x from 1 to 0 within about 1e-4 of the line, so its value there is off by up
/// to 1.9e-5 and can be negative; nanoql takes the limit only at an exact 0/0. The references are the distribution's
/// limits at ρ = ±1, F(min(h, k)) at ρ 1 and max(0, F(h) − F(−k)) at −1 (QuantLib's own boundary test's formulas), here
/// ½ and 0; QuantLib's values are its own.
/// </summary>
public class BivariateStudentTests
{
    [Fact]
    public void BesideTheLineAtRhoOneTheLimitsHold()
    {
        Assert.True(Math.Abs(new BivariateCumulativeStudentDistribution(2, 1.0).Evaluate(0.0, 9.9e-5) - 0.5) < 1e-11);
        Assert.True(Math.Abs(new BivariateCumulativeStudentDistribution(10, 1.0).Evaluate(0.0, 9.9e-5) - 0.5) < 1e-11);
        double negative = new BivariateCumulativeStudentDistribution(2, -1.0).Evaluate(0.0, -9.9e-5);
        Assert.True(negative > -1e-14 && negative < 1e-11, $"{negative:R}");

        // QuantLib's: 0.50001750089279151, 0.50001926086497173 and -1.7500892791485491e-05
    }

    [Fact]
    public void AwayFromTheLineQuantLibsValuesAreKept()
    {
        // the denominator at least 1e-8: QuantLib's values, bit for bit, at QuantLib's own symmetry point
        var t = new BivariateCumulativeStudentDistribution(5, 0.5);
        Assert.Equal(0.12287138057615865, t.Evaluate(0.75, -1.25));
        Assert.Equal(0.12287138057615862, t.Evaluate(-1.25, 0.75));
    }
}
