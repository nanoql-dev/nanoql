// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2013 Fabien Le Floc'h
// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/distributions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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
    public void TestInverseCumulativeStudent()
    {
        // Testing inverse cumulative Student t distribution...

        Real[] probabilities =
        [
            1.0e-12, 1.0e-9, 1.0e-6, 1.0e-4,
            0.001, 0.01, 0.10, 0.25, 0.50, 0.75,
            0.90, 0.99, 0.999, 1.0 - 1.0e-4,
            1.0 - 1.0e-6, 1.0 - 1.0e-9,
            1.0 - 1.0e-12,
        ];

        var inverseCauchy = new InverseCumulativeStudent(1);
        foreach (Real p in probabilities)
        {
            Real calculated = inverseCauchy.Evaluate(p);
            Real expected;
            if (p < 0.5)
            {
                expected = -1.0 / Math.Tan(M_PI * p);
            }
            else if (p > 0.5)
            {
                expected = 1.0 / Math.Tan(M_PI * (1.0 - p));
            }
            else
            {
                expected = 0.0;
            }

            Real error = Math.Abs(calculated - expected);
            Real tolerance = 1.0e-10 * Math.Max(1.0, Math.Abs(expected));
            if (error > tolerance)
            {
                Assert.Fail(
                    "Failed to reproduce inverse cumulative Cauchy value:"
                    + $"\n    probability: {p}"
                    + $"\n    calculated:  {calculated}"
                    + $"\n    expected:    {expected}"
                    + $"\n    error:       {error}");
            }
        }

        var inverseStudent2 = new InverseCumulativeStudent(2);
        foreach (Real p in probabilities)
        {
            Real calculated = inverseStudent2.Evaluate(p);
            Real expected = (2.0 * p - 1.0) / Math.Sqrt(2.0 * p * (1.0 - p));
            Real error = Math.Abs(calculated - expected);
            Real tolerance = 1.0e-12 * Math.Max(1.0, Math.Abs(expected));
            if (error > tolerance)
            {
                Assert.Fail(
                    "Failed to reproduce inverse cumulative Student t value for 2 degrees of freedom:"
                    + $"\n    probability: {p}"
                    + $"\n    calculated:  {calculated}"
                    + $"\n    expected:    {expected}"
                    + $"\n    error:       {error}");
            }
        }

        var inverse = new InverseCumulativeStudent(4);
        if (inverse.Evaluate(0.0) != QL_MIN_REAL)
        {
            Assert.Fail("Inverse cumulative Student t distribution at 0.0 must return QL_MIN_REAL");
        }

        if (inverse.Evaluate(1.0) != QL_MAX_REAL)
        {
            Assert.Fail("Inverse cumulative Student t distribution at 1.0 must return QL_MAX_REAL");
        }

        CheckThrowsError(() => inverse.Evaluate(-0.1));
        CheckThrowsError(() => inverse.Evaluate(1.1));

        int[] ns = [3, 4, 5, 10, 30, 100];
        Real[] roundTripProbabilities =
        [
            1.0e-10, 1.0e-8, 1.0e-6,
            1.0e-4, 0.001, 0.01, 0.10,
            0.25, 0.50, 0.75, 0.90, 0.99,
            0.999, 1.0 - 1.0e-4,
            1.0 - 1.0e-6, 1.0 - 1.0e-8,
            1.0 - 1.0e-10,
        ];
        Real[] symmetryProbabilities = [1.0e-8, 1.0e-6, 1.0e-4, 0.001, 0.01, 0.10, 0.25];

        foreach (int n in ns)
        {
            var inv = new InverseCumulativeStudent(n);
            var cdf = new CumulativeStudentDistribution(n);

            Real previous = QL_MIN_REAL;
            foreach (Real p in roundTripProbabilities)
            {
                Real x = inv.Evaluate(p);
                if (x <= previous)
                {
                    Assert.Fail(
                        "Inverse cumulative Student t distribution is not increasing:"
                        + $"\n    n:            {n}"
                        + $"\n    probability:  {p}"
                        + $"\n    value:        {x}"
                        + $"\n    previous:     {previous}");
                }

                previous = x;

                Real calculated = cdf.Evaluate(x);
                Real error = Math.Abs(calculated - p);
                Real tolerance = 1.0e-8;
                if (error > tolerance)
                {
                    Assert.Fail(
                        "Failed to reproduce Student t distribution round-trip:"
                        + $"\n    n:            {n}"
                        + $"\n    probability:  {p}"
                        + $"\n    inverse:      {x}"
                        + $"\n    calculated:   {calculated}"
                        + $"\n    error:        {error}");
                }
            }

            foreach (Real p in symmetryProbabilities)
            {
                Real lower = inv.Evaluate(p);
                Real upper = inv.Evaluate(1.0 - p);
                Real error = Math.Abs(lower + upper);
                Real tolerance = 1.0e-8 * Math.Max(Math.Max(1.0, Math.Abs(lower)), Math.Abs(upper));
                if (error > tolerance)
                {
                    Assert.Fail(
                        "Inverse cumulative Student t distribution symmetry failure:"
                        + $"\n    n:            {n}"
                        + $"\n    probability:  {p}"
                        + $"\n    lower:        {lower}"
                        + $"\n    upper:        {upper}"
                        + $"\n    error:        {error}");
                }
            }
        }
    }

    [Fact]
    public void TestStudentDistribution()
    {
        var cauchy = new StudentDistribution(1);
        CheckClose(cauchy.Evaluate(0.0), 1.0 / M_PI, 1.0e-10);
        CheckClose(cauchy.Evaluate(1.0), 0.5 / M_PI, 1.0e-10);

        var student = new StudentDistribution(5);
        CheckClose(student.Evaluate(-1.0), student.Evaluate(1.0), 1.0e-12);

        var cumulative = new CumulativeStudentDistribution(5);
        CheckClose(cumulative.Evaluate(0.0), 0.5, 1.0e-12);
        CheckClose(cumulative.Evaluate(-1.0), 1.0 - cumulative.Evaluate(1.0), 1.0e-12);

        CheckExpectedErrorMessage(() => _ = new StudentDistribution(0), "invalid parameter for t-distribution");
        CheckExpectedErrorMessage(() => _ = new CumulativeStudentDistribution(0), "invalid parameter for t-distribution");
    }

    [Fact]
    public void TestBivariateCumulativeStudent()
    {
        // Testing bivariate cumulative Student t distribution...

        Real[] xs = [0.00, 0.50, 1.00, 1.50, 2.00, 2.50, 3.00, 4.00, 5.00, 6.00, 7.00, 8.00, 9.00, 10.00];
        int[] ns = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25, 30, 60, 90, 120, 150, 300, 600];

        // Part of table 1 from the reference paper
        Real[] expected1 =
        [
            0.33333, 0.50000, 0.63497, 0.72338, 0.78063, 0.81943, 0.84704, 0.88332, 0.90590, 0.92124, 0.93231, 0.94066, 0.94719, 0.95243,
            0.33333, 0.52017, 0.68114, 0.78925, 0.85607, 0.89754, 0.92417, 0.95433, 0.96978, 0.97862, 0.98411, 0.98774, 0.99026, 0.99208,
            0.33333, 0.52818, 0.70018, 0.81702, 0.88720, 0.92812, 0.95238, 0.97667, 0.98712, 0.99222, 0.99497, 0.99657, 0.99756, 0.99821,
            0.33333, 0.53245, 0.71052, 0.83231, 0.90402, 0.94394, 0.96612, 0.98616, 0.99353, 0.99664, 0.99810, 0.99885, 0.99927, 0.99951,
            0.33333, 0.53510, 0.71701, 0.84196, 0.91449, 0.95344, 0.97397, 0.99095, 0.99637, 0.99836, 0.99918, 0.99956, 0.99975, 0.99985,
            0.33333, 0.53689, 0.72146, 0.84862, 0.92163, 0.95972, 0.97893, 0.99365, 0.99779, 0.99913, 0.99962, 0.99982, 0.99990, 0.99995,
            0.33333, 0.53819, 0.72470, 0.85348, 0.92679, 0.96415, 0.98230, 0.99531, 0.99857, 0.99950, 0.99981, 0.99992, 0.99996, 0.99998,
            0.33333, 0.53917, 0.72716, 0.85719, 0.93070, 0.96743, 0.98470, 0.99639, 0.99903, 0.99970, 0.99990, 0.99996, 0.99998, 0.99999,
            0.33333, 0.53994, 0.72909, 0.86011, 0.93375, 0.96995, 0.98650, 0.99713, 0.99931, 0.99981, 0.99994, 0.99998, 0.99999, 1.00000,
            0.33333, 0.54056, 0.73065, 0.86247, 0.93621, 0.97194, 0.98788, 0.99766, 0.99950, 0.99988, 0.99996, 0.99999, 1.00000, 1.00000,
            0.33333, 0.54243, 0.73540, 0.86968, 0.94362, 0.97774, 0.99168, 0.99890, 0.99985, 0.99998, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54338, 0.73781, 0.87336, 0.94735, 0.98053, 0.99337, 0.99932, 0.99993, 0.99999, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54395, 0.73927, 0.87560, 0.94959, 0.98216, 0.99430, 0.99952, 0.99996, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54433, 0.74025, 0.87709, 0.95108, 0.98322, 0.99489, 0.99963, 0.99998, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54528, 0.74271, 0.88087, 0.95482, 0.98580, 0.99623, 0.99983, 0.99999, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54560, 0.74354, 0.88215, 0.95607, 0.98663, 0.99664, 0.99987, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54576, 0.74396, 0.88279, 0.95669, 0.98704, 0.99683, 0.99989, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54586, 0.74420, 0.88317, 0.95706, 0.98729, 0.99695, 0.99990, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54605, 0.74470, 0.88394, 0.95781, 0.98777, 0.99717, 0.99992, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.33333, 0.54615, 0.74495, 0.88432, 0.95818, 0.98801, 0.99728, 0.99993, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
        ];

        // Part of table 2 from the reference paper
        Real[] expected2 =
        [
            0.16667, 0.36554, 0.54022, 0.65333, 0.72582, 0.77465, 0.80928, 0.85466, 0.88284, 0.90196, 0.91575, 0.92616, 0.93429, 0.94081,
            0.16667, 0.38889, 0.59968, 0.73892, 0.82320, 0.87479, 0.90763, 0.94458, 0.96339, 0.97412, 0.98078, 0.98518, 0.98823, 0.99044,
            0.16667, 0.39817, 0.62478, 0.77566, 0.86365, 0.91391, 0.94330, 0.97241, 0.98483, 0.99086, 0.99410, 0.99598, 0.99714, 0.99790,
            0.16667, 0.40313, 0.63863, 0.79605, 0.88547, 0.93396, 0.96043, 0.98400, 0.99256, 0.99614, 0.99782, 0.99868, 0.99916, 0.99944,
            0.16667, 0.40620, 0.64740, 0.80900, 0.89902, 0.94588, 0.97007, 0.98972, 0.99591, 0.99816, 0.99909, 0.99951, 0.99972, 0.99983,
            0.16667, 0.40829, 0.65345, 0.81794, 0.90820, 0.95368, 0.97607, 0.99290, 0.99755, 0.99904, 0.99958, 0.99980, 0.99989, 0.99994,
            0.16667, 0.40980, 0.65788, 0.82449, 0.91482, 0.95914, 0.98010, 0.99482, 0.99844, 0.99946, 0.99979, 0.99991, 0.99996, 0.99998,
            0.16667, 0.41095, 0.66126, 0.82948, 0.91981, 0.96314, 0.98295, 0.99605, 0.99895, 0.99968, 0.99989, 0.99996, 0.99998, 0.99999,
            0.16667, 0.41185, 0.66393, 0.83342, 0.92369, 0.96619, 0.98506, 0.99689, 0.99926, 0.99980, 0.99994, 0.99998, 0.99999, 1.00000,
            0.16667, 0.41257, 0.66608, 0.83661, 0.92681, 0.96859, 0.98667, 0.99748, 0.99946, 0.99987, 0.99996, 0.99999, 1.00000, 1.00000,
            0.16667, 0.41476, 0.67268, 0.84633, 0.93614, 0.97550, 0.99103, 0.99884, 0.99984, 0.99998, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41586, 0.67605, 0.85129, 0.94078, 0.97877, 0.99292, 0.99930, 0.99993, 0.99999, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41653, 0.67810, 0.85430, 0.94356, 0.98066, 0.99396, 0.99950, 0.99996, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41698, 0.67947, 0.85632, 0.94540, 0.98189, 0.99461, 0.99962, 0.99998, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41810, 0.68294, 0.86141, 0.94998, 0.98483, 0.99607, 0.99982, 0.99999, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41847, 0.68411, 0.86312, 0.95149, 0.98577, 0.99651, 0.99987, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41866, 0.68470, 0.86398, 0.95225, 0.98623, 0.99672, 0.99989, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41877, 0.68505, 0.86449, 0.95270, 0.98650, 0.99684, 0.99990, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41900, 0.68576, 0.86552, 0.95360, 0.98705, 0.99707, 0.99992, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
            0.16667, 0.41911, 0.68612, 0.86604, 0.95405, 0.98731, 0.99719, 0.99993, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000, 1.00000,
        ];

        Real tolerance = 1.0e-5;
        for (int i = 0; i < ns.Length; ++i)
        {
            var f1 = new BivariateCumulativeStudentDistribution(ns[i], 0.5);
            var f2 = new BivariateCumulativeStudentDistribution(ns[i], -0.5);
            for (int j = 0; j < xs.Length; ++j)
            {
                Real calculated1 = f1.Evaluate(xs[j], xs[j]);
                Real reference1 = expected1[(i * xs.Length) + j];
                Real calculated2 = f2.Evaluate(xs[j], xs[j]);
                Real reference2 = expected2[(i * xs.Length) + j];
                if (Math.Abs(calculated1 - reference1) > tolerance)
                {
                    Assert.Fail(
                        $"Failed to reproduce CDF value at {xs[j]}"
                        + $"\n    calculated: {calculated1}"
                        + $"\n    expected:   {reference1}");
                }

                if (Math.Abs(calculated2 - reference2) > tolerance)
                {
                    Assert.Fail(
                        $"Failed to reproduce CDF value at {xs[j]}"
                        + $"\n    calculated: {calculated2}"
                        + $"\n    expected:   {reference1}");
                }
            }
        }

        // a few more random cases
        (int N, Real Rho, Real X, Real Y, Real Result)[] cases =
        [
            (2, -1.0, 5.0, 8.0, 0.973491),
            (2, 1.0, -2.0, 8.0, 0.091752),
            (2, 1.0, 5.25, -9.5, 0.005450),
            (3, -0.5, -5.0, -5.0, 0.000220),
            (4, -1.0, -8.0, 7.5, 0.0),
            (4, 0.5, -5.5, 10.0, 0.002655),
            (4, 1.0, -5.0, 6.0, 0.003745),
            (4, 1.0, 6.0, 5.5, 0.997336),
            (5, -0.5, -7.0, -6.25, 0.000004),
            (5, -0.5, 3.75, -7.25, 0.000166),
            (5, -0.5, 7.75, -1.25, 0.133073),
            (6, 0.0, 7.5, 3.25, 0.991149),
            (7, -0.5, -1.0, -8.5, 0.000001),
            (7, -1.0, -4.25, -4.0, 0.0),
            (7, 0.0, 0.5, -2.25, 0.018819),
            (8, -1.0, 8.25, 1.75, 0.940866),
            (8, 0.0, 2.25, 4.75, 0.972105),
            (9, -0.5, -4.0, 8.25, 0.001550),
            (9, -1.0, -1.25, -8.75, 0.0),
            (9, -1.0, 5.75, -6.0, 0.0),
            (9, 0.5, -6.5, -9.5, 0.000001),
            (9, 1.0, -2.0, 9.25, 0.038276),
            (10, -1.0, -0.5, 6.0, 0.313881),
            (10, 0.5, 0.0, 9.25, 0.5),
            (10, 0.5, 6.75, -2.25, 0.024090),
            (10, 1.0, -1.75, -1.0, 0.055341),
            (15, 0.0, -1.25, -4.75, 0.000029),
            (15, 0.0, -2.0, -1.5, 0.003411),
            (15, 0.5, 3.0, -3.25, 0.002691),
            (20, -0.5, 2.0, -1.25, 0.098333),
            (20, -1.0, 3.0, 8.0, 0.996462),
            (20, 0.0, -7.5, 1.5, 0.0),
            (20, 0.5, 1.25, 9.75, 0.887136),
            (25, -1.0, -4.25, 5.0, 0.000111),
            (25, 0.5, 9.5, -1.5, 0.073069),
            (25, 1.0, -6.5, -3.25, 0.0),
            (30, -1.0, -7.75, 10.0, 0.0),
            (30, 1.0, 0.5, 9.5, 0.689638),
            (60, -1.0, -3.5, -8.25, 0.0),
            (60, -1.0, 4.25, 0.75, 0.771869),
            (60, -1.0, 5.75, 3.75, 0.9998),
            (60, 0.5, -4.5, 8.25, 0.000016),
            (60, 1.0, 6.5, -4.0, 0.000088),
            (90, -0.5, -3.75, -2.75, 0.0),
            (90, 0.5, 8.75, -7.0, 0.0),
            (120, 0.0, -3.5, -9.25, 0.0),
            (120, 0.0, -8.25, 5.0, 0.0),
            (120, 1.0, -0.75, 3.75, 0.227361),
            (120, 1.0, -3.5, -8.0, 0.0),
            (150, 0.0, 10.0, -1.75, 0.041082),
            (300, -0.5, -6.0, 3.75, 0.0),
            (300, -0.5, 3.5, -4.5, 0.000004),
            (300, 0.0, 6.5, -5.0, 0.0),
            (600, -0.5, 9.25, 1.5, 0.93293),
            (600, -1.0, -9.25, 1.5, 0.0),
            (600, 0.5, -5.0, 8.0, 0.0),
            (600, 1.0, -2.75, -9.0, 0.0),
            (1000, -0.5, -2.5, 0.25, 0.000589),
            (1000, -0.5, 3.0, 1.0, 0.839842),
            (2000, -1.0, 9.0, -4.75, 0.000001),
            (2000, 0.5, 9.75, 7.25, 1.0),
            (2000, 1.0, 0.75, -9.0, 0.0),
            (5000, -0.5, 9.75, 5.5, 1.0),
            (5000, -1.0, 6.0, 1.0, 0.841321),
            (5000, 1.0, 4.0, -7.75, 0.0),
            (10000, 0.5, 1.5, 6.0, 0.933177),
        ];

        tolerance = 1.0e-6;
        foreach ((int N, Real Rho, Real X, Real Y, Real Result) i in cases)
        {
            var f = new BivariateCumulativeStudentDistribution(i.N, i.Rho);
            Real calculated = f.Evaluate(i.X, i.Y);
            Real expected = i.Result;
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail(
                    "Failed to reproduce CDF value:"
                    + $"\n    n:   {i.N}\n    rho: {i.Rho}\n    x:   {i.X}"
                    + $"\n    y:   {i.Y}\n    calculated: {calculated}"
                    + $"\n    expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestBivariateCumulativeStudentInvalidParameters()
    {
        CheckExpectedErrorMessage(
            () => _ = new BivariateCumulativeStudentDistribution(0, 0.0), "degrees of freedom must be positive");
        CheckExpectedErrorMessage(() => _ = new BivariateCumulativeStudentDistribution(1, -1.1), "rho must be in [-1, 1]");
        CheckExpectedErrorMessage(() => _ = new BivariateCumulativeStudentDistribution(1, 1.1), "rho must be in [-1, 1]");
        CheckExpectedErrorMessage(
            () => _ = new BivariateCumulativeStudentDistribution(1, double.NaN), "rho must be in [-1, 1]");
        CheckExpectedErrorMessage(
            () => _ = new BivariateCumulativeStudentDistribution(1, double.PositiveInfinity), "rho must be in [-1, 1]");
        CheckExpectedErrorMessage(
            () => _ = new BivariateCumulativeStudentDistribution(1, double.NegativeInfinity), "rho must be in [-1, 1]");
    }

    [Fact]
    public void TestBivariateCumulativeStudentBoundaryContinuity()
    {
        int n = 5;
        var univariate = new CumulativeStudentDistribution(n);
        var perfectlyPositive = new BivariateCumulativeStudentDistribution(n, 1.0);
        var perfectlyNegative = new BivariateCumulativeStudentDistribution(n, -1.0);
        var symmetric = new BivariateCumulativeStudentDistribution(n, 0.5);
        Real[] points = [-2.0, -0.25, 0.0, 1.5];
        Real tolerance = 1.0e-10;

        foreach (Real x in points)
        {
            foreach (Real y in points)
            {
                Real positiveExpected = univariate.Evaluate(Math.Min(x, y));
                CheckSmall(perfectlyPositive.Evaluate(x, y) - positiveExpected, tolerance);

                Real negativeExpected = x < -y ? 0.0 : univariate.Evaluate(x) - univariate.Evaluate(-y);
                CheckSmall(perfectlyNegative.Evaluate(x, y) - negativeExpected, tolerance);
            }
        }

        CheckSmall(symmetric.Evaluate(0.75, -1.25) - symmetric.Evaluate(-1.25, 0.75), tolerance);

        Real epsilon = 1.0e-7;
        CheckSmall(perfectlyPositive.Evaluate(1.0, 1.0 + epsilon) - perfectlyPositive.Evaluate(1.0, 1.0 - epsilon), 1.0e-6);
        CheckSmall(
            perfectlyNegative.Evaluate(1.0, -1.0 + epsilon) - perfectlyNegative.Evaluate(1.0, -1.0 + (2.0 * epsilon)), 1.0e-6);
        CheckSmall(
            perfectlyNegative.Evaluate(1.0, -1.0 - epsilon) - perfectlyNegative.Evaluate(1.0, -1.0 - (2.0 * epsilon)), 1.0e-6);
    }

    [Fact]
    public void TestBivariateCumulativeStudentVsBivariate()
    {
        // Testing bivariate cumulative Student t distribution for large N...

        int n = 10000; // for this value, the distribution should be
                       // close to a bivariate normal distribution.

        for (Real rho = -1.0; rho < 1.01; rho += 0.25)
        {
            var t = new BivariateCumulativeStudentDistribution(n, rho);
            var normal = new BivariateCumulativeNormalDistributionWe04DP(rho);

            Real avgDiff = 0.0;
            int m = 0;
            Real tolerance = 4.0e-5;
            for (Real x = -10; x < 10.1; x += 0.5)
            {
                for (Real y = -10; y < 10.1; y += 0.5)
                {
                    Real calculated = t.Evaluate(x, y);
                    Real expected = normal.Evaluate(x, y);
                    Real diff = Math.Abs(calculated - expected);
                    if (diff > tolerance)
                    {
                        Assert.Fail(
                            "Failed to reproduce limit value:"
                            + $"\n    rho: {rho}"
                            + $"\n    x:   {x}"
                            + $"\n    y:   {y}"
                            + $"\n    calculated: {calculated}"
                            + $"\n    expected:   {expected}");
                    }

                    avgDiff += diff;
                    ++m;
                }
            }

            avgDiff /= m;
            if (avgDiff > 3.0e-6)
            {
                Assert.Fail(
                    "Failed to reproduce average limit value:"
                    + $"\n    rho: {rho}"
                    + $"\n    average error: {avgDiff}");
            }
        }
    }

    [Fact]
    public void TestInvCDFviaStochasticCollocation()
    {
        // Testing inverse CDF based on stochastic collocation...

        const Real k = 3.0;
        const Real lambda = 1.0;

        var normalCDF = new CumulativeNormalDistribution();
        Func<Real, Real> invCDF = InverseNonCentralChiSquared(k, lambda);

        var scInvCDF10 = new StochasticCollocationInvCDF(invCDF, 10);

        // low precision
        for (Real x = -3.0; x < 3.0; x += 0.1)
        {
            Real u = normalCDF.Evaluate(x);

            Real calculated1 = scInvCDF10.Evaluate(u);
            Real calculated2 = scInvCDF10.Value(x);
            Real expected = invCDF(u);

            if (Math.Abs(calculated1 - calculated2) > 1e-6)
            {
                Assert.Fail(
                    "Failed to reproduce equal stochastic collocation inverse CDF"
                    + $"\n    x: {x}"
                    + $"\n    calculated via normal distribution : {calculated2}"
                    + $"\n    calculated via uniform distribution: {calculated1}"
                    + $"\n    diff: {calculated1 - calculated2}");
            }

            const Real tol = 1e-2;
            if (Math.Abs(calculated2 - expected) > tol)
            {
                Assert.Fail(
                    "Failed to reproduce invCDF with stochastic collocation method"
                    + $"\n    x: {x}"
                    + $"\n    invCDF  :{expected}"
                    + $"\n    scInvCDF: {calculated2}"
                    + $"\n    diff    : {Math.Abs(expected - calculated2)}"
                    + $"\n    tol     : {tol}");
            }
        }

        // high precision
        var scInvCDF30 = new StochasticCollocationInvCDF(invCDF, 30, 0.9999999);
        for (Real x = -4.0; x < 4.0; x += 0.1)
        {
            Real u = normalCDF.Evaluate(x);

            Real expected = invCDF(u);
            Real calculated = scInvCDF30.Evaluate(u);

            const Real tol = 1e-6;
            if (Math.Abs(calculated - expected) > tol)
            {
                Assert.Fail(
                    "Failed to reproduce invCDF with stochastic collocation method"
                    + $"\n    x: {x}"
                    + $"\n    invCDF  :{expected}"
                    + $"\n    scInvCDF: {calculated}"
                    + $"\n    diff    : {Math.Abs(expected - calculated)}"
                    + $"\n    tol     : {tol}");
            }
        }
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

    // The test's InverseNonCentralChiSquared: Boost's non-central chi-squared quantile, which nanoql keeps internal.
    private static Func<Real, Real> InverseNonCentralChiSquared(Real df, Real ncp)
    {
        var quantile = Vectors.BoostInternals.Function<Func<double, double, double, double>>(
            "NonCentralChiSquaredFunctions", "Quantile");
        return x => quantile(df, ncp, x);
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
