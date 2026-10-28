// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010 Slava Mazur
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/linearleastsquaresregression.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class LinearLeastSquaresRegressionTests
{
    [Fact]
    public void TestRegression()
    {
        // Testing linear least-squares regression...

        const Real tolerance = 0.05;

        const Size nr = 100000;

        // PseudoRandom::rng_type
        var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(
            new MersenneTwisterUniformRng(1234));

        Func<Real, Real>[] v =
        [
            x => 1.0,
            x => x,
            x => x * x,
            Math.Sin,
        ];

        Func<Real, Real>[] w = [.. v, x => x * x];

        var errors = new List<string>();
        for (Size k = 0; k < 3; ++k)
        {
            Size i;
            Real[] a =
            [
                rng.Next().Value,
                rng.Next().Value,
                rng.Next().Value,
                rng.Next().Value,
            ];

            var x = new Real[nr];
            var y = new Real[nr];
            for (i = 0; i < nr; ++i)
            {
                x[i] = rng.Next().Value;

                // regression in y = a_1 + a_2*x + a_3*x^2 + a_4*sin(x) + eps
                y[i] = (a[0] * v[0](x[i])) + (a[1] * v[1](x[i])) + (a[2] * v[2](x[i]))
                    + (a[3] * v[3](x[i])) + rng.Next().Value;
            }

            GeneralLinearLeastSquares m = LinearRegression.Create(x, y, v);

            for (i = 0; i < v.Length; ++i)
            {
                if (m.StandardErrors()[i] > tolerance)
                {
                    errors.Add(
                        "Failed to reproduce linear regression coef."
                        + $"\n    error:     {m.StandardErrors()[i]}\n    tolerance: {tolerance}");
                }

                if (Math.Abs(m.Coefficients()[i] - a[i]) > 3 * m.StandardErrors()[i])
                {
                    errors.Add(
                        "Failed to reproduce linear regression coef."
                        + $"\n    calculated: {m.Coefficients()[i]}\n    error:      {m.StandardErrors()[i]}"
                        + $"\n    expected:   {a[i]}");
                }
            }

            m = LinearRegression.Create(x, y, w);

            Real[] ma =
            [
                m.Coefficients()[0], m.Coefficients()[1], m.Coefficients()[2] + m.Coefficients()[4],
                m.Coefficients()[3],
            ];
            Real[] err =
            [
                m.StandardErrors()[0], m.StandardErrors()[1],
                Math.Sqrt((m.StandardErrors()[2] * m.StandardErrors()[2])
                          + (m.StandardErrors()[4] * m.StandardErrors()[4])),
                m.StandardErrors()[3],
            ];
            for (i = 0; i < v.Length; ++i)
            {
                if (Math.Abs(ma[i] - a[i]) > 3 * err[i])
                {
                    errors.Add(
                        "Failed to reproduce linear regression coef."
                        + $"\n    calculated: {ma[i]}\n    error:      {err[i]}\n    expected:   {a[i]}");
                }
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void TestMultiDimRegression()
    {
        // Testing multi-dimensional linear least-squares regression...

        const Size nr = 100000;
        const Size dims = 4;
        const Real tolerance = 0.01;

        // PseudoRandom::rng_type
        var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(
            new MersenneTwisterUniformRng(1234));

        var v = new List<Func<Vector, Real>> { x => 1.0 };
        for (Size i = 0; i < dims; ++i)
        {
            Size index = i;

            // get_item
            v.Add(a => a[index]);
        }

        var coeff = new Vector(v.Count);
        for (Size i = 0; i < v.Count; ++i)
        {
            coeff[i] = rng.Next().Value;
        }

        var y = new Real[nr];
        var x = new Vector[nr];
        for (Size i = 0; i < nr; ++i)
        {
            x[i] = new Vector(dims);
            for (Size j = 0; j < dims; ++j)
            {
                x[i][j] = rng.Next().Value;
            }

            for (Size j = 0; j < v.Count; ++j)
            {
                y[i] += coeff[j] * v[j](x[i]);
            }

            y[i] += rng.Next().Value;
        }

        GeneralLinearLeastSquares m = LinearRegression.Create(x, y, v);

        var errors = new List<string>();
        for (Size i = 0; i < v.Count; ++i)
        {
            if (m.StandardErrors()[i] > tolerance)
            {
                errors.Add(
                    "Failed to reproduce linear regression coef."
                    + $"\n    error:     {m.StandardErrors()[i]}\n    tolerance: {tolerance}");
            }

            if (Math.Abs(m.Coefficients()[i] - coeff[i]) > 3 * tolerance)
            {
                errors.Add(
                    "Failed to reproduce linear regression coef."
                    + $"\n    calculated: {m.Coefficients()[i]}\n    error:      {m.StandardErrors()[i]}"
                    + $"\n    expected:   {coeff[i]}");
            }
        }

        // much simpler
        GeneralLinearLeastSquares m1 = LinearRegression.Create(x, y, 1.0);

        for (Size i = 0; i < m1.Dim; ++i)
        {
            if (m1.StandardErrors()[i] > tolerance)
            {
                errors.Add(
                    "Failed to reproduce linear regression coef."
                    + $"\n    error:     {m1.StandardErrors()[i]}\n    tolerance: {tolerance}");
            }

            if (Math.Abs(m1.Coefficients()[i] - coeff[i]) > 3 * tolerance)
            {
                errors.Add(
                    "Failed to reproduce linear regression coef."
                    + $"\n    calculated: {m1.Coefficients()[i]}\n    error:      {m1.StandardErrors()[i]}"
                    + $"\n    expected:   {coeff[i]}");
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    // Adapted: boost::circular_buffer, QuantLib's alternative container, is a list here.
    [Fact]
    public void Test1dLinearRegression()
    {
        // Testing 1D simple linear least-squares regression...

        /* Example taken from the QuantLib-User list, see posting
        * Multiple linear regression/weighted regression, Boris Skorodumov */

        Real[] x = [2.4, 1.8, 2.5, 3.0, 2.1, 1.2, 2.0, 2.7, 3.6];
        Real[] y = [7.8, 5.5, 8.0, 9.0, 6.5, 4.0, 6.3, 8.4, 10.2];

        GeneralLinearLeastSquares m = LinearRegression.Create(x, y);

        const Real tol = 0.0002;
        Real[] coeffExpected = [0.9448, 2.6853];
        Real[] errorsExpected = [0.3654, 0.1487];

        var errors = new List<string>();
        for (Size i = 0; i < 2; ++i)
        {
            if (Math.Abs(m.StandardErrors()[i] - errorsExpected[i]) > tol)
            {
                errors.Add(
                    "Failed to reproduce linear regression standard errors"
                    + $"\n    calculated: {m.StandardErrors()[i]}\n    expected:   {errorsExpected[i]}"
                    + $"\n    tolerance:  {tol}");
            }

            if (Math.Abs(m.Coefficients()[i] - coeffExpected[i]) > tol)
            {
                errors.Add(
                    "Failed to reproduce linear regression coef."
                    + $"\n    calculated: {m.Coefficients()[i]}\n    expected:   {coeffExpected[i]}"
                    + $"\n    tolerance:  {tol}");
            }
        }

        // an alternative container type
        List<Real> cx = [.. x], cy = [.. y];
        GeneralLinearLeastSquares m1 = LinearRegression.Create(cx, cy);

        for (Size i = 0; i < 2; ++i)
        {
            if (Math.Abs(m1.StandardErrors()[i] - errorsExpected[i]) > tol)
            {
                errors.Add(
                    "Failed to reproduce linear regression standard errors"
                    + $"\n    calculated: {m1.StandardErrors()[i]}\n    expected:   {errorsExpected[i]}"
                    + $"\n    tolerance:  {tol}");
            }

            if (Math.Abs(m1.Coefficients()[i] - coeffExpected[i]) > tol)
            {
                errors.Add(
                    "Failed to reproduce linear regression coef."
                    + $"\n    calculated: {m1.Coefficients()[i]}\n    expected:   {coeffExpected[i]}"
                    + $"\n    tolerance:  {tol}");
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}
