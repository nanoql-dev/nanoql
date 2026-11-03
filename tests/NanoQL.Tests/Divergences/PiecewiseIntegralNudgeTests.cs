// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The piecewise integral's nudges at critical points at or below 0 (fixed). QuantLib's nudges each piece's ends inward
/// by multiplying by 1 + ε, which moves a negative point the wrong way and 0 not at all, so on the two-interval segment
/// rule a step from 0 to 1 at −1 integrates to 3.5 over [−3, 3], exactly 4, and at 0 to 3.75, exactly 3; at +1
/// QuantLib's is exact. nanoql's nudge is mirrored below 0 and is the next double at 0, so every piece excludes its
/// critical points and the rule is exact on each constant piece.
/// </summary>
public class PiecewiseIntegralNudgeTests
{
    [Theory]
    [InlineData(-1.0, 0.0, 1.0, -3.0, 3.0, 4.0)]
    [InlineData(0.0, 0.0, 1.0, -3.0, 3.0, 3.0)]
    [InlineData(1.0, 0.0, 1.0, -3.0, 3.0, 2.0)]
    [InlineData(-1.0, 1.0, 2.0, -1.0, 3.0, 8.0)]
    [InlineData(0.0, 0.0, 1.0, 0.0, 2.5, 2.5)]
    [InlineData(-2.0, 5.0, -1.0, -6.0, -2.0, 20.0)]
    public void AStepIntegratesExactly(double at, double below, double above, double a, double b, double exact)
    {
        double[] X = [at];
        double[] Y = [below, above];
        var piecewise = new PiecewiseIntegral(new SegmentIntegral(2), X);
        double value = piecewise.Integrate(t => PiecewiseFunctionFunctions.Evaluate(X, Y, t), a, b);
        Assert.True(Math.Abs(value - exact) <= 1e-14 * Math.Abs(exact), $"step at {at} over [{a}, {b}]: {value:R} for {exact}");
    }

    [Theory]
    [InlineData(-1.0, 3.5)]
    [InlineData(0.0, 3.75)]
    public void QuantLibsNudgesMissTheSteps(double at, double quantLib)
    {
        // QuantLib's two pieces over [−3, 3]: [−3, c/(1 + ε)] and [c·(1 + ε), 3]
        double eps = 1.0 + 2.220446049250313e-16;
        double[] X = [at];
        double[] Y = [0.0, 1.0];
        double F(double t) => PiecewiseFunctionFunctions.Evaluate(X, Y, t);
        var segment = new SegmentIntegral(2);
        double value = segment.Integrate(F, -3.0, Math.Min(at / eps, 3.0)) + segment.Integrate(F, at * eps, 3.0);
        Assert.Equal(quantLib, value, 12);
    }
}
