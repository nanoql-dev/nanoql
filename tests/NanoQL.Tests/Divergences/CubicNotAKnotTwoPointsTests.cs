// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cubic spline on 2 points with a not-a-knot end condition: QuantLib's rows for that condition read
/// <c>dx_[1]</c> and <c>dx_[n_-3]</c> (<c>cubicinterpolation.hpp:428-431,460-463</c>), past the end of <c>dx_</c>,
/// whose one element is the only interval, a result C++ leaves undefined. nanoql refuses it with
/// <see cref="NanoQLException"/>, directly and through the log-cubic interpolation, which builds the same spline.
/// </summary>
public class CubicNotAKnotTwoPointsTests
{
    [Fact]
    public void ANotAKnotSplineOnTwoPointsIsRefused()
    {
        double[] x = [1.0, 2.0], y = [0.5, 0.75];
        const CubicInterpolationDerivativeApprox spline = CubicInterpolationDerivativeApprox.Spline;
        const CubicInterpolationBoundaryCondition notAKnot = CubicInterpolationBoundaryCondition.NotAKnot;
        const CubicInterpolationBoundaryCondition second = CubicInterpolationBoundaryCondition.SecondDerivative;

        Assert.Throws<NanoQLException>(() => new CubicInterpolation(x, y, spline, false, notAKnot, 0.0, second, 0.0));
        Assert.Throws<NanoQLException>(() => new CubicInterpolation(x, y, spline, false, second, 0.0, notAKnot, 0.0));
        Assert.Throws<NanoQLException>(() => new LogCubicInterpolation(x, y, spline, false, notAKnot, 0.0, notAKnot, 0.0));

        // A left end QuantLib refuses before it reaches the right end's reads keeps QuantLib's own refusal.
        Assert.Equal(
            "this end condition is not implemented yet",
            Assert.Throws<NanoQLException>(() => new CubicInterpolation(
                x, y, spline, false, CubicInterpolationBoundaryCondition.Periodic, 0.0, notAKnot, 0.0)).Message);
        Assert.Equal(
            "the not-a-knot end condition needs at least 3 points",
            Assert.Throws<NanoQLException>(() => new CubicInterpolation(
                x, y, spline, false, notAKnot, 0.0, CubicInterpolationBoundaryCondition.Periodic, 0.0)).Message);

        // Three points (one not-a-knot end: both on three is singular, which QuantLib's solve refuses too), and the
        // other end conditions on two, stay QuantLib's.
        _ = new CubicInterpolation([1.0, 2.0, 3.0], [0.5, 0.75, 0.6], spline, false, notAKnot, 0.0, second, 0.0);
        _ = new CubicInterpolation(x, y, spline, false, second, 0.0, second, 0.0);
    }
}
