// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2013 Fabien Le Floc'h
// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/distributions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testInverseCumulativeStudent, testStudentDistribution, testBivariateCumulativeStudent, testBivariateCumulativeStudentInvalidParameters, testBivariateCumulativeStudentBoundaryContinuity, testBivariateCumulativeStudentVsBivariate, testInvCDFviaStochasticCollocation

using NanoQL.Numerics;
using static NanoQL.Numerics.ComparisonFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class DistributionsTests
{
    // qldefines.hpp's QL_EPSILON, QL_MAX_REAL and QL_MIN_REAL, which nanoql keeps internal, and mathconstants.hpp's
    // M_PI.
    private const double QL_EPSILON = 2.220446049250313E-16;
    private const double QL_MAX_REAL = double.MaxValue;
    private const double QL_MIN_REAL = -double.MaxValue;
    private const double M_PI = 3.141592653589793238462643383280;

    private const double average = 1.0, sigma = 2.0;

    [Fact]
    public void TestNormal()
    {
        // Testing normal distributions...

        var invCumStandardNormal = new InverseCumulativeNormal();
        Real check = invCumStandardNormal.Evaluate(0.5);
        if (check != 0.0e0)
        {
            Assert.Fail($"C++ inverse cumulative of the standard normal at 0.5 is {check}\n instead of zero: something is wrong!");
        }

        var normal = new NormalDistribution(average, sigma);
        var cum = new CumulativeNormalDistribution(average, sigma);
        var invCum = new InverseCumulativeNormal(average, sigma);

        Size numberOfStandardDeviation = 6;
        Real xMin = average - numberOfStandardDeviation * sigma,
             xMax = average + numberOfStandardDeviation * sigma;
        Size N = 100001;
        Real h = (xMax - xMin) / (N - 1);

        Real[] x = new Real[N], y = new Real[N], yd = new Real[N], temp = new Real[N], diff = new Real[N];

        Size i;
        for (i = 0; i < N; i++)
        {
            x[i] = xMin + h * i;
        }

        for (i = 0; i < N; i++)
        {
            y[i] = Gaussian(x[i]);
            yd[i] = GaussianDerivative(x[i]);
        }

        // check that normal = Gaussian
        for (i = 0; i < N; i++)
        {
            temp[i] = normal.Evaluate(x[i]);
            diff[i] = y[i] - temp[i];
        }

        Real e = Norm(diff, h);
        if (e > 1.0e-16)
        {
            Assert.Fail($"norm of C++ NormalDistribution minus analytic Gaussian: {e}\ntolerance exceeded");
        }

        // check that invCum . cum = identity
        for (i = 0; i < N; i++)
        {
            temp[i] = invCum.Evaluate(cum.Evaluate(x[i]));
            diff[i] = x[i] - temp[i];
        }

        e = Norm(diff, h);
        if (e > 1.0e-7)
        {
            Assert.Fail($"norm of invCum . cum minus identity: {e}\ntolerance exceeded");
        }

        var mInvCum = new MaddockInverseCumulativeNormal(average, sigma);
        for (i = 0; i < N; i++)
        {
            diff[i] = x[i] - mInvCum.Evaluate(cum.Evaluate(x[i]));
        }

        e = Norm(diff, h);
        if (e > 1.0e-7)
        {
            Assert.Fail($"norm of MaddockInvCum . cum minus identity: {e}\ntolerance exceeded");
        }

        // check that cum.derivative = Gaussian
        for (i = 0; i < N; i++)
        {
            temp[i] = cum.Derivative(x[i]);
            diff[i] = y[i] - temp[i];
        }

        e = Norm(diff, h);
        if (e > 1.0e-16)
        {
            Assert.Fail($"norm of C++ Cumulative.derivative minus analytic Gaussian: {e}\ntolerance exceeded");
        }

        // check that normal.derivative = gaussianDerivative
        for (i = 0; i < N; i++)
        {
            temp[i] = normal.Derivative(x[i]);
            diff[i] = yd[i] - temp[i];
        }

        e = Norm(diff, h);
        if (e > 1.0e-16)
        {
            Assert.Fail($"norm of C++ Normal.derivative minus analytic derivative: {e}\ntolerance exceeded");
        }
    }

    [Fact]
    public void TestInverseCumulativeNormal()
    {
        // Testing inverse cumulative normal distribution...

        var invCum = new InverseCumulativeNormal();
        Assert.True(double.IsFinite(invCum.Evaluate(0.0)));
        Assert.True(double.IsFinite(invCum.Evaluate(1.0)));
        Assert.True(double.IsFinite(invCum.Evaluate(QL_EPSILON)));
        Assert.True(double.IsFinite(invCum.Evaluate(1.0 - QL_EPSILON)));
        Assert.True(invCum.Evaluate(0.0) < 0.0);
        Assert.True(invCum.Evaluate(1.0) > 0.0);
        CheckClose(invCum.Evaluate(0.02425), -1.972961049, 1.0e-6);
        CheckClose(invCum.Evaluate(0.97575), 1.972961049, 1.0e-6);
        CheckClose(invCum.Evaluate(0.1), -invCum.Evaluate(0.9), 1.0e-12);

        // Values sufficiently close to the endpoints are recovered as finite
        // sentinels; values outside that recovery tolerance are rejected.
        CheckEqual(invCum.Evaluate(-0.5 * QL_EPSILON), QL_MIN_REAL);
        CheckThrowsError(() => invCum.Evaluate(-2.0 * QL_EPSILON));
        CheckThrowsError(() => invCum.Evaluate(1.0 + 100.0 * QL_EPSILON));

        Real[] probabilities = [1.0e-6, 0.01, 0.1, 0.5, 0.9, 0.99, 1.0 - 1.0e-6];
        Real previous = -QL_MAX_REAL;
        foreach (Real probability in probabilities)
        {
            Real value = invCum.Evaluate(probability);
            Assert.True(value > previous);
            previous = value;
        }

        var shiftedInvCum = new InverseCumulativeNormal(3.0, 2.0);
        foreach (Real probability in probabilities)
        {
            CheckClose(shiftedInvCum.Evaluate(probability), 3.0 + 2.0 * invCum.Evaluate(probability), 1.0e-12);
        }

        CheckThrowsError(() => invCum.Evaluate(-1.0e-8));
        CheckThrowsError(() => invCum.Evaluate(1.0 + 1.0e-4));
        CheckThrowsError(() => _ = new InverseCumulativeNormal(0.0, -1.0));
        CheckThrowsError(() => _ = new InverseCumulativeNormal(0.0, double.NaN));
    }

    // Adapted: Boost.Math's quantile, the second reference (lines 371 and 388-390), is MaddockInverseCumulativeNormal,
    // nanoql's port of it, which the harness vectors check against QuantLib's (cxx/math/distributions/normaldistribution).
    [Fact]
    public void TestMoroInverseCumulativeNormal()
    {
        // Testing Moro inverse cumulative normal distribution...

        var invCum = new InverseCumulativeNormal();
        var moroInvCum = new MoroInverseCumulativeNormal();
        var standardNormal = new MaddockInverseCumulativeNormal(0.0, 1.0);
        Assert.True(double.IsFinite(moroInvCum.Evaluate(QL_EPSILON)));
        Assert.True(double.IsFinite(moroInvCum.Evaluate(1.0 - QL_EPSILON)));
        CheckThrowsError(() => moroInvCum.Evaluate(0.0));
        CheckThrowsError(() => moroInvCum.Evaluate(1.0));
        CheckThrowsError(() => moroInvCum.Evaluate(-1.0e-8));
        CheckThrowsError(() => moroInvCum.Evaluate(1.0 + 1.0e-8));

        // Moro switches from its tail approximation to the central approximation
        // at |p - 0.5| = 0.42, corresponding to p = 0.08 and p = 0.92.
        Real[] moroBoundaryProbabilities = [0.08, 0.080001, 0.919999, 0.92];
        foreach (Real probability in moroBoundaryProbabilities)
        {
            Assert.True(double.IsFinite(moroInvCum.Evaluate(probability)));
            // Acklam provides a more accurate independent reference here.
            CheckClose(moroInvCum.Evaluate(probability), invCum.Evaluate(probability), 1.0e-3);
            // Boost.Math provides a second independent reference for the tails.
            CheckClose(moroInvCum.Evaluate(probability), standardNormal.Evaluate(probability), 1.0e-3);
        }

        // The two approximations should not introduce a visible jump at the switch.
        CheckSmall(
            Math.Abs((moroInvCum.Evaluate(0.080001) - moroInvCum.Evaluate(0.08))
                - (invCum.Evaluate(0.080001) - invCum.Evaluate(0.08))),
            1.0e-4); // absolute continuity tolerance
        CheckSmall(
            Math.Abs((moroInvCum.Evaluate(0.92) - moroInvCum.Evaluate(0.919999))
                - (invCum.Evaluate(0.92) - invCum.Evaluate(0.919999))),
            1.0e-4); // absolute continuity tolerance
        CheckThrowsError(() => _ = new MoroInverseCumulativeNormal(0.0, -1.0));
        CheckThrowsError(() => _ = new MoroInverseCumulativeNormal(0.0, double.NaN));
    }

    // Adapted: the two checks against Boost.Math (lines 414 and 418-421, 429 and 431-434) are dropped, with
    // shiftedMaddockInvCum (line 427), which only they read: in nanoql the reference and the class are the same port
    // of Boost's functions, whose oracle is the harness file cxx/math/distributions/normaldistribution, with these
    // nine probabilities at (0, 1) and (3, 2). The round trips, the throws and the refusals are kept.
    [Fact]
    public void TestMaddockNormalDistributions()
    {
        // Testing Maddock normal distributions...

        var maddockInvCum = new MaddockInverseCumulativeNormal();
        var maddockCum = new MaddockCumulativeNormal();
        Real[] probabilities = [1.0e-10, 1.0e-6, 0.01, 0.1, 0.5, 0.9, 0.99, 1.0 - 1.0e-6, 1.0 - 1.0e-10];
        foreach (Real probability in probabilities)
        {
            // Check the CDF/inverse round trip.
            CheckClose(maddockCum.Evaluate(maddockInvCum.Evaluate(probability)), probability, 1.0e-10);
        }

        var shiftedMaddockCum = new MaddockCumulativeNormal(3.0, 2.0);
        foreach (Real probability in probabilities)
        {
            // Check the corresponding transformed CDF round trip.
            CheckClose(shiftedMaddockCum.Evaluate(3.0 + 2.0 * maddockInvCum.Evaluate(probability)), probability, 1.0e-10);
        }

        // Boost.Math throws at exact inverse-CDF endpoints.
        CheckThrowsError(() => maddockInvCum.Evaluate(0.0));
        CheckThrowsError(() => maddockInvCum.Evaluate(1.0));
        CheckClose(maddockCum.Evaluate(0.0), 0.5, 1.0e-12);
        Assert.True(maddockCum.Evaluate(-1.0) < 0.5);
        Assert.True(maddockCum.Evaluate(1.0) > 0.5);
        CheckThrowsError(() => _ = new MaddockInverseCumulativeNormal(0.0, -1.0));
        CheckThrowsError(() => _ = new MaddockCumulativeNormal(0.0, -1.0));
        CheckThrowsError(() => _ = new MaddockInverseCumulativeNormal(0.0, double.NaN));
        CheckThrowsError(() => _ = new MaddockCumulativeNormal(0.0, double.NaN));
    }

    // Adapted: Boost.Math's cdf and quantile, the references (lines 463, 466, 468, 474 and 485), are
    // MaddockCumulativeNormal and MaddockInverseCumulativeNormal, nanoql's ports of them, which the harness vectors
    // check against QuantLib's (cxx/math/distributions/normaldistribution).
    [Fact]
    public void TestCumulativeNormal()
    {
        // Testing cumulative normal distribution tails...

        const Real infinity = double.PositiveInfinity;

        var cumulative = new CumulativeNormalDistribution();
        CheckEqual(cumulative.Evaluate(-infinity), 0.0);
        CheckEqual(cumulative.Evaluate(infinity), 1.0);
        var standardNormal = new MaddockCumulativeNormal(0.0, 1.0);
        var standardNormalQuantile = new MaddockInverseCumulativeNormal(0.0, 1.0);
        Real previous = 0.0;
        // Probe both sides of the direct/asymptotic CDF switch at probability 1e-8.
        Real cdfSwitch = standardNormalQuantile.Evaluate(1.0e-8);
        foreach (Real x in new[] { cdfSwitch - 1.0e-6, cdfSwitch, cdfSwitch + 1.0e-6 })
        {
            CheckSmall(cumulative.Evaluate(x) - standardNormal.Evaluate(x), 1.0e-14);
        }

        foreach (Real x in new[] { -6.0, -10.0, -20.0 })
        {
            // The asymptotic implementation is less accurate than Boost here,
            // but remains within this relative tolerance in the lower tail.
            CheckClose(cumulative.Evaluate(x), standardNormal.Evaluate(x), 1.0e-5);
        }

        foreach (Real x in new[] { -20.0, -10.0, -6.0, -1.0, 0.0, 1.0, 6.0, 10.0, 20.0 })
        {
            Real value = cumulative.Evaluate(x);
            Assert.True(value >= previous);
            Assert.True(value >= 0.0);
            Assert.True(value <= 1.0);
            previous = value;
        }

        foreach (Real x in new[] { 6.0, 10.0, 20.0 })
        {
            CheckSmall(cumulative.Evaluate(x) - standardNormal.Evaluate(x), 1.0e-14);
        }

        var shiftedNormal = new NormalDistribution(3.0, 2.0);
        var shiftedCumulative = new CumulativeNormalDistribution(3.0, 2.0);
        var standardDensity = new NormalDistribution();
        foreach (Real x in new[] { 1.0, 3.0, 5.0 })
        {
            Real standardValue = (x - 3.0) / 2.0;
            CheckClose(shiftedNormal.Evaluate(x), standardDensity.Evaluate(standardValue) / 2.0, 1.0e-12);
            CheckClose(shiftedCumulative.Evaluate(x), cumulative.Evaluate(standardValue), 1.0e-12);
        }

        CheckThrowsError(() => _ = new NormalDistribution(0.0, 0.0));
        CheckThrowsError(() => _ = new NormalDistribution(0.0, -1.0));
        CheckThrowsError(() => _ = new CumulativeNormalDistribution(0.0, 0.0));
        CheckThrowsError(() => _ = new CumulativeNormalDistribution(0.0, -1.0));

        var normal = new NormalDistribution();
        CheckEqual(normal.Derivative(QL_MAX_REAL), 0.0);
        CheckEqual(normal.Derivative(-QL_MAX_REAL), 0.0);
        CheckEqual(normal.Derivative(infinity), 0.0);
        CheckEqual(normal.Derivative(-infinity), 0.0);
    }

    [Fact]
    public void TestBinomialDistribution()
    {
        // Testing binomial distributions...

        CheckClose(BinomialDistributionFunctions.BinomialCoefficientLn(5, 2), Math.Log(10.0), 1.0e-12);
        CheckEqual(BinomialDistributionFunctions.BinomialCoefficient(5, 2), 10.0);
        CheckEqual(BinomialDistributionFunctions.BinomialCoefficient(5, 0), 1.0);
        CheckExpectedErrorMessage(() => BinomialDistributionFunctions.BinomialCoefficientLn(2, 3), "n<k not allowed");

        var pmf = new BinomialDistribution(0.25, 4);
        CheckClose(pmf.Evaluate(0), Math.Pow(0.75, 4), 1.0e-12);
        CheckClose(pmf.Evaluate(2), 6.0 * Math.Pow(0.25, 2) * Math.Pow(0.75, 2), 1.0e-12);
        CheckEqual(pmf.Evaluate(5), 0.0);

        var zeroP = new BinomialDistribution(0.0, 4);
        CheckEqual(zeroP.Evaluate(0), 1.0);
        CheckEqual(zeroP.Evaluate(1), 0.0);
        var oneP = new BinomialDistribution(1.0, 4);
        CheckEqual(oneP.Evaluate(3), 0.0);
        CheckEqual(oneP.Evaluate(4), 1.0);
        CheckExpectedErrorMessage(() => _ = new BinomialDistribution(-0.1, 4), "negative p not allowed");
        CheckExpectedErrorMessage(() => _ = new BinomialDistribution(1.1, 4), "p>1.0 not allowed");

        var cdf = new CumulativeBinomialDistribution(0.25, 4);
        CheckClose(cdf.Evaluate(2), pmf.Evaluate(0) + pmf.Evaluate(1) + pmf.Evaluate(2), 1.0e-12);
        CheckEqual(cdf.Evaluate(4), 1.0);
        var zeroPCdf = new CumulativeBinomialDistribution(0.0, 4);
        CheckEqual(zeroPCdf.Evaluate(0), 1.0);
        var onePCdf = new CumulativeBinomialDistribution(1.0, 4);
        CheckEqual(onePCdf.Evaluate(3), 0.0);
        CheckExpectedErrorMessage(() => _ = new CumulativeBinomialDistribution(-0.1, 4), "negative p not allowed");
        CheckExpectedErrorMessage(() => _ = new CumulativeBinomialDistribution(1.1, 4), "p>1.0 not allowed");

        Real upper = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(1.0, 5);
        Real lower = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(-1.0, 5);
        CheckClose(upper + lower, 1.0, 1.0e-12);
        CheckEqual(BinomialDistributionFunctions.PeizerPrattMethod2Inversion(0.0, 5), 0.5);
        CheckExpectedErrorMessage(
            () => BinomialDistributionFunctions.PeizerPrattMethod2Inversion(0.0, 4), "n must be an odd number");
    }

    [Fact]
    public void TestPoisson()
    {
        // Testing Poisson distribution...

        for (Real mean = 0.0; mean <= 10.0; mean += 0.5)
        {
            long i = 0;
            var pdf = new PoissonDistribution(mean);
            Real calculated = pdf.Evaluate(i);
            Real logHelper = -mean;
            Real expected = Math.Exp(logHelper);
            Real error = Math.Abs(calculated - expected);
            if (error > 1.0e-16)
            {
                Assert.Fail(
                    $"Poisson pdf({mean})({i})\n"
                    + $"    calculated: {calculated:R}\n"
                    + $"    expected:   {expected:R}\n"
                    + $"    error:      {error:R}");
            }

            for (i = 1; i < 25; i++)
            {
                calculated = pdf.Evaluate(i);
                if (mean == 0.0)
                {
                    expected = 0.0;
                }
                else
                {
                    logHelper = logHelper + Math.Log(mean) - Math.Log((Real)i);
                    expected = Math.Exp(logHelper);
                }

                error = Math.Abs(calculated - expected);
                if (error > 1.0e-13)
                {
                    Assert.Fail(
                        $"Poisson pdf({mean})({i})\n"
                        + $"    calculated: {calculated:R}\n"
                        + $"    expected:   {expected:R}\n"
                        + $"    error:      {error:R}");
                }
            }
        }
    }

    [Fact]
    public void TestCumulativePoisson()
    {
        // Testing cumulative Poisson distribution...

        for (Real mean = 0.0; mean <= 10.0; mean += 0.5)
        {
            long i = 0;
            var cdf = new CumulativePoissonDistribution(mean);
            Real cumCalculated = cdf.Evaluate(i);
            Real logHelper = -mean;
            Real cumExpected = Math.Exp(logHelper);
            Real error = Math.Abs(cumCalculated - cumExpected);
            if (error > 1.0e-13)
            {
                Assert.Fail(
                    $"Poisson cdf({mean})({i})\n"
                    + $"    calculated: {cumCalculated:R}\n"
                    + $"    expected:   {cumExpected:R}\n"
                    + $"    error:      {error:R}");
            }

            for (i = 1; i < 25; i++)
            {
                cumCalculated = cdf.Evaluate(i);
                if (mean == 0.0)
                {
                    cumExpected = 1.0;
                }
                else
                {
                    logHelper = logHelper + Math.Log(mean) - Math.Log((Real)i);
                    cumExpected += Math.Exp(logHelper);
                }

                error = Math.Abs(cumCalculated - cumExpected);
                if (error > 1.0e-12)
                {
                    Assert.Fail(
                        $"Poisson cdf({mean})({i})\n"
                        + $"    calculated: {cumCalculated:R}\n"
                        + $"    expected:   {cumExpected:R}\n"
                        + $"    error:      {error:R}");
                }
            }
        }
    }

    [Fact]
    public void TestInverseCumulativePoisson()
    {
        // Testing inverse cumulative Poisson distribution...

        var icp = new InverseCumulativePoisson(1.0);

        Real[] data =
        [
            0.2,
            0.5,
            0.9,
            0.98,
            0.99,
            0.999,
            0.9999,
            0.99995,
            0.99999,
            0.999999,
            0.9999999,
            0.99999999,
        ];

        for (Size i = 0; i < data.Length; i++)
        {
            if (!Close(icp.Evaluate(data[i]), (Real)i))
            {
                Assert.Fail(
                    $"failed to reproduce known value for x = {data[i]}\n"
                    + $"    calculated: {icp.Evaluate(data[i])}\n"
                    + $"    expected:   {(Real)i}");
            }
        }

        CheckEqual(icp.Evaluate(0.0), 0.0);
        CheckEqual(icp.Evaluate(1.0), QL_MAX_REAL);

        var icp4 = new InverseCumulativePoisson(4.0);
        Real previous = -1.0;
        foreach (Real probability in new[] { 0.1, 0.5, 0.9, 0.99 })
        {
            Real calculated = icp4.Evaluate(probability);
            Assert.True(calculated > previous, $"{calculated} is not greater than {previous}");
            previous = calculated;
        }
    }

    [Fact]
    public void TestPoissonInvalidParameters()
    {
        CheckExpectedErrorMessage(() => _ = new PoissonDistribution(-1.0), "mu must be non negative");
        CheckExpectedErrorMessage(() => _ = new PoissonDistribution(double.NaN), "mu must be non negative");
        CheckExpectedErrorMessage(() => _ = new InverseCumulativePoisson(0.0), "lambda must be positive");
        CheckExpectedErrorMessage(() => _ = new InverseCumulativePoisson(-1.0), "lambda must be positive");

        var inverse = new InverseCumulativePoisson();
        CheckExpectedErrorMessage(() => inverse.Evaluate(-0.1), "only defined on the interval [0,1]");
        CheckExpectedErrorMessage(() => inverse.Evaluate(1.1), "only defined on the interval [0,1]");
        CheckExpectedErrorMessage(() => inverse.Evaluate(double.NaN), "only defined on the interval [0,1]");
    }

    [Fact]
    public void TestSankaranApproximation()
    {
        // Testing Sankaran approximation for the non-central cumulative chi-square distribution...

        Real[] dfs = [2, 2, 2, 4, 4];
        Real[] ncps = [1, 2, 3, 1, 2, 3];

        const double tol = 0.01;
        foreach (Real df in dfs)
        {
            foreach (Real ncp in ncps)
            {
                var d = new NonCentralCumulativeChiSquareDistribution(df, ncp);
                var sankaran = new NonCentralCumulativeChiSquareSankaranApprox(df, ncp);

                for (Real x = 0.25; x < 10; x += 0.1)
                {
                    Real expected = d.Evaluate(x);
                    Real calculated = sankaran.Evaluate(x);
                    Real diff = Math.Abs(expected - calculated);

                    if (diff > tol)
                    {
                        Assert.Fail(
                            "Failed to match accuracy of Sankaran approximation"
                            + $"\n    df        : {df}"
                            + $"\n    ncp       : {ncp}"
                            + $"\n    x         : {x}"
                            + $"\n    expected  : {expected}"
                            + $"\n    calculated: {calculated}"
                            + $"\n    diff      : {diff}"
                            + $"\n    tol       : {tol}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestCumulativeNormalTail()
    {
        // Testing error function and cumulative normal in the upper tail...

        // erf(x) rounds to 1 for x >= 6 (erfc(6) is about 2e-17), so the
        // implementation has to saturate to exactly 1 there.  If it stops
        // short of 1, the cumulative normal built on it is no longer
        // monotonic and deep in-the-money option prices fall below their
        // intrinsic value.
        var erf = new ErrorFunction();
        foreach (Real x in new[] { 6.0, 6.5, 7.0, 10.0, 28.0, 100.0 })
        {
            Real y = erf.Evaluate(x);
            if (y != 1.0)
            {
                Assert.Fail($"erf({x}) is below 1 by {1.0 - y}");
            }

            y = erf.Evaluate(-x);
            if (y != -1.0)
            {
                Assert.Fail($"erf({-x}) is above -1 by {y + 1.0}");
            }
        }

        // erf must not decrease when crossing the saturation threshold
        Real previous = erf.Evaluate(5.5);
        for (Size i = 1; i <= 1000; ++i)
        {
            Real x = 5.5 + i * 0.001;
            Real y = erf.Evaluate(x);
            if (y < previous || y > 1.0)
            {
                Assert.Fail($"erf is not monotonic at x = {x}: decreased by {previous - y}");
            }

            previous = y;
        }

        var cum = new CumulativeNormalDistribution();
        foreach (Real z in new[] { 8.5, 9.0, 10.0, 20.0, 40.0 })
        {
            Real p = cum.Evaluate(z);
            if (p != 1.0)
            {
                Assert.Fail($"N({z}) is below 1 by {1.0 - p}");
            }
        }

        previous = cum.Evaluate(7.0);
        for (Size i = 1; i <= 3000; ++i)
        {
            Real z = 7.0 + i * 0.001;
            Real p = cum.Evaluate(z);
            if (p < previous || p > 1.0)
            {
                Assert.Fail($"cumulative normal is not monotonic at z = {z}: decreased by {previous - p}");
            }

            previous = p;
        }
    }

    [Fact]
    public void TestCumulativeChiSquareDistribution()
    {
        var chiSquare = new CumulativeChiSquareDistribution(2.0);
        CheckEqual(chiSquare.Evaluate(-1.0), 0.0);
        CheckEqual(chiSquare.Evaluate(0.0), 0.0);
        CheckClose(chiSquare.Evaluate(2.0), 1.0 - Math.Exp(-1.0), 1.0e-5);

        var nonCentral = new NonCentralCumulativeChiSquareDistribution(4.0, 1.0);
        CheckEqual(nonCentral.Evaluate(0.0), 0.0);
        CheckEqual(nonCentral.Evaluate(-1.0), 0.0);
    }

    // Upstream: QuantLib's master's case for #2908 (the S-van plan's VR11). Adapted: Boost's
    // non_central_chi_squared_distribution is nanoql's internal port, bound through reflection.
    [Fact]
    public void TestNonCentralChiSquareLargeArguments()
    {
        // Testing the non-central cumulative chi-square distribution for large non-centrality and large arguments...

        // for ncp/2 or x/2 above about 708, exp(-ncp/2) or the first
        // term of the series underflow; the distribution used to return
        // 0 instead of the actual value (up to 1).
        var boostCdf = Vectors.BoostInternals.Function<Func<double, double, double, double>>(
            "NonCentralChiSquaredFunctions", "Cdf");
        Real[] dfs = [0.5, 1.0, 4.8, 20.0];
        Real[] ncps = [0.0, 1.0, 10.0, 1400.0, 1600.0, 5000.0, 20000.0];
        Real[] ks = [-8.0, -3.0, -1.0, 0.0, 1.0, 3.0, 8.0, 50.0];

        const Real tol = 1e-10;
        foreach (Real df in dfs)
        {
            foreach (Real ncp in ncps)
            {
                var d = new NonCentralCumulativeChiSquareDistribution(df, ncp);

                Real mean = df + ncp;
                Real stdDev = Math.Sqrt(2.0 * (df + 2.0 * ncp));
                List<Real> xs = [1500.0, 2000.0, 1.0e4, 1.0e6, 1.0e12];
                foreach (Real k in ks)
                {
                    if (mean + k * stdDev > 0.0)
                    {
                        xs.Add(mean + k * stdDev);
                    }
                }

                foreach (Real x in xs)
                {
                    Real expected = boostCdf(df, ncp, x);
                    Real calculated = d.Evaluate(x);
                    Real diff = Math.Abs(expected - calculated);

                    Assert.False(
                        diff > tol,
                        "Failed to reproduce non-central chi-square cdf"
                        + $"\n    df        : {df}"
                        + $"\n    ncp       : {ncp}"
                        + $"\n    x         : {x}"
                        + $"\n    expected  : {expected}"
                        + $"\n    calculated: {calculated}"
                        + $"\n    diff      : {diff}"
                        + $"\n    tol       : {tol}");
                }
            }
        }
    }

    [Fact]
    public void TestBivariate()
    {
        // Testing bivariate cumulative normal distribution...
        static Func<Real, Real, Real> Dr78(Real rho) => new BivariateCumulativeNormalDistributionDr78(rho).Evaluate;
        static Func<Real, Real, Real> We04DP(Real rho) => new BivariateCumulativeNormalDistributionWe04DP(rho).Evaluate;

        CheckBivariateAtZero(Dr78, "Drezner 1978", 1.0e-6);
        CheckBivariate(Dr78, "Drezner 1978");

        // due to relative low accuracy of Dr78, it does not pass with a
        // smaller perturbation
        CheckBivariateTail(Dr78, "Drezner 1978", 1.0e-5);

        CheckBivariateAtZero(We04DP, "West 2004", 1.0e-15);
        CheckBivariate(We04DP, "West 2004");

        CheckBivariateTail(We04DP, "West 2004", 1.0e-6);
        CheckBivariateTail(We04DP, "West 2004", 1.0e-8);
    }

    private static void CheckBivariate(Func<Real, Func<Real, Real, Real>> bivariate, string tag)
    {
        // The data below are from
        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
        // pag 193
        (Real A, Real B, Real Rho, Real Result)[] values =
        [
            (0.0, 0.0, 0.0, 0.250000),
            (0.0, 0.0, -0.5, 0.166667),
            (0.0, 0.0, 0.5, 1.0 / 3),
            (0.0, -0.5, 0.0, 0.154269),
            (0.0, -0.5, -0.5, 0.081660),
            (0.0, -0.5, 0.5, 0.226878),
            (0.0, 0.5, 0.0, 0.345731),
            (0.0, 0.5, -0.5, 0.273122),
            (0.0, 0.5, 0.5, 0.418340),

            (-0.5, 0.0, 0.0, 0.154269),
            (-0.5, 0.0, -0.5, 0.081660),
            (-0.5, 0.0, 0.5, 0.226878),
            (-0.5, -0.5, 0.0, 0.095195),
            (-0.5, -0.5, -0.5, 0.036298),
            (-0.5, -0.5, 0.5, 0.163319),
            (-0.5, 0.5, 0.0, 0.213342),
            (-0.5, 0.5, -0.5, 0.145218),
            (-0.5, 0.5, 0.5, 0.272239),

            (0.5, 0.0, 0.0, 0.345731),
            (0.5, 0.0, -0.5, 0.273122),
            (0.5, 0.0, 0.5, 0.418340),
            (0.5, -0.5, 0.0, 0.213342),
            (0.5, -0.5, -0.5, 0.145218),
            (0.5, -0.5, 0.5, 0.272239),
            (0.5, 0.5, 0.0, 0.478120),
            (0.5, 0.5, -0.5, 0.419223),
            (0.5, 0.5, 0.5, 0.546244),

            // known analytical values
            (0.0, 0.0, Math.Sqrt(1 / 2.0), 3.0 / 8),

            // {  0.0,  big,  any, 0.500000 },
            (0.0, 30, -1.0, 0.500000),
            (0.0, 30, 0.0, 0.500000),
            (0.0, 30, 1.0, 0.500000),

            // { big,  big,   any, 1.000000 },
            (30, 30, -1.0, 1.000000),
            (30, 30, 0.0, 1.000000),
            (30, 30, 1.0, 1.000000),

            // {-big,  any,   any, 0.000000 }
            (-30, -1.0, -1.0, 0.000000),
            (-30, 0.0, -1.0, 0.000000),
            (-30, 1.0, -1.0, 0.000000),
            (-30, -1.0, 0.0, 0.000000),
            (-30, 0.0, 0.0, 0.000000),
            (-30, 1.0, 0.0, 0.000000),
            (-30, -1.0, 1.0, 0.000000),
            (-30, 0.0, 1.0, 0.000000),
            (-30, 1.0, 1.0, 0.000000),
        ];

        for (int i = 0; i < values.Length; i++)
        {
            Func<Real, Real, Real> bcd = bivariate(values[i].Rho);
            Real value = bcd(values[i].A, values[i].B);

            Real tolerance = 1.0e-6;
            Assert.True(
                Math.Abs(value - values[i].Result) < tolerance,
                $"{tag} bivariate cumulative distribution\n    case: {i + 1}\n    a:    {values[i].A}\n"
                + $"    b:    {values[i].B}\n    rho:  {values[i].Rho}\n    tabulated value:  {values[i].Result}\n"
                + $"    result:           {value}");
        }
    }

    private static void CheckBivariateAtZero(Func<Real, Func<Real, Real, Real>> bivariate, string tag, Real tolerance)
    {
        // BVN(0.0,0.0,rho) = 1/4 + arcsin(rho)/(2*M_PI)
        // "Handbook of the Normal Distribution",
        // J.K. Patel & C.B.Read, 2nd Ed, 1996
        Real[] rho = [0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.99999];
        const Real x = 0.0;
        const Real y = 0.0;

        foreach (Real i in rho)
        {
            for (int sgn = -1; sgn < 2; sgn += 2)
            {
                Func<Real, Real, Real> bvn = bivariate(sgn * i);
                Real expected = 0.25 + (Math.Asin(sgn * i) / (2 * M_PI));
                Real realised = bvn(x, y);

                Assert.True(
                    Math.Abs(realised - expected) < tolerance,
                    $"{tag} bivariate cumulative distribution\n    rho: {sgn * i}\n    expected:  {expected}\n"
                    + $"    realised:  {realised}\n    tolerance: {tolerance}");
            }
        }
    }

    private static void CheckBivariateTail(Func<Real, Func<Real, Real, Real>> bivariate, string tag, Real tolerance)
    {
        // make sure numerical greeks are sensible, numerical error in
        // the tails can make garbage greeks for partial time barrier
        // option
        Real x = -6.9;
        Real y = 6.9;
        Real corr = -0.999;
        Func<Real, Real, Real> bvn = bivariate(corr);
        for (int i = 0; i < 10; i++)
        {
            Real cdf0 = bvn(x, y);
            y += tolerance;
            Real cdf1 = bvn(x, y);
            Assert.False(
                cdf0 > cdf1,
                $"{tag} cdf must be decreasing in the tails\n    cdf0: {cdf0}\n    cdf1: {cdf1}\n    x: {x}\n"
                + $"    y: {y}\n    rho: {corr}");
        }
    }

    private static Real Gaussian(Real x)
    {
        Real normFact = sigma * Math.Sqrt(2 * M_PI);
        Real dx = x - average;
        return Math.Exp(-dx * dx / (2.0 * sigma * sigma)) / normFact;
    }

    private static Real GaussianDerivative(Real x)
    {
        Real normFact = sigma * sigma * sigma * Math.Sqrt(2 * M_PI);
        Real dx = x - average;
        return -dx * Math.Exp(-dx * dx / (2.0 * sigma * sigma)) / normFact;
    }

    // The test suite's norm: the square root of the trapezoidal integral of f², at spacing h.
    private static Real Norm(Real[] f, Real h)
    {
        // squared values
        var f2 = new Real[f.Length];
        for (int i = 0; i < f.Length; i++)
        {
            f2[i] = f[i] * f[i];
        }

        // numeric integral of f^2
        Real sum = 0.0;
        foreach (Real v in f2)
        {
            sum += v;
        }

        Real I = h * (sum - 0.5 * f2[0] - 0.5 * f2[^1]);
        return Math.Sqrt(I);
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

    // BOOST_CHECK_SMALL(x, tolerance): |x| <= tolerance.
    private static void CheckSmall(Real x, Real tolerance) =>
        Assert.True(Math.Abs(x) <= tolerance, $"|{x:R}| exceeds {tolerance}");

    // BOOST_CHECK_EQUAL on reals: exact equality.
    private static void CheckEqual(Real actual, Real expected) =>
        Assert.True(actual == expected, $"{actual:R} != {expected:R}");

    // BOOST_CHECK_THROW(..., Error) and (..., std::exception): QuantLib's Error, and Boost.Math's std::exception, are an
    // ArgumentException or a NanoQLException in nanoql.
    private static void CheckThrowsError(Action action)
    {
        Exception? e = Record.Exception(action);
        Assert.True(
            e is ArgumentException or NanoQLException,
            $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
    }

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
