// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/functions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testModifiedBesselFunctions, testModifiedBesselFunctionsRegressionCases, testWeightedModifiedBesselFunctions, testExpm1, testLog1p

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FunctionsTests
{
    // qldefines.hpp's QL_EPSILON, which nanoql keeps internal.
    private const double QL_EPSILON = 2.220446049250313E-16;

    [Fact]
    public void TestFactorial()
    {
        // Testing factorial numbers...

        Real expected = 1.0;
        Real calculated = Factorial.Get(0);
        if (calculated != expected)
        {
            Assert.Fail($"Factorial(0) = {calculated}");
        }

        for (Natural i = 1; i < 171; ++i)
        {
            expected *= i;
            calculated = Factorial.Get(i);
            if (Math.Abs(calculated - expected) / expected > 1.0e-9)
            {
                Assert.Fail(
                    $"Factorial({i})"
                    + $"\n calculated: {calculated:E16}"
                    + $"\n   expected: {expected:E16}"
                    + $"\n rel. error: {Math.Abs(calculated - expected) / expected:E16}");
            }
        }
    }

    [Fact]
    public void TestGammaFunction()
    {
        // Testing Gamma function...

        Real expected = 0.0;
        Real calculated = new GammaFunction().LogValue(1);
        if (Math.Abs(calculated) > 1.0e-15)
        {
            Assert.Fail(
                "GammaFunction(1)\n"
                + $"    calculated: {calculated:E16}\n"
                + $"    expected:   {expected:E16}");
        }

        for (Size i = 2; i < 9000; i++)
        {
            expected += Math.Log((Real)i);
            calculated = new GammaFunction().LogValue((Real)(i + 1));
            if (Math.Abs(calculated - expected) / expected > 1.0e-9)
            {
                Assert.Fail(
                    $"GammaFunction({i})\n"
                    + $"    calculated: {calculated:E16}\n"
                    + $"    expected:   {expected:E16}\n"
                    + $"    rel. error: {Math.Abs(calculated - expected) / expected:E16}");
            }
        }
    }

    [Fact]
    public void TestGammaValues()
    {
        // Testing Gamma values...

        // reference results are calculated with R
        Real[][] tasks =
        [
            [0.0001, 9999.422883231624, 1e3],
            [1.2, 0.9181687423997607, 1e3],
            [7.3, 1271.4236336639089586, 1e3],
            [-1.1, 9.7148063829028946, 1e3],
            [-4.001, -41.6040228304425312, 1e3],
            [-4.999, -8.347576090315059, 1e3],
            [-19.000001, 8.220610833201313e-12, 1e8],
            [-19.5, 5.811045977502255e-18, 1e3],
            [-21.000001, 1.957288098276488e-14, 1e8],
            [-21.5, 1.318444918321553e-20, 1e6],
        ];

        foreach (Real[] task in tasks)
        {
            Real x = task[0];
            Real expected = task[1];
            Real calculated = new GammaFunction().Value(x);
            Real tol = task[2] * QL_EPSILON * Math.Abs(expected);

            if (Math.Abs(calculated - expected) > tol)
            {
                Assert.Fail(
                    $"GammaFunction({x})\n"
                    + $"    calculated: {calculated:E16}\n"
                    + $"    expected:   {expected:E16}\n"
                    + $"    rel. error: {Math.Abs(calculated - expected) / expected:E16}");
            }
        }
    }

    [Fact]
    public void TestCumulativeGammaDistribution()
    {
        // Testing cumulative gamma distribution...

        var exponential = new CumulativeGammaDistribution(1.0);
        CheckEqual(exponential.Evaluate(-1.0), 0.0);
        CheckEqual(exponential.Evaluate(0.0), 0.0);
        CheckClose(exponential.Evaluate(0.5), 1.0 - Math.Exp(-0.5), 1.0e-5);
        CheckClose(exponential.Evaluate(2.0), 1.0 - Math.Exp(-2.0), 1.0e-5);

        var shapeTwo = new CumulativeGammaDistribution(2.0);
        CheckClose(shapeTwo.Evaluate(1.0), 1.0 - 2.0 * Math.Exp(-1.0), 1.0e-5);
        CheckClose(shapeTwo.Evaluate(4.0), 1.0 - 5.0 * Math.Exp(-4.0), 1.0e-5);

        CheckExpectedErrorMessage(() => _ = new CumulativeGammaDistribution(0.0), "invalid parameter for gamma distribution");
        CheckExpectedErrorMessage(() => _ = new CumulativeGammaDistribution(-1.0), "invalid parameter for gamma distribution");
        CheckExpectedErrorMessage(() => new GammaFunction().LogValue(0.0), "positive argument required");
    }

    // BOOST_CHECK_CLOSE(a, b, percent): Boost.Test's strong check, |a - b|/|b| and |a - b|/|a| each within percent/100,
    // with its safe division.
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

    // BOOST_CHECK_EQUAL on reals: exact equality.
    private static void CheckEqual(Real actual, Real expected) =>
        Assert.True(actual == expected, $"{actual:R} != {expected:R}");

    // BOOST_CHECK_EXCEPTION(..., Error, ExpectedErrorMessage(expected)): QuantLib's Error is an ArgumentException or a
    // NanoQLException in nanoql, and its message must contain the expected text.
    private static void CheckExpectedErrorMessage(Action action, string expected)
    {
        Exception? e = Record.Exception(action);
        if (e is not (ArgumentException or NanoQLException))
        {
            Assert.Fail(
                $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
        }

        Assert.True(
            e.Message.Contains(expected, StringComparison.Ordinal),
            $"Error expected to contain: '{expected}'. Actual error is: '{e.Message}'.");
    }
}
