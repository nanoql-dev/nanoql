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
// Partial: testLaplaceInterpolation (S-xrt); testNoArbSabrInterpolation, testTransformations (S-xeq)
// Replaced: testInterpolateWithoutUpdate (Conventions/InterpolationConstructionTests.cs); testBicubicUpdate (Conventions/InterpolationConstructionTests.cs)

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Utilities;
using static NanoQL.Numerics.CubicInterpolationBoundaryCondition;
using static NanoQL.Numerics.CubicInterpolationDerivativeApprox;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class InterpolationsTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // QuantLib's Null<Real>(), the largest float, which the tests pass as the ignored value of a not-a-knot condition.
    private const Real NullReal = 3.4028234663852886e38;

    // mathconstants.hpp's M_PI, which nanoql keeps internal.
    private const double M_PI = 3.141592653589793238462643383280;

    [Fact]
    public void TestMultiSpline()
    {
        // Testing N-dimensional cubic spline...

        Size[] dim = [6, 5, 5, 6, 4];

        Real[] args = new Real[5], offsets = [1.005, 14.0, 33.005, 35.025, 19.025];
        offsets.CopyTo(args, 0);

        Size i, j, k, l, m;

        var grid = new List<Real>[5];

        Real r = 0.15;

        for (i = 0; i < 5; ++i)
        {
            grid[i] = [];
            Real temp = offsets[i];
            for (j = 0; j < dim[i]; temp += r, ++j)
            {
                grid[i].Add(temp);
            }
        }

        // MultiCubicSpline<5>::data_table, flattened with the first dimension slowest
        Real[] y5 = new Real[dim[0] * dim[1] * dim[2] * dim[3] * dim[4]];
        int Index(Size a, Size b, Size c, Size d, Size e) =>
            ((((((((a * dim[1]) + b) * dim[2]) + c) * dim[3]) + d) * dim[4]) + e);

        for (i = 0; i < dim[0]; ++i)
        {
            for (j = 0; j < dim[1]; ++j)
            {
                for (k = 0; k < dim[2]; ++k)
                {
                    for (l = 0; l < dim[3]; ++l)
                    {
                        for (m = 0; m < dim[4]; ++m)
                        {
                            y5[Index(i, j, k, l, m)] =
                                Multif(grid[0][i], grid[1][j], grid[2][k], grid[3][l], grid[4][m]);
                        }
                    }
                }
            }
        }

        var cs = new MultiCubicSpline([.. grid.Select(g => g.ToImmutableArray())], y5);
        /* it would fail with
        for (i = 0; i < dim[0]; ++i)
            for (j = 0; j < dim[1]; ++j)
                for (k = 0; k < dim[2]; ++k)
                    for (l = 0; l < dim[3]; ++l)
                        for (m = 0; m < dim[4]; ++m) {
        */
        var errors = new List<string>();
        for (i = 1; i < dim[0] - 1; ++i)
        {
            for (j = 1; j < dim[1] - 1; ++j)
            {
                for (k = 1; k < dim[2] - 1; ++k)
                {
                    for (l = 1; l < dim[3] - 1; ++l)
                    {
                        for (m = 1; m < dim[4] - 1; ++m)
                        {
                            args[0] = grid[0][i];
                            args[1] = grid[1][j];
                            args[2] = grid[2][k];
                            args[3] = grid[3][l];
                            args[4] = grid[4][m];
                            Real interpolated = cs.Evaluate(args);
                            Real expected = y5[Index(i, j, k, l, m)];
                            Real error = Math.Abs(interpolated - expected);
                            Real tolerance = 1e-16;
                            if (error > tolerance)
                            {
                                errors.Add(
                                    $"\n  At ({args[0]},{args[1]},{args[2]},{args[3]},{args[4]}):"
                                    + $"\n    interpolated: {interpolated}\n    actual value: {expected}"
                                    + $"\n       error: {error}\n    tolerance: {tolerance}");
                            }
                        }
                    }
                }
            }
        }

        long seed = 42;
        var rsg = new SobolRsg(5, seed);

        Real tolerance2 = 1.7e-4;
        // actually tested up to 2^21-1=2097151 Sobol draws
        for (i = 0; i < 1023; ++i)
        {
            ReadOnlySpan<Real> next = rsg.NextSequence().Value.Span;
            for (int d = 0; d < 5; ++d)
            {
                args[d] = grid[d][0] + (next[d] * (grid[d][^1] - grid[d][0]));
            }

            Real interpolated = cs.Evaluate(args), expected = Multif(args[0], args[1], args[2], args[3], args[4]);
            Real error = Math.Abs(interpolated - expected);
            if (error > tolerance2)
            {
                errors.Add(
                    $"\n  At ({args[0]},{args[1]},{args[2]},{args[3]},{args[4]}):"
                    + $"\n    interpolated: {interpolated}\n    actual value: {expected}"
                    + $"\n    error:        {error}\n    tolerance:    {tolerance2}");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestSplineErrorOnGaussianValues()
    {
        // Testing spline approximation on Gaussian data sets...

        Size[] points = [5, 9, 17, 33];

        // complete spline data from the original 1983 Hyman paper
        Real[] tabulatedErrors = [3.5e-2, 2.0e-3, 4.0e-5, 1.8e-6];
        Real[] toleranceOnTabErr = [0.1e-2, 0.1e-3, 0.1e-5, 0.1e-6];

        // (complete) MC spline data from the original 1983 Hyman paper
        // NB: with the improved Hyman filter from the Dougherty, Edelman, and
        //     Hyman 1989 paper the n=17 nonmonotonicity is not filtered anymore
        //     so the error agrees with the non MC method.
        Real[] tabulatedMCErrors = [1.7e-2, 2.0e-3, 4.0e-5, 1.8e-6];
        Real[] toleranceOnTabMCErr = [0.1e-2, 0.1e-3, 0.1e-5, 0.1e-6];

        var integral = new SimpsonIntegral(1e-12, 10000);

        // still unexplained scale factor needed to obtain the numerical
        // results from the paper
        Real scaleFactor = 1.9;

        for (Size i = 0; i < points.Length; i++)
        {
            Size n = points[i];
            Real[] x = XRange(-1.7, 1.9, n);
            Real[] y = Gaussian(x);

            // Not-a-knot
            var f = new CubicInterpolation(x, y, Spline, false, NotAKnot, NullReal, NotAKnot, NullReal);
            Real result = Math.Sqrt(integral.Integrate(MakeErrorFunction(f), -1.7, 1.9));
            result /= scaleFactor;
            if (Math.Abs(result - tabulatedErrors[i]) > toleranceOnTabErr[i])
            {
                Assert.Fail(
                    "Not-a-knot spline interpolation "
                    + $"\n    sample points:      {n}"
                    + $"\n    norm of difference: {result}"
                    + $"\n    it should be:       {tabulatedErrors[i]}");
            }

            // MC not-a-knot
            f = new CubicInterpolation(x, y, Spline, true, NotAKnot, NullReal, NotAKnot, NullReal);
            result = Math.Sqrt(integral.Integrate(MakeErrorFunction(f), -1.7, 1.9));
            result /= scaleFactor;
            if (Math.Abs(result - tabulatedMCErrors[i]) > toleranceOnTabMCErr[i])
            {
                Assert.Fail(
                    "MC Not-a-knot spline interpolation "
                    + $"\n    sample points:      {n}"
                    + $"\n    norm of difference: {result}"
                    + $"\n    it should be:       {tabulatedMCErrors[i]}");
            }
        }
    }

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

    // Adapted: enableExtrapolation (line 1241), which changes QuantLib's interpolation, returns a modified copy here
    // (playbook §2), which f is rebound to.
    [Fact]
    public void TestForwardFlat()
    {
        // Testing forward-flat interpolation...

        Real[] x = [0.0, 1.0, 2.0, 3.0, 4.0];
        Real[] y = [5.0, 4.0, 3.0, 2.0, 1.0];

        Interpolation f = new ForwardFlatInterpolation(x, y);

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
            Real expected = y[i];
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
            sum += (x[i] - x[i - 1]) * y[i - 1];
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
            sum += (x[i + 1] - x[i]) * y[i] / 2;
            Real calculated = f.Primitive(p);
            Real expected = sum;
            sum += (x[i + 1] - x[i]) * y[i] / 2;
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail(
                    $"failed to calculate primitive at {p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }
    }

    [Fact]
    public void TestMixedLinearCubicMatchDerivatives()
    {
        Size n = 6;
        Size k = 2;

        Real[] x, y;
        x = XRange(-2.0, 2.0, n);
        y = Parabolic(x);

        var f = new MixedLinearCubicInterpolation(
            x,
            y,
            k,
            MixedInterpolationBehavior.SplitRanges,
            Spline,
            false,
            FirstDerivative,
            null,
            SecondDerivative,
            0.0);

        Real tolerance = 1.0e-12;
        Real eps = tolerance / 10;

        Real leftSide = f.Derivative(x[k] - eps);
        Real rightSide = f.Derivative(x[k] + eps);
        if (Math.Abs(leftSide - rightSide) > tolerance)
        {
            Assert.Fail(
                "derivatives at the switch point do not match"
                + $"\n    left side:  {leftSide:F6}\n    right side: {rightSide:F6}\n    error:      {Math.Abs(leftSide - rightSide):E}");
        }
    }

    [Fact]
    public void TestMixedLinearCubicSwitchPoint()
    {
        // Testing switch point bounds of mixed linear/cubic interpolation...

        Size n = 5;
        Real[] x = XRange(-2.0, 2.0, n);
        Real[] y = Parabolic(x);

        // a switch index equal to the number of points puts the switch
        // iterator one past the end of the x range, which used to be
        // dereferenced; it must be rejected instead
        ArgumentException e = Assert.Throws<ArgumentException>(() => new MixedLinearCubicNaturalSpline(x, y, n));
        Assert.Contains("n is too large", e.Message, StringComparison.Ordinal);

        // the largest valid switch index still reproduces the knots
        var f = new MixedLinearCubicNaturalSpline(x, y, n - 1);
        for (Size i = 0; i < n; ++i)
        {
            Real interpolated = f.Evaluate(x[i]);
            if (Math.Abs(interpolated - y[i]) > 1e-12)
            {
                Assert.Fail(
                    $"failed to reproduce knot {i}\n    expected:     {y[i]:E}\n    interpolated: {interpolated:E}");
            }
        }
    }

    [Fact]
    public void TestSabrInterpolation()
    {
        // Testing Sabr interpolation...

        // Test SABR function against input volatilities
        Real tolerance = 1.0e-12;
        Real[] strikes =
        [
            0.03, 0.032, 0.034, 0.036, 0.038, 0.04, 0.042, 0.044, 0.046, 0.048, 0.05, 0.052, 0.054, 0.056, 0.058, 0.06,
            0.062, 0.064, 0.066, 0.068, 0.07, 0.072, 0.074, 0.076, 0.078, 0.08, 0.082, 0.084, 0.086, 0.088, 0.09,
        ];
        Real[] volatilities =
        [
            1.16725837321531, 1.15226075991385, 1.13829711098834, 1.12524190877505, 1.11299079244474,
            1.10145609357162, 1.09056348513411, 1.08024942745106, 1.07045919457758, 1.06114533019077,
            1.05226642581503, 1.04378614411707, 1.03567243073732, 1.0278968727451, 1.02043417226345,
            1.01326171139321, 1.00635919013311, 0.999708323124949, 0.993292584155381, 0.987096989695393,
            0.98110791455717, 0.975312934134512, 0.969700688771689, 0.964260766651027, 0.958983602256592,
            0.953860388001395, 0.948882997029509, 0.944043915545469, 0.939336183299237, 0.934753341079515,
            0.930289384251337,
        ];

        double expiry = 1.0;
        Real forward = 0.039;

        // input SABR coefficients (corresponding to the vols above)
        Real initialAlpha = 0.3;
        Real initialBeta = 0.6;
        Real initialNu = 0.02;
        Real initialRho = 0.01;

        // calculate SABR vols and compare with input vols
        for (Size i = 0; i < strikes.Length; i++)
        {
            Real calculatedVol = SabrFunctions.SabrVolatility(
                strikes[i], forward, expiry, initialAlpha, initialBeta, initialNu, initialRho);
            if (Math.Abs(volatilities[i] - calculatedVol) > tolerance)
            {
                Assert.Fail(
                    $"failed to calculate Sabr function at strike {strikes[i]}\n    expected:   {volatilities[i]}"
                    + $"\n    calculated: {calculatedVol}\n    error:      {Math.Abs(calculatedVol - volatilities[i])}");
            }
        }

        // Test SABR calibration against input parameters
        Real alphaGuess = Math.Sqrt(0.2);
        Real betaGuess = 0.5;
        Real nuGuess = Math.Sqrt(0.4);
        Real rhoGuess = 0.0;

        bool[] vegaWeighted = [true, false];
        bool[] isAlphaFixed = [true, false];
        bool[] isBetaFixed = [true, false];
        bool[] isNuFixed = [true, false];
        bool[] isRhoFixed = [true, false];

        Real calibrationTolerance = 5.0e-8;

        // initialize optimization methods
        OptimizationMethod[] methods = [new Simplex(0.01), new LevenbergMarquardt(1e-8, 1e-8, 1e-8)];

        // Initialize end criteria
        var endCriteria = new EndCriteria(100000, 100, 1e-8, 1e-8, 1e-8);

        // Test looping over all possibilities
        foreach (OptimizationMethod method in methods)
        {
            foreach (bool i in vegaWeighted)
            {
                foreach (bool k_a in isAlphaFixed)
                {
                    foreach (bool k_b in isBetaFixed)
                    {
                        foreach (bool k_n in isNuFixed)
                        {
                            foreach (bool k_r in isRhoFixed)
                            {
                                // to meet the tough calibration tolerance we need to lower the default
                                // error threshold for accepting a calibration (to be more specific,
                                // some of the new test cases arising from fixing a subset of the
                                // model's parameters do not calibrate with the desired error using the
                                // initial guess (i.e. optimization runs into a local minimum) - then a
                                // series of random start values for optimization is chosen until our
                                // tight custom error threshold is satisfied.
                                var sabrInterpolation = new SABRInterpolation(
                                    strikes,
                                    volatilities,
                                    expiry,
                                    forward,
                                    k_a ? initialAlpha : alphaGuess,
                                    k_b ? initialBeta : betaGuess,
                                    k_n ? initialNu : nuGuess,
                                    k_r ? initialRho : rhoGuess,
                                    k_a,
                                    k_b,
                                    k_n,
                                    k_r,
                                    i,
                                    endCriteria,
                                    method,
                                    1E-10);

                                // Recover SABR calibration parameters
                                bool failed = false;
                                Real calibratedAlpha = sabrInterpolation.Alpha;
                                Real calibratedBeta = sabrInterpolation.Beta;
                                Real calibratedNu = sabrInterpolation.Nu;
                                Real calibratedRho = sabrInterpolation.Rho;
                                Real error;
                                var messages = new List<string>();

                                // compare results: alpha
                                error = Math.Abs(initialAlpha - calibratedAlpha);
                                if (error > calibrationTolerance)
                                {
                                    messages.Add(
                                        $"failed to calibrate alpha Sabr parameter: expected {initialAlpha}, calibrated {calibratedAlpha}, error {error}");
                                    failed = true;
                                }

                                // Beta
                                error = Math.Abs(initialBeta - calibratedBeta);
                                if (error > calibrationTolerance)
                                {
                                    messages.Add(
                                        $"failed to calibrate beta Sabr parameter: expected {initialBeta}, calibrated {calibratedBeta}, error {error}");
                                    failed = true;
                                }

                                // Nu
                                error = Math.Abs(initialNu - calibratedNu);
                                if (error > calibrationTolerance)
                                {
                                    messages.Add(
                                        $"failed to calibrate nu Sabr parameter: expected {initialNu}, calibrated {calibratedNu}, error {error}");
                                    failed = true;
                                }

                                // Rho
                                error = Math.Abs(initialRho - calibratedRho);
                                if (error > calibrationTolerance)
                                {
                                    messages.Add(
                                        $"failed to calibrate rho Sabr parameter: expected {initialRho}, calibrated {calibratedRho}, error {error}");
                                    failed = true;
                                }

                                if (failed)
                                {
                                    Assert.Fail(
                                        string.Join("\n", messages)
                                        + $"\nSabr calibration failure: isAlphaFixed {k_a}, isBetaFixed {k_b}, isNuFixed {k_n}, isRhoFixed {k_r}, vegaWeighted {i}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestSabrGuess()
    {
        // Testing Sabr guess...

        // table 3 in Le Floc'h and Kennedy.
        // They only seems to use it with lognormal volatility
        // and no shift; we extend the test here.
        (double Expiry, Real Forward, Real Alpha, Real Beta, Real Nu, Real Rho)[] cases =
        [
            (0.058, 2016, 0.271, 1.0, 1.010, -0.345),
            (0.153, 2016, 0.256, 1.0, 0.933, -0.321),
            (0.230, 2016, 0.256, 1.0, 0.820, -0.346),
            (0.479, 2016, 0.255, 1.0, 0.629, -0.370),
            (0.729, 2016, 0.257, 1.0, 0.528, -0.403),
            (1.227, 2016, 0.260, 1.0, 0.448, -0.429),
            (1.726, 2016, 0.261, 1.0, 0.392, -0.440),
            (2.244, 2016, 0.262, 1.0, 0.355, -0.445),
            (2.742, 2016, 0.262, 1.0, 0.329, -0.445),
            (3.241, 2016, 0.262, 1.0, 0.310, -0.447),
            (4.239, 2016, 0.263, 1.0, 0.284, -0.452),
        ];

        foreach (var c in cases)
        {
            foreach (VolatilityType type in new[] { VolatilityType.ShiftedLognormal, VolatilityType.Normal })
            {
                foreach (Real shift in new[] { 0.0, 0.01, 0.1 })
                {
                    // strikes at forward and plus or minus 5%
                    Real k_m = c.Forward * 0.95, k_0 = c.Forward, k_p = c.Forward * 1.05;
                    Real vol_m = SabrFunctions.ShiftedSabrVolatility(
                        k_m, c.Forward, c.Expiry, c.Alpha, c.Beta, c.Nu, c.Rho, shift, type);
                    Real vol_0 = SabrFunctions.ShiftedSabrVolatility(
                        k_0, c.Forward, c.Expiry, c.Alpha, c.Beta, c.Nu, c.Rho, shift, type);
                    Real vol_p = SabrFunctions.ShiftedSabrVolatility(
                        k_p, c.Forward, c.Expiry, c.Alpha, c.Beta, c.Nu, c.Rho, shift, type);

                    // try to invert smile and retrieve parameters
                    var guess = SabrFunctions.SabrGuess(
                        k_m, vol_m, k_0, vol_0, k_p, vol_p, c.Forward, c.Expiry, c.Beta, shift, type);
                    Real alpha = guess[0], beta = guess[1], nu = guess[2], rho = guess[3];

                    Assert.True(
                        Math.Abs(alpha - c.Alpha) <= 0.0001,
                        $"alpha = {alpha}, expected = {c.Alpha}, error = {alpha - c.Alpha}");
                    Assert.Equal(c.Beta, beta);
                    Assert.True(
                        Math.Abs(nu - c.Nu) <= 0.01, $"nu = {nu}, expected = {c.Nu}, error = {nu - c.Nu}");
                    Assert.True(
                        Math.Abs(rho - c.Rho) <= 0.005, $"rho = {rho}, expected = {c.Rho}, error = {rho - c.Rho}");
                }
            }
        }
    }

    // Adapted: enableExtrapolation (line 1678), which changes QuantLib's interpolation, returns a modified copy here
    // (playbook §2), which f is rebound to. QuantLib's __FAST_MATH__ tolerance isn't taken: nanoql's arithmetic is
    // strict.
    [Fact]
    public void TestKernelInterpolation()
    {
        // Testing kernel 1D interpolation...

        Real[] deltaGrid = [0.10, 0.25, 0.50, 0.75, 0.90];

        Real[][] yd =
        [
            [11.275, 11.125, 11.250, 11.825, 12.625],
            [16.025, 13.450, 11.350, 10.150, 10.075],
            [10.300, 9.6375, 9.2000, 9.1125, 9.4000],
        ];
        Real[] lambdaVec = [0.05, 0.50, 0.75, 1.65, 2.55];

        const Real tolerance = 2.0e-5;

        Real expectedVal;
        Real calcVal;
        var errors = new List<string>();

        // Check that y-values at knots are exactly the feeded y-values,
        // irrespective of kernel parameters
        foreach (Real i in lambdaVec)
        {
            var myKernel = new GaussianKernel(0, i);

            foreach (Real[] currY in yd)
            {
                var f = new KernelInterpolation(deltaGrid, currY, myKernel.Evaluate);

                for (int dIt = 0; dIt < deltaGrid.Length; ++dIt)
                {
                    expectedVal = currY[dIt];
                    calcVal = f.Evaluate(deltaGrid[dIt]);

                    if (Math.Abs(expectedVal - calcVal) > tolerance)
                    {
                        errors.Add(
                            $"Kernel interpolation failed at x = {deltaGrid[dIt]}"
                            + $"\n    interpolated value: {calcVal:E}"
                            + $"\n    expected value:     {expectedVal:E}"
                            + $"\n    error:              {Math.Abs(expectedVal - calcVal):E}");
                    }
                }
            }
        }

        Real[] testDeltaGrid = [0.121, 0.279, 0.678, 0.790, 0.980];

        // Gaussian Kernel values for testDeltaGrid with a standard
        // deviation of 2.05 (the value is arbitrary.)  Source: parrallel
        // implementation in R, no literature sources found
        Real[][] ytd =
        [
            [11.23847, 11.12003, 11.58932, 11.99168, 13.29650],
            [15.55922, 13.11088, 10.41615, 10.05153, 10.50741],
            [10.17473, 9.557842, 9.09339, 9.149687, 9.779971],
        ];

        var kernel = new GaussianKernel(0, 2.05);

        for (int j = 0; j < ytd.Length; ++j)
        {
            Real[] currY = yd[j];
            Real[] currTY = ytd[j];

            // Build interpolation according to original grid + y-values
            var f = new KernelInterpolation(deltaGrid, currY, kernel.Evaluate);

            // test values at test Grid
            for (int dIt = 0; dIt < testDeltaGrid.Length; ++dIt)
            {
                expectedVal = currTY[dIt];
                f = f.EnableExtrapolation(); // allow extrapolation

                calcVal = f.Evaluate(testDeltaGrid[dIt]);
                if (Math.Abs(expectedVal - calcVal) > tolerance)
                {
                    errors.Add(
                        $"Kernel interpolation failed at x = {deltaGrid[dIt]}"
                        + $"\n    interpolated value: {calcVal:E}"
                        + $"\n    expected value:     {expectedVal:E}"
                        + $"\n    error:              {Math.Abs(expectedVal - calcVal):E}");
                }
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    // Adapted: the update test (lines 1792-1812) changes the vectors QuantLib's interpolation reads and calls update();
    // nanoql's interpolation copies its inputs, so the updated interpolation is a new one on the changed vectors.
    [Fact]
    public void TestKernelInterpolation2D()
    {
        // No test values known from the literature.
        // Testing for consistency of input output data
        // at the nodes

        // Testing kernel 2D interpolation...

        Real mean = 0.0, var = 0.18;
        var myKernel = new GaussianKernel(mean, var);

        var xVec = new Real[10];
        xVec[0] = 0.10; xVec[1] = 0.20; xVec[2] = 0.30; xVec[3] = 0.40;
        xVec[4] = 0.50; xVec[5] = 0.60; xVec[6] = 0.70; xVec[7] = 0.80;
        xVec[8] = 0.90; xVec[9] = 1.00;

        var yVec = new Real[3];
        yVec[0] = 1.0; yVec[1] = 2.0; yVec[2] = 3.5;

        var m = new Matrix(xVec.Length, yVec.Length);

        m[0, 0] = 0.25; m[1, 0] = 0.24; m[2, 0] = 0.23; m[3, 0] = 0.20; m[4, 0] = 0.19;
        m[5, 0] = 0.20; m[6, 0] = 0.21; m[7, 0] = 0.22; m[8, 0] = 0.26; m[9, 0] = 0.29;

        m[0, 1] = 0.27; m[1, 1] = 0.26; m[2, 1] = 0.25; m[3, 1] = 0.22; m[4, 1] = 0.21;
        m[5, 1] = 0.22; m[6, 1] = 0.23; m[7, 1] = 0.24; m[8, 1] = 0.28; m[9, 1] = 0.31;

        m[0, 2] = 0.21; m[1, 2] = 0.22; m[2, 2] = 0.27; m[3, 2] = 0.29; m[4, 2] = 0.24;
        m[5, 2] = 0.28; m[6, 2] = 0.25; m[7, 2] = 0.22; m[8, 2] = 0.29; m[9, 2] = 0.30;

        var kernel2D = new KernelInterpolation2D(xVec, yVec, m, myKernel.Evaluate);

        Real calcVal, expectedVal;
        const Real tolerance = 1.0e-10;
        var errors = new List<string>();

        for (int i = 0; i < m.Rows; ++i)
        {
            for (int j = 0; j < m.Columns; ++j)
            {
                calcVal = kernel2D.Evaluate(xVec[i], yVec[j]);
                expectedVal = m[i, j];

                if (Math.Abs(expectedVal - calcVal) > tolerance)
                {
                    errors.Add(
                        $"2D Kernel interpolation failed at x = {xVec[i]}, y = {yVec[j]}"
                        + $"\n    interpolated value: {calcVal}"
                        + $"\n    expected value:     {expectedVal}"
                        + $"\n    error:              {Math.Abs(expectedVal - calcVal)}");
                }
            }
        }

        // alternative data set
        var xVec1 = new Real[4];
        xVec1[0] = 80.0; xVec1[1] = 90.0; xVec1[2] = 100.0; xVec1[3] = 110.0;

        var yVec1 = new Real[8];
        yVec1[0] = 0.5; yVec1[1] = 0.7; yVec1[2] = 1.0; yVec1[3] = 2.0;
        yVec1[4] = 3.5; yVec1[5] = 4.5; yVec1[6] = 5.5; yVec1[7] = 6.5;

        var m1 = new Matrix(xVec1.Length, yVec1.Length);
        m1[0, 0] = 10.25; m1[1, 0] = 12.24; m1[2, 0] = 14.23; m1[3, 0] = 17.20;
        m1[0, 1] = 12.25; m1[1, 1] = 15.24; m1[2, 1] = 16.23; m1[3, 1] = 16.20;
        m1[0, 2] = 12.25; m1[1, 2] = 13.24; m1[2, 2] = 13.23; m1[3, 2] = 17.20;
        m1[0, 3] = 13.25; m1[1, 3] = 15.24; m1[2, 3] = 12.23; m1[3, 3] = 19.20;
        m1[0, 4] = 14.25; m1[1, 4] = 16.24; m1[2, 4] = 13.23; m1[3, 4] = 12.20;
        m1[0, 5] = 15.25; m1[1, 5] = 17.24; m1[2, 5] = 14.23; m1[3, 5] = 12.20;
        m1[0, 6] = 16.25; m1[1, 6] = 13.24; m1[2, 6] = 15.23; m1[3, 6] = 10.20;
        m1[0, 7] = 14.25; m1[1, 7] = 14.24; m1[2, 7] = 16.23; m1[3, 7] = 19.20;

        // test with function pointer
        var kernel2DEp = new KernelInterpolation2D(xVec1, yVec1, m1, EpanechnikovKernel);

        for (int i = 0; i < m1.Rows; ++i)
        {
            for (int j = 0; j < m1.Columns; ++j)
            {
                calcVal = kernel2DEp.Evaluate(xVec1[i], yVec1[j]);
                expectedVal = m1[i, j];

                if (Math.Abs(expectedVal - calcVal) > tolerance)
                {
                    errors.Add(
                        $"2D Epanechnkikov Kernel interpolation failed at x = {xVec1[i]}, y = {yVec1[j]}"
                        + $"\n    interpolated value: {calcVal}"
                        + $"\n    expected value:     {expectedVal}"
                        + $"\n    error:              {Math.Abs(expectedVal - calcVal)}");
                }
            }
        }

        // test updating mechanism by changing initial variables
        xVec1[0] = 60.0; xVec1[1] = 95.0; xVec1[2] = 105.0; xVec1[3] = 135.0;

        yVec1[0] = 12.5; yVec1[1] = 13.7; yVec1[2] = 15.0; yVec1[3] = 19.0;
        yVec1[4] = 26.5; yVec1[5] = 27.5; yVec1[6] = 29.2; yVec1[7] = 36.5;

        kernel2DEp = new KernelInterpolation2D(xVec1, yVec1, m1, EpanechnikovKernel);

        for (int i = 0; i < m1.Rows; ++i)
        {
            for (int j = 0; j < m1.Columns; ++j)
            {
                calcVal = kernel2DEp.Evaluate(xVec1[i], yVec1[j]);
                expectedVal = m1[i, j];

                if (Math.Abs(expectedVal - calcVal) > tolerance)
                {
                    errors.Add(
                        $"2D Epanechnkikov Kernel updated interpolation failed at x = {xVec1[i]}, y = {yVec1[j]}"
                        + $"\n    interpolated value: {calcVal}"
                        + $"\n    expected value:     {expectedVal}"
                        + $"\n    error:              {Math.Abs(expectedVal - calcVal)}");
                }
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void TestBicubicDerivatives()
    {
        // Testing bicubic spline derivatives...

        var x = new Real[100];
        var y = new Real[100];
        for (Size i = 0; i < 100; ++i)
        {
            x[i] = y[i] = i / 20.0;
        }

        var f = new Matrix(100, 100);
        for (Size i = 0; i < 100; ++i)
        {
            for (Size j = 0; j < 100; ++j)
            {
                f[i, j] = (y[i] / 10 * Math.Sin(x[j])) + Math.Cos(y[i]);
            }
        }

        const Real tol = 0.005;
        var spline = new BicubicSpline(x, y, f);

        var failures = new List<string>();
        for (Size i = 5; i < 95; i += 10)
        {
            for (Size j = 5; j < 95; j += 10)
            {
                Real f_x = spline.DerivativeX(x[j], y[i]);
                Real f_xx = spline.SecondDerivativeX(x[j], y[i]);
                Real f_y = spline.DerivativeY(x[j], y[i]);
                Real f_yy = spline.SecondDerivativeY(x[j], y[i]);
                Real f_xy = spline.DerivativeXY(x[j], y[i]);

                if (Math.Abs(f_x - (y[i] / 10 * Math.Cos(x[j]))) > tol)
                {
                    failures.Add("Failed to reproduce f_x");
                }

                if (Math.Abs(f_xx + (y[i] / 10 * Math.Sin(x[j]))) > tol)
                {
                    failures.Add("Failed to reproduce f_xx");
                }

                if (Math.Abs(f_y - ((Math.Sin(x[j]) / 10) - Math.Sin(y[i]))) > tol)
                {
                    failures.Add("Failed to reproduce f_y");
                }

                if (Math.Abs(f_yy + Math.Cos(y[i])) > tol)
                {
                    failures.Add("Failed to reproduce f_yy");
                }

                if (Math.Abs(f_xy - (Math.Cos(x[j]) / 10)) > tol)
                {
                    failures.Add("Failed to reproduce f_xy");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void TestUnknownRichardsonExtrapolation()
    {
        // Testing Richardson extrapolation with unknown order of convergence...

        const Real stepSize = 0.01;

        (Real Exponent, Real Factor)[] testCases =
        [
            (1.0, 1.0), (1.0, -1.0),
            (2.0, 0.25), (2.0, -1.0),
            (3.0, 2.0), (3.0, -0.5),
            (4.0, 1.0), (4.0, 0.5),
        ];

        foreach ((Real exponent, Real factor) in testCases)
        {
            var extrap = new RichardsonExtrapolation(h => GF(exponent, factor, h), stepSize);

            Real calculated = extrap.Evaluate(4.0, 2.0);
            Real diff = Math.Abs(M_PI - calculated);

            Real tol = Math.Pow(stepSize, exponent + 1);

            Assert.False(
                diff > tol, "failed to reproduce Richardson extrapolation  with unknown order of convergence");
        }

        Real highOrder = new RichardsonExtrapolation(h => GF(14.0, 1.0, h), 0.5).Evaluate(4.0, 2.0);
        Assert.False(
            Math.Abs(highOrder - M_PI) > 1e-12,
            "failed to reproduce Richardson extrapolation  with unknown order of convergence");

        bool thrown = false;
        try
        {
            new RichardsonExtrapolation(h => GF(16.0, 1.0, h), 0.5).Evaluate(4.0, 2.0);
        }
        catch (Exception)
        {
            thrown = true;
        }

        Assert.True(
            thrown, "Richardson extrapolation with order of convergence above 15 should throw exception");

        Real limCosValue = new RichardsonExtrapolation(h => -Math.Cos(h), 0.01).Evaluate(4.0, 2.0);
        Assert.False(
            Math.Abs(limCosValue + 1.0) > 1e-6,
            "failed to reproduce Richardson extrapolation  with unknown order of convergence");
    }

    [Fact]
    public void TestRichardsonExtrapolation()
    {
        // Testing Richardson extrapolation...

        /* example taken from
         * http://www.ipvs.uni-stuttgart.de/abteilungen/bv/lehre/
         *      lehrveranstaltungen/vorlesungen/WS0910/
         *      NSG_termine/dateien/Richardson.pdf
         */

        const Real stepSize = 0.1;
        const Real orderOfConvergence = 1.0;
        var extrap = new RichardsonExtrapolation(h => Math.Pow(1.0 + h, 1 / h), stepSize, orderOfConvergence);

        const Real tol = 0.00002;
        Real expected = 2.71285;

        const Real scalingFactor = 2.0;
        Real calculated = extrap.Evaluate(scalingFactor);

        Assert.False(Math.Abs(expected - calculated) > tol, "failed to reproduce Richardson extrapolation");

        calculated = extrap.Evaluate();
        Assert.False(Math.Abs(expected - calculated) > tol, "failed to reproduce Richardson extrapolation");

        expected = 2.721376;
        const Real scalingFactor2 = 4.0;
        calculated = extrap.Evaluate(scalingFactor2, scalingFactor);

        Assert.False(Math.Abs(expected - calculated) > tol, "failed to reproduce Richardson extrapolation");
    }

    [Fact]
    public void TestSabrSingleCases()
    {
        // Testing Sabr calibration single cases...

        // case #1
        // this fails with an exception thrown in 1.4, fixed in 1.5
        Real[] strikes = [0.01, 0.01125, 0.0125, 0.01375, 0.0150];
        Real[] vols = [0.1667, 0.2020, 0.2785, 0.3279, 0.3727];

        double tte = 0.3833;
        Real forward = 0.011025;

        var s0 = new SABRInterpolation(
            strikes, vols, tte, forward, null, 0.25, null, null, false, true, false, false);

        if (s0.MaxError > 0.01 || s0.RmsError > 0.01)
        {
            Assert.Fail(
                $"Sabr case #1 failed with max error ({s0.MaxError}) and rms error ({s0.RmsError}), both should be < 0.01");
        }
    }

    [Fact]
    public void TestFlochKennedySabrIsSmoothAroundATM()
    {
        // Testing that Andersen SABR formula is smooth close to the ATM level...

        const Real f0 = 1.1;
        const Real alpha = 0.35;
        const Real nu = 1.1;
        const Real rho = 0.25;
        const Real beta = 0.3;
        const Real strike = f0;
        const double t = 2.1;

        Real vol = SabrFunctions.SabrFlochKennedyVolatility(strike, f0, t, alpha, beta, nu, rho);

        const Real expected = 0.3963883944;
        const Real tol = 1e-8;
        Real diff = Math.Abs(expected - vol);
        if (diff > tol)
        {
            Assert.Fail($"failed to get ATM value :\n    expected:   {expected}\n    calculated: {vol}\n    diff:      {diff}");
        }

        Real k = 0.996 * strike;
        Real v = SabrFunctions.SabrFlochKennedyVolatility(k, f0, t, alpha, beta, nu, rho);

        for (; k < 1.004 * strike; k += 0.0001 * strike)
        {
            Real vt = SabrFunctions.SabrFlochKennedyVolatility(k, f0, t, alpha, beta, nu, rho);

            Real d = Math.Abs(v - vt);

            if (d > 1e-5)
            {
                Assert.Fail(
                    $"Sabr vol spike around ATM :\n    volatility at {k - (0.0001 * strike)} is {v}\n    volatility at {k}"
                    + $" is {vt}\n    difference: {d}\n    tolerance : {1e-5}");
            }

            v = vt;
        }
    }

    [Fact]
    public void TestLeFlochKennedySabrExample()
    {
        // Testing Le Floc'h Kennedy SABR Example...

        // Example is taken from F. Le Floc'h, G. Kennedy:
        //  Explicit SABR Calibration through Simple Expansions.
        //  https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2467231
        const Real f0 = 1.0;
        const Real alpha = 0.35;
        const Real nu = 1.0;
        const Real rho = 0.25;
        const Real beta = 0.25;
        Real[] strikes = [1.0, 1.5, 0.5];
        const double t = 2.0;

        Real[] expected = [0.408702473958, 0.428489933046, 0.585701651161];

        for (Size i = 0; i < strikes.Length; ++i)
        {
            Real strike = strikes[i];
            Real vol = SabrFunctions.SabrFlochKennedyVolatility(strike, f0, t, alpha, beta, nu, rho);

            const Real tol = 1e-8;
            Real diff = Math.Abs(expected[i] - vol);

            if (diff > tol)
            {
                Assert.Fail(
                    $"failed to reproduce reference examples :\n    expected:   {expected[i]}\n    calculated: {vol}"
                    + $"\n    diff:       {diff}");
            }
        }
    }

    [Fact]
    public void TestLagrangeInterpolation()
    {
        // Testing Lagrange interpolation...
        double[] x = [-1.0, -0.5, -0.25, 0.1, 0.4, 0.75, 0.96];
        double[] y = new double[x.Length];
        for (int i = 0; i < x.Length; ++i)
        {
            y[i] = LagrangeTestFct(x[i]);
        }

        var interpl = new LagrangeInterpolation(x, y);

        // reference results are taken from R package pracma
        Real[] references =
        [
            -0.5000000000000000, -0.5392414024347419, -0.5591485962711904,
            -0.5629199661387594, -0.5534414777017116, -0.5333043347921566,
            -0.5048221831582063, -0.4700478608272949, -0.4307896950846587,
            -0.3886273460669714, -0.3449271969711449, -0.3008572908782903,
            -0.2574018141928359, -0.2153751266968088, -0.1754353382192734,
            -0.1380974319209344, -0.1037459341938971, -0.0726471311765894,
            -0.0449608318838433, -0.0207516779521373, 0.0000000000000000,
            0.0173877793964286, 0.0315691961126723, 0.0427562482700356,
            0.0512063534145595, 0.0572137590808174, 0.0611014067405497,
            0.0632132491361394, 0.0639070209989264, 0.0635474631523613,
            0.0625000000000000, 0.0611248703983366, 0.0597717119144768,
            0.0587745984686508, 0.0584475313615655, 0.0590803836865967,
            0.0609352981268212, 0.0642435381368876, 0.0692027925097279,
            0.0759749333281079, 0.0846842273010179, 0.0954160004849021,
            0.1082157563897290, 0.1230887474699003, 0.1400000000000001,
            0.1588747923353829, 0.1795995865576031, 0.2020234135046815,
            0.2259597111862140, 0.2511886165833182, 0.2774597108334206,
            0.3044952177998833, 0.3319936560264689, 0.3596339440766487,
            0.3870799592577457, 0.4139855497299214, 0.4400000000000001,
            0.4647739498001331, 0.4879657663513030, 0.5092483700116673,
            0.5283165133097421, 0.5448945133624253, 0.5587444376778583,
            0.5696747433431296, 0.5775493695968156, 0.5822972837863635,
            0.5839224807103117, 0.5825144353453510, 0.5782590089582251,
            0.5714498086024714, 0.5625000000000000, 0.5519545738075141,
            0.5405030652677689, 0.5289927272456703, 0.5184421566492137,
            0.5100553742352614, 0.5052363578001620, 0.5056040287552059,
            0.5130076920869246,
        ];

        const double tol = 50 * QL_EPSILON;
        for (int i = 0; i < 79; ++i)
        {
            Real xx = -1.0 + (i * 0.025);
            Real calculated = interpl.Evaluate(xx);
            Assert.False(
                double.IsNaN(calculated) || Math.Abs(references[i] - calculated) > tol,
                $"failed to reproduce the Lagrange interpolation\n    x         : {xx}\n    calculated: {calculated}"
                + $"\n    expected  : {references[i]}");
        }
    }

    [Fact]
    public void TestLagrangeInterpolationAtSupportPoint()
    {
        // Testing Lagrange interpolation at supporting points...
        const int n = 5;
        double[] x = new double[n], y = new double[n];
        for (int i = 0; i < n; ++i)
        {
            x[i] = i / (Real)n;
            y[i] = 1.0 / (1.0 - x[i]);
        }

        var interpl = new LagrangeInterpolation(x, y);

        const Real relTol = 5e-12;

        for (int i = 1; i < n - 1; ++i)
        {
            for (Real z = x[i] - (100 * QL_EPSILON); z < x[i] + (100 * QL_EPSILON); z += 2 * QL_EPSILON)
            {
                Real expected = 1.0 / (1.0 - x[i]);
                Real calculated = interpl.Evaluate(z);

                Assert.False(
                    double.IsNaN(calculated) || Math.Abs(expected - calculated) > relTol,
                    $"failed to reproduce the Lagrange interplation\n    x         : {z}\n    calculated: {calculated}"
                    + $"\n    expected  : {expected}");
            }
        }
    }

    [Fact]
    public void TestLagrangeInterpolationDerivative()
    {
        // Testing Lagrange interpolation derivatives...
        double[] x = [-1.0, -0.3, 0.1, 0.3, 0.9];
        double[] y = [2.0, 3.0, 6.0, 3.0, -1.0];

        var interpl = new LagrangeInterpolation(x, y);

        Real eps = Math.Sqrt(QL_EPSILON);
        for (Real xx = -1.0; xx <= 0.9; xx += 0.01)
        {
            Real calculated = interpl.Derivative(xx, true);
            Real expected = (interpl.Evaluate(xx + eps, true) - interpl.Evaluate(xx - eps, true)) / (2 * eps);

            Assert.False(
                double.IsNaN(calculated) || Math.Abs(expected - calculated) > 25 * eps,
                $"failed to reproduce the Lagrange interplation derivative\n    x         : {xx}"
                + $"\n    calculated: {calculated}\n    expected  : {expected}");
        }
    }

    [Fact]
    public void TestLagrangeInterpolationOnChebyshevPoints()
    {
        // Testing Lagrange interpolation on Chebyshev nodes...

        // Test example taken from
        // J.P. Berrut, L.N. Trefethen, Barycentric Lagrange Interpolation
        // https://people.maths.ox.ac.uk/trefethen/barycentric.pdf
        const int n = 50;
        double[] x = new double[n + 1], y = new double[n + 1];
        for (int i = 0; i <= n; ++i)
        {
            // Chebyshev nodes
            x[i] = Math.Cos(((2 * i) + 1) * Math.PI / ((2 * n) + 2));
            y[i] = Math.Exp(x[i]) / Math.Cos(x[i]);
        }

        var interpl = new LagrangeInterpolation(x, y);

        const Real tol = 1e-13;
        const Real tolDeriv = 1e-11;

        for (Real xx = -1.0; xx <= 1.0; xx += 0.03)
        {
            Real calculated = interpl.Evaluate(xx, true);
            Real expected = Math.Exp(xx) / Math.Cos(xx);

            Real diff = Math.Abs(expected - calculated);
            Assert.False(
                double.IsNaN(calculated) || diff > tol,
                $"failed to reproduce the Lagrange interpolation on Chebyshev nodes\n    x         : {xx}"
                + $"\n    calculated: {calculated}\n    expected  : {expected}\n    difference: {diff}");

            Real calculatedDeriv = interpl.Derivative(xx, true);
            Real expectedDeriv = Math.Exp(xx) * (Math.Cos(xx) + Math.Sin(xx)) / (Math.Cos(xx) * Math.Cos(xx));

            Real diffDeriv = Math.Abs(expectedDeriv - calculatedDeriv);
            Assert.False(
                double.IsNaN(calculated) || diffDeriv > tolDeriv,
                $"failed to reproduce the Lagrange interpolation derivative on Chebyshev nodes\n    x         : {xx}"
                + $"\n    calculated: {calculatedDeriv}\n    expected  : {expectedDeriv}\n    difference: {diffDeriv}");
        }
    }

    [Fact]
    public void TestBSplines()
    {
        // Testing B-Splines...

        // reference values have been generate with the R package splines2
        // https://cran.r-project.org/web/packages/splines2/splines2.pdf
        double[] knots = [-1.0, 0.5, 0.75, 1.2, 3.0, 4.0, 5.0];

        const Natural p = 2;
        var bspline = new BSpline(p, knots.Length - p - 2, knots);

        (Natural Idx, Real X, Real Expected)[] referenceValues =
        [
            (0, -0.95, 9.5238095238e-04),
            (0, -0.01, 0.37337142857),
            (0, 0.49, 0.84575238095),
            (0, 1.21, 0.0),
            (1, 1.49, 0.562987654321),
            (1, 1.59, 0.490888888889),
            (2, 1.99, 0.62429409171),
            (3, 1.19, 0.0),
            (3, 1.99, 0.12382936508),
            (3, 3.59, 0.765914285714),
        ];

        const Real tol = 1e-10;
        foreach ((Natural idx, Real x, Real expected) in referenceValues)
        {
            Real calculated = bspline.Evaluate(idx, x);

            if (double.IsNaN(calculated) || Math.Abs(calculated - expected) > tol)
            {
                Assert.Fail(
                    "failed to reproduce the B-Spline value"
                    + $"\n    i         : {idx}"
                    + $"\n    x         : {x}"
                    + $"\n    calculated: {calculated}"
                    + $"\n    expected  : {expected}"
                    + $"\n    difference: {Math.Abs(calculated - expected)}"
                    + $"\n    tolerance : {tol}");
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

    // Adapted: without its update smoke test (lines 3172-3184): an interpolation is immutable and has no update().
    [Fact]
    public void TestChebyshevInterpolation()
    {
        // Testing Chebyshev interpolation...
        (Func<Real, Real> Fct, string Name)[] fcts =
        [
            (Math.Sin, "sin"),
            (Math.Cos, "cos"),
            (x => Math.Exp(-x * x), "e^(-x*x)"),
        ];

        (Size Nodes, Real Tol)[] tests =
        [
            (11, 1e-5),
            (20, 1e-11),
        ];

        foreach (var t in tests)
        {
            foreach (var fct in fcts)
            {
                var interp = new ChebyshevInterpolation(t.Nodes, fct.Fct);

                for (Real x = -0.99; x < 1.0; x += 0.01)
                {
                    Real expected = fct.Fct(x);
                    Real calculated = interp.Evaluate(x);
                    Real diff = Math.Abs(expected - calculated);
                    Real tol = t.Tol;

                    Assert.False(
                        double.IsNaN(calculated) || Math.Abs(calculated - expected) > tol,
                        $"failed to reproduce the Chebyshev interpolation values\n    x         : {x}\n    fct       : "
                        + $"{fct.Name}\n    calculated: {calculated}\n    expected  : {expected}\n    difference: {diff}"
                        + $"\n    tolerance : {tol}");
                }
            }
        }
    }

    [Fact]
    public void TestChebyshevInterpolationOnNodes()
    {
        // Testing Chebyshev interpolation on and around nodes...
        const double tol = 10 * QL_EPSILON;
        Func<Real, Real> testFct = Math.Sin;

        const Size nrNodes = 7;
        double[] y = new double[nrNodes];

        foreach (ChebyshevInterpolationPointsType pointType in new[]
                 { ChebyshevInterpolationPointsType.FirstKind, ChebyshevInterpolationPointsType.SecondKind })
        {
            Vector nodes = ChebyshevInterpolation.Nodes(nrNodes, pointType);
            for (int i = 0; i < nrNodes; ++i)
            {
                y[i] = testFct(nodes[i]);
            }

            var interp = new ChebyshevInterpolation(y, pointType);
            for (int j = 0; j < nodes.Size; ++j)
            {
                Real node = nodes[j];

                // test on Chebyshev node
                Real expected = testFct(node);
                Real calculated = interp.Evaluate(node);
                Real diff = Math.Abs(expected - calculated);

                Assert.False(
                    diff > tol,
                    $"failed to reproduce the node values\n    node      : {node}\n    calculated: {calculated}"
                    + $"\n    expected  : {expected}\n    difference: {diff}\n    tolerance : {tol}");

                // check around Chebyshev node
                for (Integer i = -50; i < 50; ++i)
                {
                    Real x = node + (i * QL_EPSILON);
                    expected = testFct(x);
                    calculated = interp.Evaluate(x, true);
                    diff = Math.Abs(expected - calculated);

                    Assert.False(
                        diff > tol,
                        $"failed to reproduce values around nodes\n    node      : {node}\n    epsilon   : {x - node}"
                        + $"\n    calculated: {calculated}\n    expected  : {expected}\n    difference: {diff}"
                        + $"\n    tolerance : {tol}");
                }
            }
        }
    }

    // Adapted: QuantLib's updateY overwrites the values in place; WithY returns the interpolation of the new ones.
    [Fact]
    public void TestChebyshevInterpolationUpdateY()
    {
        // Testing Y update for Chebyshev interpolation...
        double[] y = [1, 4, 7, 4];
        var interp = new ChebyshevInterpolation(y);

        double[] yd = [6, 4, 5, 6];
        interp = interp.WithY(yd);

        const double tol = 10 * QL_EPSILON;

        for (int i = 0; i < y.Length; ++i)
        {
            Real expected = yd[i];
            Real calculated = interp.Evaluate(interp.Nodes()[i], true);
            Real diff = Math.Abs(calculated - expected);

            Assert.False(
                diff > tol,
                $"failed to reproduce updated node values\n    node      : {i}\n    expected  : {expected}"
                + $"\n    calculated: {calculated}\n    difference: {diff}\n    tolerance : {tol}");
        }
    }

    [Fact]
    public void TestFlatExtrapolation()
    {
        // Testing flat extrapolation of 1-D interpolation...
        Real[] x = [0.0, 1.0, 2.0, 3.0, 4.0];
        Real[] y = [5.0, 3.0, 4.0, 2.0, 1.0];
        Size n = x.Length;

        CubicInterpolation cubic = new CubicInterpolation(x, y, Spline, false, SecondDerivative, 0.0, SecondDerivative, 0.0)
            .EnableExtrapolation();

        var f = new FlatExtrapolator(cubic);

        Real tolerance = 1.0e-12;

        // extrapolation not allowed by default
        Assert.Throws<NanoQLException>(() => f.Evaluate(-1.0));

        f = f.EnableExtrapolation();

        // in-range: must match underlying cubic at knots
        for (Size i = 0; i < n; ++i)
        {
            Real calculated = f.Evaluate(x[i]);
            Real expected = y[i];
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    $"in-range mismatch at x={x[i]}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // in-range at midpoints: must match underlying cubic
        for (Size i = 0; i < n - 1; ++i)
        {
            Real mid = (x[i] + x[i + 1]) / 2.0;
            Real calculated = f.Evaluate(mid);
            Real expected = cubic.Evaluate(mid);
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    $"in-range mismatch at midpoint x={mid}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // below range: flat at y[0]
        foreach (Real p in (Real[])[-2.0, -1.0, -0.01])
        {
            Real calculated = f.Evaluate(p);
            Real expected = y[0];
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    $"flat extrapolation below range failed at {p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // above range: flat at y[N-1]
        foreach (Real p in (Real[])[4.01, 5.0, 10.0])
        {
            Real calculated = f.Evaluate(p);
            Real expected = y[n - 1];
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    $"flat extrapolation above range failed at {p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // derivative outside range is zero
        Real calc = f.Derivative(-1.0);
        if (Math.Abs(calc) > tolerance)
        {
            Assert.Fail($"derivative should be zero below range\n    calculated: {calc}");
        }

        calc = f.Derivative(5.0);
        if (Math.Abs(calc) > tolerance)
        {
            Assert.Fail($"derivative should be zero above range\n    calculated: {calc}");
        }

        // second derivative outside range is zero
        calc = f.SecondDerivative(-1.0);
        if (Math.Abs(calc) > tolerance)
        {
            Assert.Fail($"second derivative should be zero below range\n    calculated: {calc}");
        }

        calc = f.SecondDerivative(5.0);
        if (Math.Abs(calc) > tolerance)
        {
            Assert.Fail($"second derivative should be zero above range\n    calculated: {calc}");
        }

        // derivative inside range matches underlying
        foreach (Real p in (Real[])[0.5, 1.5, 2.5, 3.5])
        {
            Real calculated = f.Derivative(p);
            Real expected = cubic.Derivative(p);
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    $"in-range derivative mismatch at x={p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // second derivative inside range matches underlying
        foreach (Real p in (Real[])[0.5, 1.5, 2.5, 3.5])
        {
            Real calculated = f.SecondDerivative(p);
            Real expected = cubic.SecondDerivative(p);
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    $"in-range second derivative mismatch at x={p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // primitive below range
        {
            Real calculated = f.Primitive(-1.0);
            Real expected = cubic.Primitive(x[0], true) + (y[0] * (-1.0 - x[0]));
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    "primitive below range mismatch at x=-1.0"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // primitive above range
        {
            Real calculated = f.Primitive(5.0);
            Real expected = cubic.Primitive(x[n - 1], true) + (y[n - 1] * (5.0 - x[n - 1]));
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    "primitive above range mismatch at x=5.0"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // primitive inside range matches underlying
        foreach (Real p in (Real[])[0.5, 1.5, 2.5, 3.5])
        {
            Real calculated = f.Primitive(p);
            Real expected = cubic.Primitive(p);
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    $"in-range primitive mismatch at x={p}"
                    + $"\n    expected:   {expected:F6}\n    calculated: {calculated:F6}"
                    + $"\n    error:      {Math.Abs(calculated - expected):E}");
            }
        }

        // xValues and yValues accessors
        var xs = f.XValues();
        var ys = f.YValues();
        if (xs.Length != n)
        {
            Assert.Fail($"xValues size mismatch\n    expected: {n}\n    got:      {xs.Length}");
        }

        if (ys.Length != n)
        {
            Assert.Fail($"yValues size mismatch\n    expected: {n}\n    got:      {ys.Length}");
        }

        for (Size i = 0; i < n; ++i)
        {
            if (Math.Abs(xs[i] - x[i]) > tolerance)
            {
                Assert.Fail($"xValues mismatch at index {i}\n    expected: {x[i]}\n    got:      {xs[i]}");
            }

            if (Math.Abs(ys[i] - y[i]) > tolerance)
            {
                Assert.Fail($"yValues mismatch at index {i}\n    expected: {y[i]}\n    got:      {ys[i]}");
            }
        }

        // isInRange
        if (!f.IsInRange(2.0))
        {
            Assert.Fail("isInRange should return true for x=2.0");
        }

        if (f.IsInRange(-1.0))
        {
            Assert.Fail("isInRange should return false for x=-1.0");
        }
    }

    // QuantLib's errorFunction: the squared difference between the interpolation and exp(-x^2).
    private static Func<Real, Real> MakeErrorFunction(CubicInterpolation f) =>
        x =>
        {
            Real temp = f.Evaluate(x) - Math.Exp(-x * x);
            return temp * temp;
        };

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

    // interpolations.cpp's epanechnikovKernel (lines 199-206).
    private static Real EpanechnikovKernel(Real u)
    {
        if (Math.Abs(u) <= 1)
        {
            return (3.0 / 4.0) * (1 - (u * u));
        }
        else
        {
            return 0.0;
        }
    }

    // interpolations.cpp's GF (lines 216-227): π + factor·h^exponent + (factor·h)^(exponent + 1).
    private static Real GF(Real exponent, Real factor, Real h) =>
        M_PI + (factor * Math.Pow(h, exponent)) + Math.Pow(factor * h, exponent + 1);

    // The test suite's lagrangeTestFct: |x| + x/2 - x squared.
    private static Real LagrangeTestFct(Real x) => Math.Abs(x) + (0.5 * x) - (x * x);

    // interpolations.cpp's multif (lines 193-197).
    private static Real Multif(Real s, Real t, Real u, Real v, Real w) =>
        Math.Sqrt((s * Math.Sinh(Math.Log(t))) + Math.Exp(Math.Sin(u) * Math.Sin(3 * v)) + Math.Sinh(Math.Log(v * w)));
}
