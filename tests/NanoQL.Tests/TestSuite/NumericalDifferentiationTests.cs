// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/numericaldifferentiation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class NumericalDifferentiationTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    [Fact]
    public void TestTabulatedCentralScheme()
    {
        // Testing numerical differentiation using the central scheme...
        Func<Real, Real>? f = null;

        const NumericalDifferentiationScheme central = NumericalDifferentiationScheme.Central;

        // see http://en.wikipedia.org/wiki/Finite_difference_coefficient
        CheckTwoArraysAreTheSame(new NumericalDifferentiation(f, 1, 1.0, 3, central).Weights(), [-0.5, 0.0, 0.5]);

        CheckTwoArraysAreTheSame(new NumericalDifferentiation(f, 1, 0.5, 3, central).Weights(), [-1.0, 0.0, 1.0]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 1, 0.25, 7, central).Weights(),
            [-4 / 60.0, 12 / 20.0, -12 / 4.0, 0.0, 12 / 4.0, -12 / 20.0, 4 / 60.0]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 4, Math.Pow(0.5, 0.25), 9, central).Weights(),
            [
                14 / 240.0, -4 / 5.0, 338 / 60.0, -244 / 15.0, 182 / 8.0, -244 / 15.0, 338 / 60.0, -4 / 5.0,
                14 / 240.0,
            ]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 1, 0.5, 7, central).Offsets(), [-1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5]);
    }

    [Fact]
    public void TestTabulatedBackwardScheme()
    {
        // Testing numerical differentiation using the backward scheme...
        Func<Real, Real>? f = null;

        const NumericalDifferentiationScheme backward = NumericalDifferentiationScheme.Backward;

        // see http://en.wikipedia.org/wiki/Finite_difference_coefficient
        CheckTwoArraysAreTheSame(new NumericalDifferentiation(f, 1, 1.0, 2, backward).Weights(), [1.0, -1.0]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 2, 2.0, 4, backward).Weights(), [2 / 4.0, -5 / 4.0, 4 / 4.0, -1.0 / 4.0]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 4, 1.0, 6, backward).Weights(), [3.0, -14.0, 26.0, -24.0, 11.0, -2.0]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 2, 0.5, 4, backward).Offsets(), [0.0, -0.5, -1.0, -1.5]);
    }

    [Fact]
    public void TestTabulatedForwardScheme()
    {
        // Testing numerical differentiation using the Forward scheme...
        Func<Real, Real>? f = null;

        const NumericalDifferentiationScheme forward = NumericalDifferentiationScheme.Forward;

        // see http://en.wikipedia.org/wiki/Finite_difference_coefficient
        CheckTwoArraysAreTheSame(new NumericalDifferentiation(f, 1, 1.0, 2, forward).Weights(), [-1.0, 1.0]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 1, 0.5, 3, forward).Weights(), [-6 / 2.0, 4.0, -2 / 2.0]);

        CheckTwoArraysAreTheSame(
            new NumericalDifferentiation(f, 1, 0.5, 7, forward).Weights(),
            [-98 / 20.0, 12.0, -30 / 2.0, 40 / 3.0, -30 / 4.0, 12 / 5.0, -2 / 6.0]);

        CheckTwoArraysAreTheSame(new NumericalDifferentiation(f, 2, 0.5, 4, forward).Offsets(), [0.0, 0.5, 1.0, 1.5]);
    }

    [Fact]
    public void TestIrregularSchemeFirstOrder()
    {
        // Testing numerical differentiation of first order using an irregular scheme...
        Func<Real, Real>? f = null;

        const Real h1 = 5e-7;
        const Real h2 = 3e-6;

        const Real alpha = -h2 / (h1 * (h1 + h2));
        const Real gamma = h1 / (h2 * (h1 + h2));
        const Real beta = -alpha - gamma;

        Real[] offsets = [-h1, 0.0, h2];

        CheckTwoArraysAreTheSame(new NumericalDifferentiation(f, 1, offsets).Weights(), [alpha, beta, gamma]);
    }

    [Fact]
    public void TestIrregularSchemeSecondOrder()
    {
        // Testing numerical differentiation of second order using an irregular scheme...
        Func<Real, Real>? f = null;

        const Real h1 = 2e-7;
        const Real h2 = 8e-8;

        const Real alpha = 2 / (h1 * (h1 + h2));
        const Real gamma = 2 / (h2 * (h1 + h2));
        const Real beta = -alpha - gamma;

        Real[] offsets = [-h1, 0.0, h2];

        CheckTwoArraysAreTheSame(new NumericalDifferentiation(f, 2, offsets).Weights(), [alpha, beta, gamma]);
    }

    [Fact]
    public void TestDerivativesOfSineFunction()
    {
        // Testing numerical differentiation of sin function...

        Func<Real, Real> f = Math.Sin;

        Func<Real, Real> df_central = new NumericalDifferentiation(
            f, 1, Math.Sqrt(QL_EPSILON), 3, NumericalDifferentiationScheme.Central).Evaluate;

        Func<Real, Real> df_backward = new NumericalDifferentiation(
            f, 1, Math.Sqrt(QL_EPSILON), 3, NumericalDifferentiationScheme.Backward).Evaluate;

        Func<Real, Real> df_forward = new NumericalDifferentiation(
            f, 1, Math.Sqrt(QL_EPSILON), 3, NumericalDifferentiationScheme.Forward).Evaluate;

        for (Real x = 0.0; x < 5.0; x += 0.1)
        {
            Real calculatedCentral = df_central(x);
            Real calculatedBackward = df_backward(x);
            Real calculatedForward = df_forward(x);
            Real expected = Math.Cos(x);

            SingleValueTest("central first", calculatedCentral, expected, 1e-8);
            SingleValueTest("backward first", calculatedBackward, expected, 1e-6);
            SingleValueTest("forward first", calculatedForward, expected, 1e-6);
        }

        Func<Real, Real> df4_central =
            new NumericalDifferentiation(f, 4, 1e-2, 7, NumericalDifferentiationScheme.Central).Evaluate;
        Func<Real, Real> df4_backward =
            new NumericalDifferentiation(f, 4, 1e-2, 7, NumericalDifferentiationScheme.Backward).Evaluate;
        Func<Real, Real> df4_forward =
            new NumericalDifferentiation(f, 4, 1e-2, 7, NumericalDifferentiationScheme.Forward).Evaluate;

        for (Real x = 0.0; x < 5.0; x += 0.1)
        {
            Real calculatedCentral = df4_central(x);
            Real calculatedBackward = df4_backward(x);
            Real calculatedForward = df4_forward(x);
            Real expected = Math.Sin(x);

            SingleValueTest("central 4th", calculatedCentral, expected, 1e-4);
            SingleValueTest("backward 4th", calculatedBackward, expected, 1e-4);
            SingleValueTest("forward 4th", calculatedForward, expected, 1e-4);
        }

        Real[] offsets = [-0.01, -0.02, 0.03, 0.014, 0.041];
        var df3_irregular = new NumericalDifferentiation(f, 3, offsets);

        CheckTwoArraysAreTheSame(df3_irregular.Offsets(), offsets);

        for (Real x = 0.0; x < 5.0; x += 0.1)
        {
            Real calculatedIrregular = df3_irregular.Evaluate(x);
            Real expected = -Math.Cos(x);

            SingleValueTest("irregular 3th", calculatedIrregular, expected, 5e-5);
        }
    }

    [Fact]
    public void TestCoefficientBasedOnVandermonde()
    {
        // Testing coefficients from numerical differentiation by comparison with results from Vandermonde matrix
        // inversion...
        Func<Real, Real>? f = null;

        for (int order = 0; order < 5; ++order)
        {
            for (int nGridPoints = order + 1; nGridPoints < order + 3; ++nGridPoints)
            {
                var gridPoints = new Real[nGridPoints];
                for (int i = 0; i < nGridPoints; ++i)
                {
                    Real p = i;
                    gridPoints[i] = Math.Sin(p) + Math.Cos(p); // strange points
                }

                const Real x = 0.3902842; // strange points
                Vector weightsVandermonde = VandermondeCoefficients(order, x, gridPoints);
                var shifted = new Real[nGridPoints];
                for (int i = 0; i < nGridPoints; ++i)
                {
                    shifted[i] = gridPoints[i] - x;
                }

                var nd = new NumericalDifferentiation(f, order, shifted);

                var offsetsPlusX = new Real[nGridPoints];
                for (int i = 0; i < nGridPoints; ++i)
                {
                    offsetsPlusX[i] = nd.Offsets()[i] + x;
                }

                CheckTwoArraysAreTheSame(gridPoints, offsetsPlusX);
                CheckTwoArraysAreTheSame(weightsVandermonde.AsSpan(), nd.Weights().AsSpan());
            }
        }
    }

    private static bool IsTheSame(Real a, Real b)
    {
        const double eps = 500 * QL_EPSILON;

        if (Math.Abs(b) < QL_EPSILON)
        {
            return Math.Abs(a) < eps;
        }
        else
        {
            return Math.Abs((a - b) / b) < eps;
        }
    }

    private static void CheckTwoArraysAreTheSame(ImmutableArray<Real> calculated, ReadOnlySpan<Real> expected) =>
        CheckTwoArraysAreTheSame(calculated.AsSpan(), expected);

    private static void CheckTwoArraysAreTheSame(ReadOnlySpan<Real> calculated, ReadOnlySpan<Real> expected)
    {
        bool correct = calculated.Length == expected.Length;
        for (int i = 0; correct && i < calculated.Length; ++i)
        {
            correct = IsTheSame(calculated[i], expected[i]);
        }

        if (!correct)
        {
            var difference = new Real[Math.Min(calculated.Length, expected.Length)];
            for (int i = 0; i < difference.Length; ++i)
            {
                difference[i] = expected[i] - calculated[i];
            }

            Assert.Fail(
                "Failed to reproduce expected array"
                + $"\n    calculated: {string.Join(", ", calculated.ToArray())}"
                + $"\n    expected:   {string.Join(", ", expected.ToArray())}"
                + $"\n    difference: {string.Join(", ", difference)}");
        }
    }

    private static void SingleValueTest(string comment, Real calculated, Real expected, Real tol)
    {
        if (Math.Abs(calculated - expected) > tol)
        {
            Assert.Fail(
                $"Failed to reproduce {comment} order derivative"
                + $"\n    calculated: {calculated}"
                + $"\n      expected: {expected}"
                + $"\n     tolerance: {tol}"
                + $"\n    difference: {expected - calculated}");
        }
    }

    private static Vector VandermondeCoefficients(int order, Real x, Real[] gridPoints)
    {
        int n = gridPoints.Length;
        var q = new Real[n];
        for (int j = 0; j < n; ++j)
        {
            q[j] = gridPoints[j] - x;
        }

        var m = new Matrix(n, n, 1.0);
        for (int i = 1; i < n; ++i)
        {
            Real fact = Factorial.Get(i);
            for (int j = 0; j < n; ++j)
            {
                m[i, j] = Math.Pow(q[j], i) / fact;
            }
        }

        var b = new Vector(n, 0.0);
        b[order] = 1.0;
        return MatrixFunctions.Inverse(m) * b;
    }
}
