// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/functions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
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

    [Fact]
    public void TestModifiedBesselFunctions()
    {
        // Testing modified Bessel function of first and second kind...

        /* reference values are computed with R and the additional package Bessel
         * http://cran.r-project.org/web/packages/Bessel
         */
        Real[][] r =
        [
            [-1.3, 2.0, 1.2079888436539505, 0.1608243636110430],
            [1.3, 2.0, 1.2908192151358788, 0.1608243636110430],
            [0.001, 2.0, 2.2794705965773794, 0.1138938963603362],
            [1.2, 0.5, 0.1768918783499572, 2.1086579232338192],
            [2.3, 0.1, 0.00037954958988425198, 572.096866928290183],
            [-2.3, 1.1, 1.07222017902746969, 1.88152553684107371],
            [-10.0001, 1.1, 13857.7715614282552, 69288858.9474423379],
        ];

        foreach (Real[] i in r)
        {
            Real nu = i[0];
            Real x = i[1];
            Real expected_i = i[2];
            Real expected_k = i[3];
            Real tol_i = 5e4 * QL_EPSILON * Math.Abs(expected_i);
            Real tol_k = 5e4 * QL_EPSILON * Max(Math.Abs(expected_k), Math.Abs(expected_i));

            Real calculated_i = ModifiedBesselFunctions.ModifiedBesselFunction_i(nu, x);
            Real calculated_k = ModifiedBesselFunctions.ModifiedBesselFunction_k(nu, x);

            if (Math.Abs(expected_i - calculated_i) > tol_i)
            {
                Assert.Fail(
                    "failed to reproduce modified Bessel function of first kind"
                    + $"\n order     : {nu:E16}"
                    + $"\n argument  : {x:E16}"
                    + $"\n calculated: {calculated_i:E16}"
                    + $"\n expected  : {expected_i:E16}"
                    + $"\n difference: {Math.Abs(expected_i - calculated_i):E16}"
                    + $"\n tolerance : {tol_i:E16}");
            }

            if (Math.Abs(expected_k - calculated_k) > tol_k)
            {
                Assert.Fail(
                    "failed to reproduce modified Bessel function of second kind"
                    + $"\n order     : {nu:E16}"
                    + $"\n argument  : {x:E16}"
                    + $"\n calculated: {calculated_k:E16}"
                    + $"\n expected  : {expected_k:E16}"
                    + $"\n difference: {Math.Abs(expected_k - calculated_k):E16}"
                    + $"\n tolerance : {tol_k:E16}");
            }
        }

        Real[][] c =
        [
            [-1.3, 2.0, 0.0, 1.2079888436539505, 0.0, 0.1608243636110430, 0.0],
            [1.2, 1.5, 0.3, 0.7891550871263575, 0.2721408731632123, 0.275126507673411, -0.1316314405663727],
            [1.2, -1.5, 0.0, -0.6650597524355781, -0.4831941938091643, -0.251112360556051, -2.400130904230102],
            [-11.2, 1.5, 0.3, 12780719.20252659, 16401053.26770633, -34155172.65672453, -43830147.36759921],
            [1.2, -1.5, 2.0, -0.3869803778520574, 0.9756701796853728, -3.111629716783005, 0.6307859871879062],
            [1.2, 0.0, 9.9999, -0.03507838078252647, 0.1079601550451466, -0.05979939995451453, 0.3929814473878203],
            [1.2, 0.0, 10.1, -0.02782046891519293, 0.08562259917678558, -0.02035685034691133, 0.3949834389686676],
            [1.2, 0.0, 12.1, 0.07092110620741207, -0.2182727210128104, 0.3368505862966958, -0.1299038064313366],
            [1.2, 0.0, 14.1, -0.03014378676768797, 0.09277303628303372, -0.237531022649052, -0.2351923034581644],
            [1.2, 0.0, 16.1, -0.03823210284792657, 0.1176663135266562, -0.1091239402448228, 0.2930535651966139],
            [1.2, 0.0, 18.1, 0.05626742394733754, -0.173173324361983, 0.2941636588154642, -0.02023355577954348],
            [1.2, 0.0, 180.1, -0.001230682086826484, 0.003787649998122361, 0.02284509628723454, 0.09055419580980778],
            [1.2, 0.0, 21.0, -0.04746415965014021, 0.1460796627610969, -0.2693825171336859, -0.04830804448126782],
            [1.2, 10.0, 0.0, 2609.784936867044, 0, 1.904394919838336e-05, 0],
            [1.2, 14.0, 0.0, 122690.4873454286, 0, 2.902060692576643e-07, 0],
            [1.2, 20.0, 10.0, -37452017.91168936, -13917587.22151363, -3.821534367487143e-10, 4.083211255351664e-10],
            [1.2, 9.0, 9.0, -621.7335051293694, 618.1455736670332, -4.480795479964915e-05, -3.489034389148745e-08],
        ];

        foreach (Real[] i in c)
        {
            Real nu = i[0];
            var z = new Complex(i[1], i[2]);
            var expected_i = new Complex(i[3], i[4]);
            var expected_k = new Complex(i[5], i[6]);

            Real tol_i = 5e4 * QL_EPSILON * Complex.Abs(expected_i);
            Real tol_k = 1e6 * QL_EPSILON * Complex.Abs(expected_k);

            Complex calculated_i = ModifiedBesselFunctions.ModifiedBesselFunction_i(nu, z);
            Complex calculated_k = ModifiedBesselFunctions.ModifiedBesselFunction_k(nu, z);

            if (Complex.Abs(expected_i - calculated_i) > tol_i)
            {
                Assert.Fail(
                    "failed to reproduce modified Bessel function of first kind"
                    + $"\n order     : {nu}"
                    + $"\n argument  : {z}"
                    + $"\n calculated: {calculated_i}"
                    + $"\n expected  : {expected_i}");
            }

            // do not check small values
            if (Complex.Abs(expected_k) > 1e-4 && Complex.Abs(expected_k - calculated_k) > tol_k)
            {
                Assert.Fail(
                    "failed to reproduce modified Bessel function of second kind"
                    + $"\n order     : {nu}"
                    + $"\n argument  : {z}"
                    + $"\n diff      : {calculated_k - expected_k}"
                    + $"\n calculated: {calculated_k}"
                    + $"\n expected  : {expected_k}");
            }
        }
    }

    [Fact]
    public void TestModifiedBesselFunctionsRegressionCases()
    {
        // Testing modified Bessel functions at integer order and large argument...

        // Every reference point in testModifiedBesselFunctions sits outside the
        // two regimes below: the orders there are all non-integer and the largest
        // argument is 2. Reference values from boost::math::cyl_bessel_i/k.
        Real[][] r =
        [
            // integer order: the I_(-nu) - I_nu quotient is 0/0 here
            [0, 2, 2.279585302336067, 0.1138938727495334],
            [1, 2, 1.590636854637329, 0.1398658818165224],
            [2, 2, 0.6889484476987382, 0.2537597545660559],
            [5, 2, 0.009825679323131702, 9.431049100596468],

            // large argument: the same quotient used to collapse to exactly zero
            [0.5, 13, 48951.57328321644, 7.857058697340969e-07],
            [0.5, 14, 128223.8446632749, 2.785307663176792e-07],
            [2.3, 20, 38035457.61215977, 6.531642087006757e-10],
            [1.2, 30, 762821352844.8658, 2.183426121339329e-14],
        ];

        foreach (Real[] i in r)
        {
            Real nu = i[0];
            Real x = i[1];
            Real expected_i = i[2];
            Real expected_k = i[3];

            // Unlike the check above this scales the second-kind tolerance by the
            // second-kind value: out here I is many orders of magnitude larger
            // than K, so a bound taken from max(I, K) would accept anything.
            Real tol_i = 5e4 * QL_EPSILON * Math.Abs(expected_i);
            Real tol_k = 5e4 * QL_EPSILON * Math.Abs(expected_k);

            Real calculated_i = ModifiedBesselFunctions.ModifiedBesselFunction_i(nu, x);
            Real calculated_k = ModifiedBesselFunctions.ModifiedBesselFunction_k(nu, x);

            if (Math.Abs(expected_i - calculated_i) > tol_i)
            {
                Assert.Fail(
                    "failed to reproduce modified Bessel function of first kind"
                    + $"\n order      : {nu}"
                    + $"\n argument   : {x}"
                    + $"\n calculated : {calculated_i}"
                    + $"\n expected   : {expected_i}");
            }

            if (Math.Abs(expected_k - calculated_k) > tol_k)
            {
                Assert.Fail(
                    "failed to reproduce modified Bessel function of second kind"
                    + $"\n order      : {nu}"
                    + $"\n argument   : {x}"
                    + $"\n calculated : {calculated_k}"
                    + $"\n expected   : {expected_k}");
            }
        }
    }

    [Fact]
    public void TestWeightedModifiedBesselFunctions()
    {
        // Testing weighted modified Bessel functions...
        for (Real nu = -5.0; nu <= 5.0; nu += 0.5)
        {
            for (Real x = 0.1; x <= 15.0; x += 0.5)
            {
                Real calculated_i = ModifiedBesselFunctions.ModifiedBesselFunction_i_exponentiallyWeighted(nu, x);
                Real expected_i = ModifiedBesselFunctions.ModifiedBesselFunction_i(nu, x) * Math.Exp(-x);
                Real calculated_k = ModifiedBesselFunctions.ModifiedBesselFunction_k_exponentiallyWeighted(nu, x);

                // Mirror the check above for the first kind rather than
                // inlining the old I_(-nu) - I_nu quotient: that expression is
                // 0/0 at integer order, so using it here made the test assert
                // the very failure it should catch.
                Real expected_k = ModifiedBesselFunctions.ModifiedBesselFunction_k(nu, x) * Math.Exp(-x);
                Real tol_i = Max(QL_EPSILON, 1e3 * QL_EPSILON * Math.Abs(expected_i) * Max(Math.Exp(x), 1.0));
                Real tol_k = Max(QL_EPSILON, 1e3 * QL_EPSILON * Math.Abs(expected_k) * Max(Math.Exp(x), 1.0));
                if (Math.Abs(expected_i - calculated_i) > tol_i)
                {
                    Assert.Fail(
                        "failed to verify exponentially weighted modified Bessel function of first kind"
                        + $"\n order      : {nu}"
                        + $"\n argument   : {x}"
                        + $"\n calculated : {calculated_i}"
                        + $"\n expected   : {expected_i}"
                        + $"\n tolerance  : {tol_i}"
                        + $"\n difference : {expected_i - calculated_i}");
                }

                if (Math.Abs(expected_k - calculated_k) > tol_k)
                {
                    Assert.Fail(
                        "failed to verify exponentially weighted modified Bessel function of second kind"
                        + $"\n order      : {nu}"
                        + $"\n argument   : {x}"
                        + $"\n calculated : {calculated_k}"
                        + $"\n expected   : {expected_k}"
                        + $"\n tolerance  : {tol_k}"
                        + $"\n difference : {expected_k - calculated_k}");
                }
            }
        }

        for (Real nu = -5.0; nu <= 5.0; nu += 0.5)
        {
            for (Real x = -5.0; x <= 5.0; x += 0.5)
            {
                for (Real y = -5.0; y <= 5.0; y += 0.5)
                {
                    var z = new Complex(x, y);
                    Complex calculated_i = ModifiedBesselFunctions.ModifiedBesselFunction_i_exponentiallyWeighted(nu, z);
                    Complex expected_i = ModifiedBesselFunctions.ModifiedBesselFunction_i(nu, z) * Complex.Exp(-z);
                    Complex calculated_k = ModifiedBesselFunctions.ModifiedBesselFunction_k_exponentiallyWeighted(nu, z);

                    // As above, check against the unweighted function rather
                    // than against the old I_(-nu) - I_nu quotient, which is
                    // 0/0 at integer order.
                    Complex expected_k = ModifiedBesselFunctions.ModifiedBesselFunction_k(nu, z) * Complex.Exp(-z);
                    Real tol_i = Max(QL_EPSILON, 1e3 * QL_EPSILON * Complex.Abs(calculated_i));
                    Real tol_k = Max(QL_EPSILON, 1e3 * QL_EPSILON * Complex.Abs(calculated_k));
                    if (Complex.Abs(calculated_i - expected_i) > tol_i)
                    {
                        Assert.Fail(
                            "failed to verify exponentially weighted modified Bessel function of first kind"
                            + $"\n order      : {nu}"
                            + $"\n argument   : {x}"
                            + $"\n calculated : {calculated_i}"
                            + $"\n expected   : {expected_i}"
                            + $"\n tolerance  : {tol_i}"
                            + $"\n difference : {expected_i - calculated_i}");
                    }

                    if (Complex.Abs(expected_k - calculated_k) > tol_k)
                    {
                        Assert.Fail(
                            "failed to verify exponentially weighted modified Bessel function of second kind"
                            + $"\n order      : {nu}"
                            + $"\n argument   : {x}"
                            + $"\n calculated : {calculated_k}"
                            + $"\n expected   : {expected_k}"
                            + $"\n tolerance  : {tol_k}"
                            + $"\n difference : {expected_k - calculated_k}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestExpm1()
    {
        // Testing complex valued expm1...

        var z = new Complex(1.2, 0.5);
        CheckSmall(Complex.Abs(Complex.Exp(z) - 1.0 - Expm1Functions.Expm1(z)), 10 * QL_EPSILON);

        Complex calculated = Expm1Functions.Expm1(new Complex(5e-6, 5e-5));

        // scipy reference value
        var expected = new Complex(4.998762493771078e-06, 5.000024997979157e-05);
        Real tol = Max(2.2e-14, 100 * QL_EPSILON);
        CheckCloseFraction(calculated.Real, expected.Real, tol);
        CheckCloseFraction(calculated.Imaginary, expected.Imaginary, tol);
    }

    [Fact]
    public void TestLog1p()
    {
        // Testing complex valued log1p...

        var z = new Complex(1.2, 0.57);
        CheckSmall(Complex.Abs(Complex.Log(1.0 + z) - Expm1Functions.Log1p(z)), 10 * QL_EPSILON);

        Complex calculated = Expm1Functions.Log1p(new Complex(5e-6, 5e-5));

        // scipy reference value
        var expected = new Complex(5.0012374875401984e-06, 4.999974995958395e-05);
        Real tol = Max(2.2e-14, 100 * QL_EPSILON);
        CheckCloseFraction(calculated.Real, expected.Real, tol);
        CheckCloseFraction(calculated.Imaginary, expected.Imaginary, tol);
    }

    // std::max: the second only when strictly larger.
    private static Real Max(Real a, Real b) => a < b ? b : a;

    // QL_CHECK_SMALL: |fpv| <= tolerance.
    private static void CheckSmall(Real fpv, Real tolerance) =>
        Assert.True(Math.Abs(fpv) <= tolerance, $"|{fpv}| exceeds {tolerance}");

    // QL_CHECK_CLOSE_FRACTION, Boost's BOOST_CHECK_CLOSE_FRACTION: |a - b|/|b| and |a - b|/|a| each within the
    // fraction.
    private static void CheckCloseFraction(Real a, Real b, Real fraction)
    {
        Real diff = Math.Abs(a - b);
        bool close = SafeDivision(diff, Math.Abs(b)) <= fraction && SafeDivision(diff, Math.Abs(a)) <= fraction;
        Assert.True(close, $"difference between {a:R} and {b:R} exceeds {fraction}");
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
