// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cubic spline on 2 points with a not-a-knot end condition: QuantLib 1.44's rows for that condition read
/// <c>dx_[1]</c> and <c>dx_[n_-3]</c> (<c>cubicinterpolation.hpp:428-431,460-463</c>), past the end of <c>dx_</c>,
/// whose one element is the only interval, a result C++ leaves undefined, and on 3 points two not-a-knot ends make the
/// system singular. QuantLib's master refuses both at construction, a not-a-knot end needing 3 points and two needing 4
/// (#2888, <c>7b21b6145</c>), which nanoql takes: an <see cref="ArgumentException"/> on <c>x</c> with QuantLib's text,
/// as nanoql maps the constructor's Lagrange and Akima counts beside it, directly and through the log-cubic
/// interpolation, which builds the same spline. Its place is master's, among the constructor's checks, so it comes
/// before a Periodic end's "not implemented yet", which update raises. One departure from master: the count applies
/// under the spline alone, where master's applies under every scheme, as only the spline's rows read the conditions
/// (<c>cubicinterpolation.hpp:425</c>, <c>:457</c>), so the local and overshooting schemes keep QuantLib 1.44's values
/// on 2 and 3 points, which the C++ vectors check, where master refuses them; a refusal of inputs 1.44 prices right
/// fixes no defect.
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

        AssertCount(3, () => new CubicInterpolation(x, y, spline, false, notAKnot, 0.0, second, 0.0));
        AssertCount(3, () => new CubicInterpolation(x, y, spline, false, second, 0.0, notAKnot, 0.0));
        AssertCount(4, () => new LogCubicInterpolation(x, y, spline, false, notAKnot, 0.0, notAKnot, 0.0));

        // The count is the constructor's, so it comes before a Periodic end's refusal, which update raises.
        AssertCount(3, () => new CubicInterpolation(x, y, spline, false, CubicInterpolationBoundaryCondition.Periodic, 0.0, notAKnot, 0.0));
        AssertCount(3, () => new CubicInterpolation(x, y, spline, false, notAKnot, 0.0, CubicInterpolationBoundaryCondition.Periodic, 0.0));

        // Under a scheme that doesn't read the conditions, the not-a-knot ends are 1.44's: the scheme's own values on
        // 2 and 3 points, where QuantLib's master refuses them.
        foreach (CubicInterpolationDerivativeApprox local in new[]
            { CubicInterpolationDerivativeApprox.Kruger, CubicInterpolationDerivativeApprox.SplineOM1 })
        {
            (double[] X, double[] Y)[] sets = [(x, y), ([1.0, 2.0, 3.0], [0.5, 0.75, 0.6])];
            foreach ((double[] xs, double[] ys) in sets)
            {
                var knots = new CubicInterpolation(xs, ys, local, false, notAKnot, 0.0, notAKnot, 0.0);
                var natural = new CubicInterpolation(xs, ys, local, false, second, 0.0, second, 0.0);
                Assert.Equal(natural.ACoefficients(), knots.ACoefficients());
                Assert.Equal(natural.BCoefficients(), knots.BCoefficients());
                Assert.Equal(natural.CCoefficients(), knots.CCoefficients());
            }
        }

        // Three points with one not-a-knot end, four with two, and the other end conditions on two stay QuantLib's.
        _ = new CubicInterpolation([1.0, 2.0, 3.0], [0.5, 0.75, 0.6], spline, false, notAKnot, 0.0, second, 0.0);
        _ = new CubicInterpolation([1.0, 2.0, 3.0, 4.0], [0.5, 0.75, 0.6, 0.9], spline, false, notAKnot, 0.0, notAKnot, 0.0);
        _ = new CubicInterpolation(x, y, spline, false, second, 0.0, second, 0.0);
    }

    [Fact]
    public void TestNotAKnotWithFewPoints()
    {
        // Upstream: QuantLib's master's case for #2888 (interpolations.cpp's testNotAKnotWithFewPoints)
        const CubicInterpolationDerivativeApprox spline = CubicInterpolationDerivativeApprox.Spline;
        const CubicInterpolationBoundaryCondition notAKnot = CubicInterpolationBoundaryCondition.NotAKnot;
        const CubicInterpolationBoundaryCondition second = CubicInterpolationBoundaryCondition.SecondDerivative;

        // The not-a-knot conditions read dx_[1], S_[1] and dx_[n-3]; with two points dx_ and S_ have size one and these
        // indices are out of bounds. Construction must fail cleanly rather than read past the end.
        double[] x2 = [1.0, 2.0], y2 = [1.0, 3.0];
        ArgumentException e2 = Assert.Throws<ArgumentException>(
            () => new CubicInterpolation(x2, y2, spline, false, second, 0.0, notAKnot, double.NaN));
        Assert.Equal("x", e2.ParamName);
        Assert.Contains("not-a-knot boundary condition requires at least 3 points", e2.Message);

        // With three points, not-a-knot at both ends gives a singular system.
        double[] x3 = [0.0, 1.0, 2.0], y3 = [1.0, 2.0, 0.5];
        ArgumentException e3 = Assert.Throws<ArgumentException>(
            () => new CubicInterpolation(x3, y3, spline, false, notAKnot, double.NaN, notAKnot, double.NaN));
        Assert.Equal("x", e3.ParamName);
        Assert.Contains("not-a-knot boundary condition requires at least 4 points", e3.Message);

        // Three points are enough for not-a-knot at one end only...
        var f = new CubicInterpolation(x3, y3, spline, false, notAKnot, double.NaN, second, 0.0);
        for (int i = 0; i < 3; ++i)
        {
            Assert.Equal(y3[i], f.Evaluate(x3[i]), 1e-12);
        }

        // ...and four for not-a-knot at both ends.
        double[] x4 = [0.0, 1.0, 2.0, 3.0], y4 = [1.0, 2.0, 0.5, 1.5];
        var g = new CubicInterpolation(x4, y4, spline, false, notAKnot, double.NaN, notAKnot, double.NaN);
        for (int i = 0; i < 4; ++i)
        {
            Assert.Equal(y4[i], g.Evaluate(x4[i]), 1e-12);
        }
    }

    /// <summary>
    /// A bootstrapped cubic curve with a not-a-knot end: on its first node QuantLib 1.44's bootstrap builds the spline
    /// over two points, whose update reads past <c>dx_</c>'s end. The master's refusal reads only the configuration and
    /// the point count, one of the cubic's construction checks (<see cref="Cubic.CheckPoints"/>): the bootstrap's first
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
        AssertCount(3, () => cubic.CheckPoints(2));

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

    private static void AssertCount(int required, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal("x", e.ParamName);
        Assert.StartsWith($"not-a-knot boundary condition requires at least {required} points", e.Message, StringComparison.Ordinal);
    }
}
