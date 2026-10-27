// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/lowdiscrepancysequences.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testRandomizedLowDiscrepancySequence, testRandomizedLattices, testFaure, testMersenneTwisterDiscrepancy, testPlainHaltonDiscrepancy, testRandomStartHaltonDiscrepancy, testRandomShiftHaltonDiscrepancy, testRandomStartRandomShiftHaltonDiscrepancy, testJackelSobolDiscrepancy, testSobolLevitanSobolDiscrepancy, testSobolLevitanLemieuxSobolDiscrepancy, testUnitSobolDiscrepancy
// Replaced: testSeedGenerator (Divergences/SeedZeroTests.cs)

using System.Reflection;
using NanoQL.Numerics;
using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class LowDiscrepancySequencesTests
{
    // primitivepolynomials.hpp's PPMT_MAX_DIM at its default, which nanoql keeps internal.
    private const int PPMT_MAX_DIM = 21200;

    private readonly List<string> errors = [];

    // Adapted: PrimitivePolynomials[i][j] (line 76) and PPMT_MAX_DIM (lines 59 and 71) are read through reflection from
    // the internal class that loads nanoql's generated table, PrimitivePolynomials, which keeps QuantLib's layout.
    [Fact]
    public void TestPolynomialsModuloTwo()
    {
        Type table = typeof(SobolRsg).Assembly.GetType("NanoQL.Numerics.PrimitivePolynomials", throwOnError: true)!;
        var primitivePolynomials = table.GetMethod("Get", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<int, int, int>>();
        var maxDimension = (int)table.GetField("PPMT_MAX_DIM", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;
        Assert.Equal(PPMT_MAX_DIM, maxDimension);

        Size[] jj =
        [
                  1,       1,       2,       2,       6,       6,      18,
                 16,      48,      60,     176,     144,     630,     756,
               1800,    2048,    7710,    7776,   27594,   24000,   84672,
             120032,  356960,  276480, 1296000, 1719900, 4202496,
        ];

        Size i = 0, j = 0, n = 0;
        BigInteger polynomial = 0;
        while (n < maxDimension || polynomial != -1)
        {
            if (polynomial == -1)
            {
                ++i; // Increase degree index
                j = 0; // Reset index of polynomial in degree.
            }

            polynomial = primitivePolynomials(i, j);
            if (polynomial == -1)
            {
                --n;
                if (j != jj[i])
                {
                    errors.Add($"Only {j} polynomials in degree {i + 1} instead of {jj[i]}");
                }
            }

            ++j; // Increase index of polynomial in degree i+1
            ++n; // Increase overall polynomial counter
        }

        Assert.Empty(errors);
    }

    // Adapted: the homogeneity block's SequenceStatistics (lines 207, 214 and 216) is a mean computed in the test, each
    // dimension's sum over the draws divided by their number, as its mean() is for weights of 1; and the van der Corput
    // block's SobolRsg(dimensionality) (line 247), QuantLib's default seed 0, takes seed 1, since nanoql refuses 0
    // (Divergences/SeedZeroTests.cs): one dimension lies in every table, where the seed isn't read.
    [Fact]
    public void TestSobol()
    {
        Real[] point;
        Real tolerance = 1.0e-15;

        // testing max dimensionality
        Size dimensionality = PPMT_MAX_DIM;
        long seed = 123456;
        var rsg = new SobolRsg(dimensionality, seed);
        Size points = 100, i;
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            if (point.Length != dimensionality)
            {
                errors.Add($"Sobol sequence generator returns  a sequence of wrong dimensionality: {point.Length} instead of  {dimensionality}");
            }
        }

        // testing homogeneity properties
        dimensionality = 33;
        seed = 123456;
        rsg = new SobolRsg(dimensionality, seed);
        var sums = new Real[dimensionality];
        Size samples = 0;
        Real[] mean;
        Size k = 0;
        for (Integer j = 1; j < 5; j++)
        {
            // five cycle
            points = (Size)Math.Pow(2.0, j) - 1; // base 2
            for (; k < points; k++)
            {
                point = rsg.NextSequence().Value.ToArray();
                Add(sums, ref samples, point);
            }

            mean = Mean(sums, samples);
            for (i = 0; i < dimensionality; i++)
            {
                Real error = Math.Abs(mean[i] - 0.5);
                if (error > tolerance)
                {
                    errors.Add($"{Ordinal(i + 1)} dimension: mean ({mean[i]:F6}) at the end of the {Ordinal(j + 1)} cycle in Sobol sequence is not {0.5:F6} (error = {error:E})");
                }
            }
        }

        // testing first dimension (van der Corput sequence)
        Real[] vanderCorputSequenceModuloTwo =
        [
            // first cycle (zero excluded)
            0.50000,
            // second cycle
            0.75000, 0.25000,
            // third cycle
            0.37500, 0.87500, 0.62500, 0.12500,
            // fourth cycle
            0.18750, 0.68750, 0.93750, 0.43750, 0.31250, 0.81250, 0.56250, 0.06250,
            // fifth cycle
            0.09375, 0.59375, 0.84375, 0.34375, 0.46875, 0.96875, 0.71875, 0.21875,
            0.15625, 0.65625, 0.90625, 0.40625, 0.28125, 0.78125, 0.53125, 0.03125,
        ];

        dimensionality = 1;
        rsg = new SobolRsg(dimensionality, 1);
        points = (Size)Math.Pow(2.0, 5) - 1; // five cycles
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            Real error = Math.Abs(point[0] - vanderCorputSequenceModuloTwo[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw ({point[0]:F6}) in 1-D Sobol sequence is not in the van der Corput sequence modulo two: it should have been {vanderCorputSequenceModuloTwo[i]:F6} (error = {error:E})");
            }
        }

        Assert.Empty(errors);
    }

    // Adapted: every HaltonRsg(dimensionality, 0, false, false) (lines 423, 450, 479, 509 and 532), QuantLib's seed 0,
    // takes seed 1, since nanoql refuses 0 (Divergences/SeedZeroTests.cs): without a random start or shift the seed
    // isn't read. The homogeneity blocks' SequenceStatistics (lines 510, 518, 520, 533, 539 and 541) is a mean
    // computed in the test, each dimension's sum over the draws divided by their number, as its mean() is for weights
    // of 1, and its reset a new sum.
    [Fact]
    public void TestHalton()
    {
        Real[] point;
        Real tolerance = 1.0e-15;

        // testing "high" dimensionality
        Size dimensionality = PPMT_MAX_DIM;
        var rsg = new HaltonRsg(dimensionality, 1, false, false);
        Size points = 100, i, k;
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            if (point.Length != dimensionality)
            {
                errors.Add($"Halton sequence generator returns  a sequence of wrong dimensionality: {point.Length} instead of  {dimensionality}");
            }
        }

        // testing first and second dimension (van der Corput sequence)
        Real[] vanderCorputSequenceModuloTwo =
        [
            // first cycle (zero excluded)
            0.50000,
            // second cycle
            0.25000, 0.75000,
            // third cycle
            0.12500, 0.62500, 0.37500, 0.87500,
            // fourth cycle
            0.06250, 0.56250, 0.31250, 0.81250, 0.18750, 0.68750, 0.43750, 0.93750,
            // fifth cycle
            0.03125, 0.53125, 0.28125, 0.78125, 0.15625, 0.65625, 0.40625, 0.90625,
            0.09375, 0.59375, 0.34375, 0.84375, 0.21875, 0.71875, 0.46875, 0.96875,
        ];

        dimensionality = 1;
        rsg = new HaltonRsg(dimensionality, 1, false, false);
        points = (Size)Math.Pow(2.0, 5) - 1; // five cycles
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            Real error = Math.Abs(point[0] - vanderCorputSequenceModuloTwo[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw ({point[0]:F6}) in 1-D Halton sequence is not in the van der Corput sequence modulo two: it should have been {vanderCorputSequenceModuloTwo[i]:F6} (error = {error:E})");
            }
        }

        Real[] vanderCorputSequenceModuloThree =
        [
            // first cycle (zero excluded)
            1.0 / 3,  2.0 / 3,
            // second cycle
            1.0 / 9,  4.0 / 9,  7.0 / 9,  2.0 / 9,  5.0 / 9,  8.0 / 9,
            // third cycle
            1.0 / 27, 10.0 / 27, 19.0 / 27,  4.0 / 27, 13.0 / 27, 22.0 / 27,
            7.0 / 27, 16.0 / 27, 25.0 / 27,  2.0 / 27, 11.0 / 27, 20.0 / 27,
            5.0 / 27, 14.0 / 27, 23.0 / 27,  8.0 / 27, 17.0 / 27, 26.0 / 27,
        ];

        dimensionality = 2;
        rsg = new HaltonRsg(dimensionality, 1, false, false);
        points = (Size)Math.Pow(3.0, 3) - 1; // three cycles of the higher dimension
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            Real error = Math.Abs(point[0] - vanderCorputSequenceModuloTwo[i]);
            if (error > tolerance)
            {
                errors.Add($"First component of {Ordinal(i + 1)} draw ({point[0]:F6}) in 2-D Halton sequence is not in the van der Corput sequence modulo two: it should have been {vanderCorputSequenceModuloTwo[i]:F6} (error = {error:E})");
            }

            error = Math.Abs(point[1] - vanderCorputSequenceModuloThree[i]);
            if (error > tolerance)
            {
                errors.Add($"Second component of {Ordinal(i + 1)} draw ({point[1]:F6}) in 2-D Halton sequence is not in the van der Corput sequence modulo three: it should have been {vanderCorputSequenceModuloThree[i]:F6} (error = {error:E})");
            }
        }

        // testing homogeneity properties
        dimensionality = 33;
        rsg = new HaltonRsg(dimensionality, 1, false, false);
        var sums = new Real[dimensionality];
        Size samples = 0;
        Real[] mean;
        k = 0;
        Integer j;
        for (j = 1; j < 5; j++)
        {
            // five cycle
            points = (Size)Math.Pow(2.0, j) - 1; // base 2
            for (; k < points; k++)
            {
                point = rsg.NextSequence().Value.ToArray();
                Add(sums, ref samples, point);
            }

            mean = Mean(sums, samples);
            Real error = Math.Abs(mean[0] - 0.5);
            if (error > tolerance)
            {
                errors.Add($"First dimension mean ({mean[0]:F6}) at the end of the {Ordinal(j + 1)} cycle in Halton sequence is not {0.5:F6} (error = {error:E})");
            }
        }

        // reset generator and gaussianstatistics
        rsg = new HaltonRsg(dimensionality, 1, false, false);
        sums = new Real[dimensionality];
        samples = 0;
        k = 0;
        for (j = 1; j < 3; j++)
        {
            // three cycle
            points = (Size)Math.Pow(3.0, j) - 1; // base 3
            for (; k < points; k++)
            {
                point = rsg.NextSequence().Value.ToArray();
                Add(sums, ref samples, point);
            }

            mean = Mean(sums, samples);
            Real error = Math.Abs(mean[1] - 0.5);
            if (error > tolerance)
            {
                errors.Add($"Second dimension mean ({mean[1]:F6}) at the end of the {Ordinal(j + 1)} cycle in Halton sequence is not {0.5:F6} (error = {error:E})");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestSobolSkipping()
    {
        long seed = 42;
        Size[] dimensionality = [1, 10, 100, 1000];
        long[] skip = [0, 1, 42, 512, 100000];
        SobolRsgDirectionIntegers[] integers =
        [
            SobolRsgDirectionIntegers.Unit,
            SobolRsgDirectionIntegers.Jaeckel,
            SobolRsgDirectionIntegers.SobolLevitan,
            SobolRsgDirectionIntegers.SobolLevitanLemieux,
        ];

        foreach (SobolRsgDirectionIntegers integer in integers)
        {
            foreach (Size j in dimensionality)
            {
                foreach (long k in skip)
                {
                    // extract n samples
                    var rsg1 = new SobolRsg(j, seed, integer);
                    for (long l = 0; l < k; l++)
                    {
                        rsg1.NextInt32Sequence();
                    }

                    // skip n samples at once
                    var rsg2 = new SobolRsg(j, seed, integer);
                    rsg2.SkipTo(k);

                    // compare next 100 samples
                    for (Size m = 0; m < 100; m++)
                    {
                        ReadOnlySpan<long> s1 = rsg1.NextInt32Sequence().Span;
                        ReadOnlySpan<long> s2 = rsg2.NextInt32Sequence().Span;
                        for (Size n = 0; n < s1.Length; n++)
                        {
                            if (s1[n] != s2[n])
                            {
                                errors.Add($"Mismatch after skipping:\n  size:     {j}\n  integers: {(int)integer}\n  skipped:  {k}\n  at index: {n}\n  expected: {s1[n]}\n  found:    {s2[n]}");
                            }
                        }
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestSobolBurleySkipping()
    {
        long seed = 42;
        long scramblingSeed = 43;
        Size[] dimensionality = [1, 10, 100, 1000];
        long[] skip = [0, 1, 42, 512, 10000];
        SobolRsgDirectionIntegers[] integers =
        [
            SobolRsgDirectionIntegers.Jaeckel,
            SobolRsgDirectionIntegers.SobolLevitan,
            SobolRsgDirectionIntegers.SobolLevitanLemieux,
        ];

        foreach (SobolRsgDirectionIntegers integer in integers)
        {
            foreach (Size j in dimensionality)
            {
                foreach (long k in skip)
                {
                    // extract n samples
                    var rsg1 = new Burley2020SobolRsg(j, seed, integer, scramblingSeed);
                    for (long l = 0; l < k; l++)
                    {
                        rsg1.NextInt32Sequence();
                    }

                    // skip n samples at once
                    var rsg2 = new Burley2020SobolRsg(j, seed, integer, scramblingSeed);
                    rsg2.SkipTo(k);

                    // compare next 100 samples
                    for (Size m = 0; m < 100; m++)
                    {
                        ReadOnlySpan<long> s1 = rsg1.NextInt32Sequence().Span;
                        ReadOnlySpan<long> s2 = rsg2.NextInt32Sequence().Span;
                        for (Size n = 0; n < s1.Length; n++)
                        {
                            if (s1[n] != s2[n])
                            {
                                errors.Add($"Mismatch after skipping:\n  size:     {j}\n  integers: {(int)integer}\n  skipped:  {k}\n  at index: {n}\n  expected: {s1[n]}\n  found:    {s2[n]}");
                            }
                        }
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestHighDimensionalIntegrals()
    {
        // We are running "Integration test 1, results for high dimensions" (Figure 9) from:
        //
        // Sobol, Asotsky, Kreinin, Kucherenko: Construction and Comparison of High-Dimensional Sobol’
        // Generators, available at https://www.broda.co.uk/doc/HD_SobolGenerator.pdf
        //
        // We check the error of Kuo1 (using Gray code and sequential numbers) roughly against what
        // their graph suggests. In addition we check the error of the Burley2020-scrambled version of
        // Kuo1 against what we experimentally find - the error turns out to be more than one order
        // better than the unscrambled version.
        static Real Integrand(Real[] c, ReadOnlySpan<Real> x)
        {
            Real p = 1.0;
            for (Size i = 0; i < c.Length; ++i)
            {
                p *= 1.0 + (c[i] * (x[i] - 0.5));
            }

            return p;
        }

        Size N = 30031;

        Size[] dimension = [1000, 2000, 5000];
        Real[][] expectedOrderOfError = [[-3.0, -3.0, -4.5], [-2.5, -2.5, -4.0], [-2.0, -2.0, -4.0]];

        for (Size d = 0; d < dimension.Length; ++d)
        {
            Real[] c1 = Enumerable.Repeat(0.01, dimension[d]).ToArray();

            var s1 = new SobolRsg(dimension[d], 42, SobolRsgDirectionIntegers.Kuo, true);
            var s2 = new SobolRsg(dimension[d], 42, SobolRsgDirectionIntegers.Kuo, false);
            var s3 = new Burley2020SobolRsg(dimension[d], 42, SobolRsgDirectionIntegers.Kuo, 43);

            Real I1 = 0.0, I2 = 0.0, I3 = 0.0;
            for (Size i = 0; i < N; ++i)
            {
                I1 += Integrand(c1, s1.NextSequence().Value.Span) / (double)N;
                I2 += Integrand(c1, s2.NextSequence().Value.Span) / (double)N;
                I3 += Integrand(c1, s3.NextSequence().Value.Span) / (double)N;
            }

            Real errOrder1 = Math.Log10(Math.Abs(I1 - 1.0));
            Real errOrder2 = Math.Log10(Math.Abs(I2 - 1.0));
            Real errOrder3 = Math.Log10(Math.Abs(I3 - 1.0));

            if (!(errOrder1 < expectedOrderOfError[d][0]))
            {
                errors.Add($"order of error for dimension {dimension[d]} is{errOrder1:F6} expected {expectedOrderOfError[d][0]:F6}");
            }

            if (!(errOrder2 < expectedOrderOfError[d][1]))
            {
                errors.Add($"order of error for dimension {dimension[d]} is{errOrder2:F6} expected {expectedOrderOfError[d][1]:F6}");
            }

            if (!(errOrder3 < expectedOrderOfError[d][2]))
            {
                errors.Add($"order of error for dimension {dimension[d]} is{errOrder3:F6} expected {expectedOrderOfError[d][2]:F6}");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestBurley2020SobolRsgOutputBounds()
    {
        // With enough dimensions the scrambling occasionally maps to
        // zero.  Without the +0.5 offset this would give 0.0 in the
        // double sequence, which breaks InverseCumulativeNormal.
        var rsg = new Burley2020SobolRsg(1551, 42, SobolRsgDirectionIntegers.JoeKuoD7, 43);
        for (Size i = 0; i < 100000; ++i)
        {
            ReadOnlySpan<Real> seq = rsg.NextSequence().Value.Span;
            for (Size j = 0; j < seq.Length; ++j)
            {
                if (seq[j] <= 0.0 || seq[j] >= 1.0)
                {
                    errors.Add($"output {seq[j]} at sample {i}, dim {j}");
                }
            }
        }

        Assert.Empty(errors);
    }

    // SequenceStatistics' add, for weights of 1: each dimension's running sum, and the number of draws.
    private static void Add(Real[] sums, ref Size samples, Real[] point)
    {
        for (Size i = 0; i < sums.Length; i++)
        {
            sums[i] += point[i];
        }

        samples++;
    }

    // SequenceStatistics' mean, for weights of 1.
    private static Real[] Mean(Real[] sums, Size samples) => [.. sums.Select(s => s / samples)];
}
