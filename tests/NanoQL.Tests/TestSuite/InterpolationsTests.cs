// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/interpolations.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testSplineErrorOnGaussianValues (SimpsonIntegral, B4.5); testLagrangeInterpolation, testLagrangeInterpolationAtSupportPoint, testLagrangeInterpolationDerivative, testLagrangeInterpolationOnChebyshevPoints, testChebyshevInterpolation, testChebyshevInterpolationOnNodes, testChebyshevInterpolationUpdateY (B4.5); testMultiSpline (B4.7); testForwardFlat (S-cds); testMixedLinearCubicMatchDerivatives, testMixedLinearCubicSwitchPoint (S5); testSabrInterpolation, testSabrGuess, testSabrSingleCases, testFlochKennedySabrIsSmoothAroundATM, testLeFlochKennedySabrExample (S7); testBicubicDerivatives, testBicubicUpdate (S8); testFlatExtrapolation (B3.1); testBSplines (B3.5); testKernelInterpolation, testKernelInterpolation2D, testUnknownRichardsonExtrapolation, testRichardsonExtrapolation (B6.2); testLaplaceInterpolation (B7.3); testNoArbSabrInterpolation, testTransformations (B7.5)
// Replaced: testInterpolateWithoutUpdate (Conventions/InterpolationConstructionTests.cs)

using NanoQL.Numerics;
using NanoQL.Utilities;
using static NanoQL.Numerics.CubicInterpolationBoundaryCondition;
using static NanoQL.Numerics.CubicInterpolationDerivativeApprox;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class InterpolationsTests
{
    // QuantLib's Null<Real>(), the largest float, which the tests pass as the ignored value of a not-a-knot condition.
    private const Real NullReal = 3.4028234663852886e38;

    [Fact]
    public void TestSplineOnGaussianValues()
    {
        // Testing spline interpolation on a Gaussian data set...

        Real interpolated, interpolated2;
        Size n = 5;

        Real[] x, y;
        Real x1_bad = -1.7, x2_bad = 1.7;

        for (Real start = -1.9, j = 0; j < 2; start += 0.2, j++)
        {
            x = XRange(start, start + 3.6, n);
            y = Gaussian(x);

            // Not-a-knot spline
            var f = new CubicInterpolation(x, y, Spline, false, NotAKnot, NullReal, NotAKnot, NullReal);
            CheckValues("Not-a-knot spline", f, x, y);
            CheckNotAKnotCondition("Not-a-knot spline", f);

            // bad performance
            interpolated = f.Evaluate(x1_bad);
            interpolated2 = f.Evaluate(x2_bad);
            if (interpolated > 0.0 && interpolated2 > 0.0)
            {
                Assert.Fail(
                    "Not-a-knot spline interpolation bad performance unverified"
                    + $"\nat x = {x1_bad} interpolated value: {interpolated}"
                    + $"\nat x = {x2_bad} interpolated value: {interpolated}"
                    + "\n at least one of them was expected to be < 0.0");
            }

            // MC not-a-knot spline
            f = new CubicInterpolation(x, y, Spline, true, NotAKnot, NullReal, NotAKnot, NullReal);
            CheckValues("MC not-a-knot spline", f, x, y);

            // good performance
            interpolated = f.Evaluate(x1_bad);
            if (interpolated < 0.0)
            {
                Assert.Fail(
                    "MC not-a-knot spline interpolation good performance unverified\n"
                    + $"at x = {x1_bad}\ninterpolated value: {interpolated}\nexpected value > 0.0");
            }

            interpolated = f.Evaluate(x2_bad);
            if (interpolated < 0.0)
            {
                Assert.Fail(
                    "MC not-a-knot spline interpolation good performance unverified\n"
                    + $"at x = {x2_bad}\ninterpolated value: {interpolated}\nexpected value > 0.0");
            }
        }
    }

    [Fact]
    public void TestSplineOnRPN15AValues()
    {
        // Testing spline interpolation on RPN15A data set...

        Real[] RPN15A_x =
        [
            7.99, 8.09, 8.19, 8.7,
            9.2, 10.0, 12.0, 15.0, 20.0,
        ];
        Real[] RPN15A_y =
        [
            0.0, 2.76429e-5, 4.37498e-5, 0.169183,
            0.469428, 0.943740, 0.998636, 0.999919, 0.999994,
        ];

        Real interpolated;

        // Natural spline
        var f = new CubicInterpolation(RPN15A_x, RPN15A_y, Spline, false, SecondDerivative, 0.0, SecondDerivative, 0.0);
        CheckValues("Natural spline", f, RPN15A_x, RPN15A_y);
        Check2ndDerivativeValue("Natural spline", f, RPN15A_x[0], 0.0);
        Check2ndDerivativeValue("Natural spline", f, RPN15A_x[^1], 0.0);

        // poor performance
        Real x_bad = 11.0;
        interpolated = f.Evaluate(x_bad);
        if (interpolated < 1.0)
        {
            Assert.Fail(
                "Natural spline interpolation poor performance unverified\n"
                + $"at x = {x_bad}\ninterpolated value: {interpolated}\nexpected value > 1.0");
        }

        // Clamped spline
        f = new CubicInterpolation(RPN15A_x, RPN15A_y, Spline, false, FirstDerivative, 0.0, FirstDerivative, 0.0);
        CheckValues("Clamped spline", f, RPN15A_x, RPN15A_y);
        Check1stDerivativeValue("Clamped spline", f, RPN15A_x[0], 0.0);
        Check1stDerivativeValue("Clamped spline", f, RPN15A_x[^1], 0.0);

        // poor performance
        interpolated = f.Evaluate(x_bad);
        if (interpolated < 1.0)
        {
            Assert.Fail(
                "Clamped spline interpolation poor performance unverified\n"
                + $"at x = {x_bad}\ninterpolated value: {interpolated}\nexpected value > 1.0");
        }

        // Not-a-knot spline
        f = new CubicInterpolation(RPN15A_x, RPN15A_y, Spline, false, NotAKnot, NullReal, NotAKnot, NullReal);
        CheckValues("Not-a-knot spline", f, RPN15A_x, RPN15A_y);
        CheckNotAKnotCondition("Not-a-knot spline", f);

        // poor performance
        interpolated = f.Evaluate(x_bad);
        if (interpolated < 1.0)
        {
            Assert.Fail(
                "Not-a-knot spline interpolation poor performance unverified\n"
                + $"at x = {x_bad}\ninterpolated value: {interpolated}\nexpected value > 1.0");
        }

        // MC natural spline values
        f = new CubicInterpolation(RPN15A_x, RPN15A_y, Spline, true, SecondDerivative, 0.0, SecondDerivative, 0.0);
        CheckValues("MC natural spline", f, RPN15A_x, RPN15A_y);

        // good performance
        interpolated = f.Evaluate(x_bad);
        if (interpolated > 1.0)
        {
            Assert.Fail(
                "MC natural spline interpolation good performance unverified\n"
                + $"at x = {x_bad}\ninterpolated value: {interpolated}\nexpected value < 1.0");
        }

        // MC clamped spline values
        f = new CubicInterpolation(RPN15A_x, RPN15A_y, Spline, true, FirstDerivative, 0.0, FirstDerivative, 0.0);
        CheckValues("MC clamped spline", f, RPN15A_x, RPN15A_y);
        Check1stDerivativeValue("MC clamped spline", f, RPN15A_x[0], 0.0);
        Check1stDerivativeValue("MC clamped spline", f, RPN15A_x[^1], 0.0);

        // good performance
        interpolated = f.Evaluate(x_bad);
        if (interpolated > 1.0)
        {
            Assert.Fail(
                "MC clamped spline interpolation good performance unverified\n"
                + $"at x = {x_bad}\ninterpolated value: {interpolated}\nexpected value < 1.0");
        }

        // MC not-a-knot spline values
        f = new CubicInterpolation(RPN15A_x, RPN15A_y, Spline, true, NotAKnot, NullReal, NotAKnot, NullReal);
        CheckValues("MC not-a-knot spline", f, RPN15A_x, RPN15A_y);

        // good performance
        interpolated = f.Evaluate(x_bad);
        if (interpolated > 1.0)
        {
            Assert.Fail(
                "MC clamped spline interpolation good performance unverified\n"
                + $"at x = {x_bad}\ninterpolated value: {interpolated}\nexpected value < 1.0");
        }
    }

    [Fact]
    public void TestSplineOnGenericValues()
    {
        // Testing spline interpolation on generic values...

        Real[] generic_x = [0.0, 1.0, 3.0, 4.0];
        Real[] generic_y = [0.0, 0.0, 2.0, 2.0];
        Real[] generic_natural_y2 = [0.0, 1.5, -1.5, 0.0];

        Real interpolated, error;
        Size i, n = generic_x.Length;
        var x35 = new Real[3];

        // Natural spline
        var f = new CubicInterpolation(
            generic_x, generic_y, Spline, false, SecondDerivative, generic_natural_y2[0], SecondDerivative, generic_natural_y2[n - 1]);
        CheckValues("Natural spline", f, generic_x, generic_y);

        // cached second derivative
        for (i = 0; i < n; i++)
        {
            interpolated = f.SecondDerivative(generic_x[i]);
            error = interpolated - generic_natural_y2[i];
            if (Math.Abs(error) > 3e-16)
            {
                Assert.Fail(
                    $"Natural spline interpolation second derivative failed at x={generic_x[i]}"
                    + $"\ninterpolated value: {interpolated}\nexpected value:     {generic_natural_y2[i]}\nerror:              {error}");
            }
        }

        x35[1] = f.Evaluate(3.5);

        // Clamped spline
        Real y1a = 0.0, y1b = 0.0;
        f = new CubicInterpolation(generic_x, generic_y, Spline, false, FirstDerivative, y1a, FirstDerivative, y1b);
        CheckValues("Clamped spline", f, generic_x, generic_y);
        Check1stDerivativeValue("Clamped spline", f, generic_x[0], 0.0);
        Check1stDerivativeValue("Clamped spline", f, generic_x[^1], 0.0);
        x35[0] = f.Evaluate(3.5);

        // Not-a-knot spline
        f = new CubicInterpolation(generic_x, generic_y, Spline, false, NotAKnot, NullReal, NotAKnot, NullReal);
        CheckValues("Not-a-knot spline", f, generic_x, generic_y);
        CheckNotAKnotCondition("Not-a-knot spline", f);

        x35[2] = f.Evaluate(3.5);

        if (x35[0] > x35[1] || x35[1] > x35[2])
        {
            Assert.Fail(
                "Spline interpolation failure"
                + $"\nat x = {3.5}\nclamped spline    {x35[0]}\nnatural spline    {x35[1]}\nnot-a-knot spline {x35[2]}"
                + "\nvalues should be in increasing order");
        }
    }

    [Fact]
    public void TestSimmetricEndConditions()
    {
        // Testing symmetry of spline interpolation end-conditions...

        Size n = 9;

        Real[] x, y;
        x = XRange(-1.8, 1.8, n);
        y = Gaussian(x);

        // Not-a-knot spline
        var f = new CubicInterpolation(x, y, Spline, false, NotAKnot, NullReal, NotAKnot, NullReal);
        CheckValues("Not-a-knot spline", f, x, y);
        CheckNotAKnotCondition("Not-a-knot spline", f);
        CheckSymmetry("Not-a-knot spline", f, x[0]);

        // MC not-a-knot spline
        f = new CubicInterpolation(x, y, Spline, true, NotAKnot, NullReal, NotAKnot, NullReal);
        CheckValues("MC not-a-knot spline", f, x, y);
        CheckSymmetry("MC not-a-knot spline", f, x[0]);
    }

    [Fact]
    public void TestDerivativeEndConditions()
    {
        // Testing derivative end-conditions for spline interpolation...

        Size n = 4;

        Real[] x, y;
        x = XRange(-2.0, 2.0, n);
        y = Parabolic(x);

        // Not-a-knot spline
        var f = new CubicInterpolation(x, y, Spline, false, NotAKnot, NullReal, NotAKnot, NullReal);
        CheckValues("Not-a-knot spline", f, x, y);
        Check1stDerivativeValue("Not-a-knot spline", f, x[0], 4.0);
        Check1stDerivativeValue("Not-a-knot spline", f, x[n - 1], -4.0);
        Check2ndDerivativeValue("Not-a-knot spline", f, x[0], -2.0);
        Check2ndDerivativeValue("Not-a-knot spline", f, x[n - 1], -2.0);

        // Clamped spline
        f = new CubicInterpolation(x, y, Spline, false, FirstDerivative, 4.0, FirstDerivative, -4.0);
        CheckValues("Clamped spline", f, x, y);
        Check1stDerivativeValue("Clamped spline", f, x[0], 4.0);
        Check1stDerivativeValue("Clamped spline", f, x[n - 1], -4.0);
        Check2ndDerivativeValue("Clamped spline", f, x[0], -2.0);
        Check2ndDerivativeValue("Clamped spline", f, x[n - 1], -2.0);

        // SecondDerivative spline
        f = new CubicInterpolation(x, y, Spline, false, SecondDerivative, -2.0, SecondDerivative, -2.0);
        CheckValues("SecondDerivative spline", f, x, y);
        Check1stDerivativeValue("SecondDerivative spline", f, x[0], 4.0);
        Check1stDerivativeValue("SecondDerivative spline", f, x[n - 1], -4.0);
        Check2ndDerivativeValue("SecondDerivative spline", f, x[0], -2.0);
        Check2ndDerivativeValue("SecondDerivative spline", f, x[n - 1], -2.0);

        // MC Not-a-knot spline
        f = new CubicInterpolation(x, y, Spline, true, NotAKnot, NullReal, NotAKnot, NullReal);
        CheckValues("MC Not-a-knot spline", f, x, y);
        Check1stDerivativeValue("MC Not-a-knot spline", f, x[0], 4.0);
        Check1stDerivativeValue("MC Not-a-knot spline", f, x[n - 1], -4.0);
        Check2ndDerivativeValue("MC Not-a-knot spline", f, x[0], -2.0);
        Check2ndDerivativeValue("MC Not-a-knot spline", f, x[n - 1], -2.0);

        // MC Clamped spline
        f = new CubicInterpolation(x, y, Spline, true, FirstDerivative, 4.0, FirstDerivative, -4.0);
        CheckValues("MC Clamped spline", f, x, y);
        Check1stDerivativeValue("MC Clamped spline", f, x[0], 4.0);
        Check1stDerivativeValue("MC Clamped spline", f, x[n - 1], -4.0);
        Check2ndDerivativeValue("MC Clamped spline", f, x[0], -2.0);
        Check2ndDerivativeValue("MC Clamped spline", f, x[n - 1], -2.0);

        // MC SecondDerivative spline
        f = new CubicInterpolation(x, y, Spline, true, SecondDerivative, -2.0, SecondDerivative, -2.0);
        CheckValues("MC SecondDerivative spline", f, x, y);
        Check1stDerivativeValue("MC SecondDerivative spline", f, x[0], 4.0);
        Check1stDerivativeValue("MC SecondDerivative spline", f, x[n - 1], -4.0);
        Check2ndDerivativeValue("SecondDerivative spline", f, x[0], -2.0);
        Check2ndDerivativeValue("MC SecondDerivative spline", f, x[n - 1], -2.0);
    }

    [Fact]
    public void TestNonRestrictiveHymanFilter()
    {
        // Testing non-restrictive Hyman filter...

        Size n = 4;

        Real[] x, y;
        x = XRange(-2.0, 2.0, n);
        y = Parabolic(x);
        Real zero = 0.0, interpolated, expected = 0.0;

        // MC Not-a-knot spline
        var f = new CubicInterpolation(x, y, Spline, true, NotAKnot, NullReal, NotAKnot, NullReal);
        interpolated = f.Evaluate(zero);
        if (Math.Abs(interpolated - expected) > 1e-15)
        {
            Assert.Fail(
                $"MC not-a-knot spline interpolation failed at x = {zero}"
                + $"\n    interpolated value: {interpolated}\n    expected value:     {expected}"
                + $"\n    error:              {Math.Abs(interpolated - expected)}");
        }

        // MC Clamped spline
        f = new CubicInterpolation(x, y, Spline, true, FirstDerivative, 4.0, FirstDerivative, -4.0);
        interpolated = f.Evaluate(zero);
        if (Math.Abs(interpolated - expected) > 1e-15)
        {
            Assert.Fail(
                $"MC clamped spline interpolation failed at x = {zero}"
                + $"\n    interpolated value: {interpolated}\n    expected value:     {expected}"
                + $"\n    error:              {Math.Abs(interpolated - expected)}");
        }

        // MC SecondDerivative spline
        f = new CubicInterpolation(x, y, Spline, true, SecondDerivative, -2.0, SecondDerivative, -2.0);
        interpolated = f.Evaluate(zero);
        if (Math.Abs(interpolated - expected) > 1e-15)
        {
            Assert.Fail(
                $"MC SecondDerivative spline interpolation failed at x = {zero}"
                + $"\n    interpolated value: {interpolated}\n    expected value:     {expected}"
                + $"\n    error:              {Math.Abs(interpolated - expected)}");
        }
    }

    // Adapted: enableExtrapolation (line 1006), which changes QuantLib's interpolation, returns a modified copy here
    // (playbook §2), which f is rebound to; std::transform's calls of the functor are a loop calling Evaluate, and the
    // NotThrown marker a flag.
    [Fact]
    public void TestAsFunctor()
    {
        // Testing use of interpolations as functors...

        Real[] x = [0.0, 1.0, 2.0, 3.0, 4.0];
        Real[] y = [5.0, 4.0, 3.0, 2.0, 1.0];

        Interpolation f = new LinearInterpolation(x, y);

        Real[] x2 = [-2.0, -1.0, 0.0, 1.0, 3.0, 4.0, 5.0, 6.0, 7.0];
        Size N = x2.Length;
        var y2 = new Real[N];
        Real tolerance = 1.0e-12;

        // case 1: extrapolation not allowed
        bool thrown = false;
        try
        {
            for (Size i = 0; i < N; i++)
            {
                y2[i] = f.Evaluate(x2[i]);
            }
        }
        catch (NanoQLException)
        {
            // as expected; do nothing
            thrown = true;
        }

        if (!thrown)
        {
            Assert.Fail("failed to throw exception when trying to extrapolate");
        }

        // case 2: enable extrapolation
        f = f.EnableExtrapolation();
        y2 = new Real[N];
        for (Size i = 0; i < N; i++)
        {
            y2[i] = f.Evaluate(x2[i]);
        }

        for (Size i = 0; i < N; i++)
        {
            Real expected = 5.0 - x2[i];
            if (Math.Abs(y2[i] - expected) > tolerance)
            {
                Assert.Fail(
                    $"failed to reproduce {DataFormattersFunctions.Ordinal(i + 1)} expected datum"
                    + $"\n    expected:   {expected:F6}\n    calculated: {y2[i]:F6}\n    error:      {Math.Abs(y2[i] - expected):E}");
            }
        }
    }

    [Fact]
    public void TestFritschButland()
    {
        // Testing Fritsch-Butland interpolation...

        Real[] x = [0.0, 1.0, 2.0, 3.0, 4.0];
        Real[][] y =
        [
            [1.0, 2.0, 1.0, 1.0, 2.0],
            [1.0, 2.0, 1.0, 1.0, 1.0],
            [2.0, 1.0, 0.0, 2.0, 3.0],
        ];

        for (Size i = 0; i < 3; ++i)
        {
            Interpolation f = new FritschButlandCubic(x, y[i]);

            for (Size j = 0; j < 4; ++j)
            {
                Real left_knot = x[j];
                Integer expected_sign = Sign(y[i][j], y[i][j + 1]);
                for (Size k = 0; k < 10; ++k)
                {
                    Real x1 = left_knot + (k * 0.1), x2 = left_knot + ((k + 1) * 0.1);
                    Real y1 = f.Evaluate(x1), y2 = f.Evaluate(x2);
                    if (double.IsNaN(y1))
                    {
                        Assert.Fail($"NaN detected in case {i}:\n    f({x1:F6}) = {y1:F6}");
                    }
                    else if (Sign(y1, y2) != expected_sign)
                    {
                        Assert.Fail(
                            $"interpolation is not monotonic in case {i}:"
                            + $"\n    f({x1:F6}) = {y1:F6}\n    f({x2:F6}) = {y2:F6}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestAkimaWithFewPoints()
    {
        // Testing Akima interpolation with few points...

        // The Akima scheme reads S_[2] and S_[n-4] while computing the first-derivative estimates; with three points
        // S_ has size two and both indices are out of bounds. Construction must fail cleanly rather than read past the
        // end of the vector.
        Real[] x3 = [0.0, 1.0, 2.0];
        Real[] y3 = [1.0, 2.0, 0.5];
        ArgumentException e = Assert.Throws<ArgumentException>(() => new AkimaCubicInterpolation(x3, y3));
        Assert.Contains("Akima approximation requires at least 4 points", e.Message, StringComparison.Ordinal);

        // Four points are enough; the interpolation must reproduce the knots.
        Real[] x4 = [0.0, 1.0, 2.0, 3.0];
        Real[] y4 = [1.0, 2.0, 0.5, 1.5];
        var f = new AkimaCubicInterpolation(x4, y4);
        for (Size i = 0; i < 4; ++i)
        {
            CheckClose(f.Evaluate(x4[i]), y4[i], 1.0e-12);
        }
    }

    // Adapted: enableExtrapolation (line 1124), which changes QuantLib's interpolation, returns a modified copy here
    // (playbook §2), which f is rebound to.
    [Fact]
    public void TestBackwardFlat()
    {
        // Testing backward-flat interpolation...

        Real[] x = [0.0, 1.0, 2.0, 3.0, 4.0];
        Real[] y = [5.0, 4.0, 3.0, 2.0, 1.0];

        Interpolation f = new BackwardFlatInterpolation(x, y);

        Size N = x.Length;
        Size i;
        Real tolerance = 1.0e-12;

        // at original points
        for (i = 0; i < N; i++)
        {
            Real p = x[i];
            Real calculated = f.Evaluate(p);
            Real expected = y[i];
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to reproduce {DataFormattersFunctions.Ordinal(i + 1)} datum"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // at middle points
        for (i = 0; i < N - 1; i++)
        {
            Real p = (x[i] + x[i + 1]) / 2;
            Real calculated = f.Evaluate(p);
            Real expected = y[i + 1];
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to interpolate correctly at {p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // outside the original range
        f = f.EnableExtrapolation();

        {
            Real p = x[0] - 0.5;
            Real calculated = f.Evaluate(p);
            Real expected = y[0];
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to extrapolate correctly at {p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }

            p = x[N - 1] + 0.5;
            calculated = f.Evaluate(p);
            expected = y[N - 1];
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to extrapolate correctly at {p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }

            // primitive at original points
            calculated = f.Primitive(x[0]);
            expected = 0.0;
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to calculate primitive at {x[0]}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        Real sum = 0.0;
        for (i = 1; i < N; i++)
        {
            sum += (x[i] - x[i - 1]) * y[i];
            Real calculated = f.Primitive(x[i]);
            Real expected = sum;
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to calculate primitive at {x[i]}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // primitive at middle points
        sum = 0.0;
        for (i = 0; i < N - 1; i++)
        {
            Real p = (x[i] + x[i + 1]) / 2;
            sum += (x[i + 1] - x[i]) * y[i + 1] / 2;
            Real calculated = f.Primitive(p);
            Real expected = sum;
            sum += (x[i + 1] - x[i]) * y[i + 1] / 2;
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to calculate primitive at {x[i]}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }
    }

    [Fact]
    public void TestBackwardFlatOnSinglePoint()
    {
        // Testing piecewise constant interpolation on a single point...
        Real[] knots = [1.0], values = [2.5];

        Interpolation impl = new BackwardFlat().Interpolate(knots, values);

        Real[] x = [-1.0, 1.0, 2.0, 3.0];

        foreach (Real i in x)
        {
            Real calculated = impl.Evaluate(i, true);
            Real expected = values[0];

            if (!ComparisonFunctions.CloseEnough(calculated, expected))
            {
                Assert.Fail(
                    "failed to reproduce a piecewise constant interpolation on a single point "
                    + $"\n   x         : {i}\n   expected  : {expected}\n   calculated: {calculated}");
            }

            Real expectedPrimitive = values[0] * (i - knots[0]);
            Real calculatedPrimitive = impl.Primitive(i, true);

            if (!ComparisonFunctions.CloseEnough(calculatedPrimitive, expectedPrimitive))
            {
                Assert.Fail(
                    "failed to reproduce primitive on a piecewise constant interpolation for a single point "
                    + $"\n   x         : {i}\n   expected  : {expectedPrimitive}\n   calculated: {calculatedPrimitive}");
            }
        }
    }

    private static Real[] XRange(Real start, Real finish, Size points)
    {
        var x = new Real[points];
        Real dx = (finish - start) / (points - 1);
        for (Size i = 0; i < points - 1; i++)
        {
            x[i] = start + (i * dx);
        }

        x[points - 1] = finish;
        return x;
    }

    private static Real[] Gaussian(Real[] x)
    {
        var y = new Real[x.Length];
        for (Size i = 0; i < x.Length; i++)
        {
            y[i] = Math.Exp(-x[i] * x[i]);
        }

        return y;
    }

    private static Real[] Parabolic(Real[] x)
    {
        var y = new Real[x.Length];
        for (Size i = 0; i < x.Length; i++)
        {
            y[i] = -x[i] * x[i];
        }

        return y;
    }

    private static void CheckValues(string type, CubicInterpolation cubic, Real[] x, Real[] y)
    {
        Real tolerance = 2.0e-15;
        for (Size i = 0; i < x.Length; i++)
        {
            Real interpolated = cubic.Evaluate(x[i]);
            if (Math.Abs(interpolated - y[i]) > tolerance)
            {
                Assert.Fail(
                    $"{type} interpolation failed at x = {x[i]}"
                    + $"\n    interpolated value: {interpolated:E}\n    expected value:     {y[i]:E}"
                    + $"\n    error:              {Math.Abs(interpolated - y[i]):E}");
            }
        }
    }

    private static void Check1stDerivativeValue(string type, CubicInterpolation cubic, Real x, Real value)
    {
        Real tolerance = 1.0e-14;
        Real interpolated = cubic.Derivative(x);
        Real error = Math.Abs(interpolated - value);
        if (error > tolerance)
        {
            Assert.Fail(
                $"{type} interpolation first derivative failure\nat x = {x}"
                + $"\n    interpolated value: {interpolated}\n    expected value:     {value}\n    error:              {error:E}");
        }
    }

    private static void Check2ndDerivativeValue(string type, CubicInterpolation cubic, Real x, Real value)
    {
        Real tolerance = 1.0e-13;
        Real interpolated = cubic.SecondDerivative(x);
        Real error = Math.Abs(interpolated - value);
        if (error > tolerance)
        {
            Assert.Fail(
                $"{type} interpolation second derivative failure\nat x = {x}"
                + $"\n    interpolated value: {interpolated}\n    expected value:     {value}\n    error:              {error:E}");
        }
    }

    private static void CheckNotAKnotCondition(string type, CubicInterpolation cubic)
    {
        Real tolerance = 1.0e-14;
        var c = cubic.CCoefficients();
        if (Math.Abs(c[0] - c[1]) > tolerance)
        {
            Assert.Fail(
                $"{type} interpolation failure"
                + $"\n    cubic coefficient of the first polinomial is {c[0]}"
                + $"\n    cubic coefficient of the second polinomial is {c[1]}");
        }

        Size n = c.Length;
        if (Math.Abs(c[n - 2] - c[n - 1]) > tolerance)
        {
            Assert.Fail(
                $"{type} interpolation failure"
                + $"\n    cubic coefficient of the 2nd to last polinomial is {c[n - 2]}"
                + $"\n    cubic coefficient of the last polinomial is {c[n - 1]}");
        }
    }

    private static void CheckSymmetry(string type, CubicInterpolation cubic, Real xMin)
    {
        Real tolerance = 1.0e-15;
        for (Real x = xMin; x < 0.0; x += 0.1)
        {
            Real y1 = cubic.Evaluate(x), y2 = cubic.Evaluate(-x);
            if (Math.Abs(y1 - y2) > tolerance)
            {
                Assert.Fail(
                    $"{type} interpolation not symmetric"
                    + $"\n    x = {x}\n    g(x)  = {y1}\n    g(-x) = {y2}\n    error:  {Math.Abs(y1 - y2)}");
            }
        }
    }

    private static Integer Sign(Real y1, Real y2) =>
        y1 == y2 ? 0 :
        y1 < y2 ? 1 :
        -1;

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b|/|b| and |a - b|/|a| each within percent/100, with its safe
    // division.
    private static void CheckClose(Real a, Real b, Real percent)
    {
        Real diff = Math.Abs(a - b), tolerance = 0.01 * percent;
        bool close = SafeDivision(diff, Math.Abs(b)) <= tolerance && SafeDivision(diff, Math.Abs(a)) <= tolerance;
        Assert.True(close, $"difference between {a:R} and {b:R} exceeds {percent}%");
    }

    private static Real SafeDivision(Real f1, Real f2)
    {
        // Avoid overflow.
        if (f2 < 1 && f1 > f2 * double.MaxValue)
        {
            return double.MaxValue;
        }

        // Avoid underflow.
        if (f1 == 0 || (f2 > 1 && f1 < f2 * 2.2250738585072014E-308))
        {
            return 0;
        }

        return f1 / f2;
    }
}
