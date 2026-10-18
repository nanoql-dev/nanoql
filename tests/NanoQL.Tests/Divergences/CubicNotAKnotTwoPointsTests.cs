// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

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

    /// <summary>
    /// A bootstrapped cubic curve with a not-a-knot end: on its first node QuantLib's bootstrap builds the spline over
    /// two points, whose update reads past <c>dx_</c>'s end. nanoql's refusal reads only the configuration and the point
    /// count, so it is one of the cubic's construction checks (<see cref="Cubic.CheckPoints"/>): the bootstrap's first
    /// pass takes linear interpolation on that node, as QuantLib takes it for a global interpolation that can't be
    /// built over the first nodes, and the curve reprices its helpers.
    /// </summary>
    [Fact]
    public void ACubicCurveWithANotAKnotEndBuildsThroughTheFirstPassFallback()
    {
        var cubic = new Cubic(
            CubicInterpolationDerivativeApprox.Spline,
            false,
            CubicInterpolationBoundaryCondition.NotAKnot,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);
        Assert.Throws<NanoQLException>(() => cubic.CheckPoints(2));

        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.039, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, true,
                new Actual360(), context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.035), (5, 0.0325), (10, 0.032) })
        {
            helpers.Add(new SwapRateHelper(
                rate,
                new Period(years, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                new Euribor6M(),
                context));
        }

        var curve = new PiecewiseYieldCurve<ZeroYield, Cubic, IterativeBootstrap>(
            today, helpers, new Actual365Fixed(), context, i: cubic);

        foreach (RateHelper helper in helpers)
        {
            Assert.True(Math.Abs(helper.SetTermStructure(curve).QuoteError(context)) < 1e-10);
        }
    }
}
