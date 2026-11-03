// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/lowdiscrepancysequences.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
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
    // Adapted: QuantLib's clock seeds become explicit, seed 1: RandomizedLDS(PPMT_MAX_DIM) (line 95), its sequences'
    // ldsSeed and prsSeed, the unused MersenneTwisterUniformRng t0 (line 100), SobolRsg t1(PPMT_MAX_DIM) (line 101),
    // RandomSequenceGenerator t2(PPMT_MAX_DIM) (line 102), and the pseudo-random generator RandomizedLDS(t1) builds
    // (line 108); the case asserts nothing about the numbers.
    [Fact]
    public void TestRandomizedLowDiscrepancySequence()
    {
        // Testing randomized low-discrepancy sequences up to dimension PPMT_MAX_DIM...

        RandomizedLDS<SobolRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>> rldsg =
            RandomizedLDS.Create<SobolRsg, MersenneTwisterUniformRng>(PPMT_MAX_DIM, 1, 1);
        rldsg.NextSequence();
        rldsg.LastSequence();
        rldsg.NextRandomizer();

        _ = new MersenneTwisterUniformRng(1);
        var t1 = new SobolRsg(PPMT_MAX_DIM, 1);
        RandomSequenceGenerator<MersenneTwisterUniformRng> t2 =
            RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(PPMT_MAX_DIM, 1);
        var rldsg2 = new RandomizedLDS<SobolRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>>(t1, t2);
        rldsg2.NextSequence();
        rldsg2.LastSequence();
        rldsg2.NextRandomizer();

        RandomizedLDS<SobolRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>> rldsg3 =
            RandomizedLDS.Create<SobolRsg, MersenneTwisterUniformRng>(t1, 1);
        rldsg3.NextSequence();
        rldsg3.LastSequence();
        rldsg3.NextRandomizer();
    }

    [Fact]
    public void TestRandomizedLattices()
    {
        TestRandomizedLatticeRule(LatticeRuleType.A, "A");
        TestRandomizedLatticeRule(LatticeRuleType.B, "B");
        TestRandomizedLatticeRule(LatticeRuleType.C, "C");
        TestRandomizedLatticeRule(LatticeRuleType.D, "D");
    }

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
    public void TestFaure()
    {
        // Testing Faure sequences...

        Real[] point;
        Real tolerance = 1.0e-15;

        // testing "high" dimensionality
        Size dimensionality = PPMT_MAX_DIM;
        var rsg = new FaureRsg(dimensionality);
        Size points = 100, i;
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            if (point.Length != dimensionality)
            {
                errors.Add($"Faure sequence generator returns  a sequence of wrong dimensionality: {point.Length} instead of  {dimensionality}");
            }
        }

        // 1-dimension Faure (van der Corput sequence base 2)
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
        rsg = new FaureRsg(dimensionality);
        points = (Size)Math.Pow(2.0, 5) - 1; // five cycles
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            Real error = Math.Abs(point[0] - vanderCorputSequenceModuloTwo[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw, dimension 1 ({point[0]:F6}) in 3-D Faure sequence should have been {vanderCorputSequenceModuloTwo[i]:F6} (error = {error:E})");
            }
        }

        // 2nd dimension of the 2-dimensional Faure sequence
        // (shuffled van der Corput sequence base 2)
        // checked with the code provided with "Economic generation of
        // low-discrepancy sequences with a b-ary gray code", by E. Thiemard
        Real[] faureDimensionTwoOfTwo =
        [
            // first cycle (zero excluded)
            0.50000,
            // second cycle
            0.25000, 0.75000,
            // third cycle
            0.37500, 0.87500, 0.12500, 0.62500,
            // fourth cycle
            0.31250, 0.81250, 0.06250, 0.56250, 0.18750, 0.68750, 0.43750, 0.93750,
            // fifth cycle
            0.46875, 0.96875, 0.21875, 0.71875, 0.09375, 0.59375, 0.34375, 0.84375,
            0.15625, 0.65625, 0.40625, 0.90625, 0.28125, 0.78125, 0.03125, 0.53125,
        ];
        dimensionality = 2;
        rsg = new FaureRsg(dimensionality);
        points = (Size)Math.Pow(2.0, 5) - 1; // five cycles
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            Real error = Math.Abs(point[0] - vanderCorputSequenceModuloTwo[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw, dimension 1 ({point[0]:F6}) in 3-D Faure sequence should have been {vanderCorputSequenceModuloTwo[i]:F6} (error = {error:E})");
            }

            error = Math.Abs(point[1] - faureDimensionTwoOfTwo[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw, dimension 2 ({point[1]:F6}) in 3-D Faure sequence should have been {faureDimensionTwoOfTwo[i]:F6} (error = {error:E})");
            }
        }

        // 3-dimension Faure sequence (shuffled van der Corput sequence base 3)
        // see "Monte Carlo Methods in Financial Engineering,"
        // by Paul Glasserman, 2004 Springer Verlag, pag. 299
        Real[] faureDimensionOneOfThree =
        [
            // first cycle (zero excluded)
            1.0 / 3, 2.0 / 3,
            // second cycle
            7.0 / 9, 1.0 / 9, 4.0 / 9, 5.0 / 9, 8.0 / 9, 2.0 / 9,
        ];
        Real[] faureDimensionTwoOfThree =
        [
            // first cycle (zero excluded)
            1.0 / 3, 2.0 / 3,
            // second cycle
            1.0 / 9, 4.0 / 9, 7.0 / 9, 2.0 / 9, 5.0 / 9, 8.0 / 9,
        ];
        Real[] faureDimensionThreeOfThree =
        [
            // first cycle (zero excluded)
            1.0 / 3, 2.0 / 3,
            // second cycle
            4.0 / 9, 7.0 / 9, 1.0 / 9, 8.0 / 9, 2.0 / 9, 5.0 / 9,
        ];

        dimensionality = 3;
        rsg = new FaureRsg(dimensionality);
        points = (Size)Math.Pow(3.0, 2) - 1; // three cycles
        for (i = 0; i < points; i++)
        {
            point = rsg.NextSequence().Value.ToArray();
            Real error = Math.Abs(point[0] - faureDimensionOneOfThree[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw, dimension 1 ({point[0]:F6}) in 3-D Faure sequence should have been {faureDimensionOneOfThree[i]:F6} (error = {error:E})");
            }

            error = Math.Abs(point[1] - faureDimensionTwoOfThree[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw, dimension 2 ({point[1]:F6}) in 3-D Faure sequence should have been {faureDimensionTwoOfThree[i]:F6} (error = {error:E})");
            }

            error = Math.Abs(point[2] - faureDimensionThreeOfThree[i]);
            if (error > tolerance)
            {
                errors.Add($"{Ordinal(i + 1)} draw, dimension 3 ({point[2]:F6}) in 3-D Faure sequence should have been {faureDimensionThreeOfThree[i]:F6} (error = {error:E})");
            }
        }

        Assert.Empty(errors);
    }

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
    public void TestMersenneTwisterDiscrepancy()
    {
        // Testing Mersenne-twister discrepancy...

        Real[][] discrepancy =
        [
            dim002DiscrMersenneTwis, dim003DiscrMersenneTwis, dim005DiscrMersenneTwis, dim010DiscrMersenneTwis,
            dim015DiscrMersenneTwis, dim030DiscrMersenneTwis, dim050DiscrMersenneTwis, dim100DiscrMersenneTwis,
        ];

        TestGeneratorDiscrepancy(MersenneFactory(), discrepancy);
    }

    [Fact]
    public void TestPlainHaltonDiscrepancy()
    {
        // Testing plain Halton discrepancy...

        Real[][] discrepancy =
        [
            dim002DiscrPlain_Halton, dim003DiscrPlain_Halton, dim005DiscrPlain_Halton, dim010DiscrPlain_Halton,
            dim015DiscrPlain_Halton, dim030DiscrPlain_Halton, dim050DiscrPlain_Halton, dim100DiscrPlain_Halton,
        ];

        TestGeneratorDiscrepancy(HaltonFactory(false, false), discrepancy);
    }

    [Fact]
    public void TestRandomStartHaltonDiscrepancy()
    {
        // Testing random-start Halton discrepancy...

        Real[][] discrepancy =
        [
            dim002DiscrRStartHalton, dim003DiscrRStartHalton, dim005DiscrRStartHalton, dim010DiscrRStartHalton,
            dim015DiscrRStartHalton, dim030DiscrRStartHalton, dim050DiscrRStartHalton, dim100DiscrRStartHalton,
        ];

        TestGeneratorDiscrepancy(HaltonFactory(true, false), discrepancy);
    }

    [Fact]
    public void TestRandomShiftHaltonDiscrepancy()
    {
        // Testing random-shift Halton discrepancy...

        Real[][] discrepancy =
        [
            dim002DiscrRShiftHalton, dim003DiscrRShiftHalton, dim005DiscrRShiftHalton, dim010DiscrRShiftHalton,
            dim015DiscrRShiftHalton, dim030DiscrRShiftHalton, dim050DiscrRShiftHalton, dim100DiscrRShiftHalton,
        ];

        TestGeneratorDiscrepancy(HaltonFactory(false, true), discrepancy);
    }

    [Fact]
    public void TestRandomStartRandomShiftHaltonDiscrepancy()
    {
        // Testing random-start, random-shift Halton discrepancy...

        Real[][] discrepancy =
        [
            dim002DiscrRStRShHalton, dim003DiscrRStRShHalton, dim005DiscrRStRShHalton, dim010DiscrRStRShHalton,
            dim015DiscrRStRShHalton, dim030DiscrRStRShHalton, dim050DiscrRStRShHalton, dim100DiscrRStRShHalton,
        ];

        TestGeneratorDiscrepancy(HaltonFactory(true, true), discrepancy);
    }

    [Fact]
    public void TestJackelSobolDiscrepancy()
    {
        // Testing Jaeckel-Sobol discrepancy...

        Real[][] discrepancy =
        [
            dim002Discr_Sobol, dim003Discr_Sobol, dim005Discr_Sobol, dim010DiscrJackel_Sobol,
            dim015DiscrJackel_Sobol, dim030DiscrJackel_Sobol, dim050DiscrJackel_Sobol, dim100DiscrJackel_Sobol,
        ];

        TestGeneratorDiscrepancy(SobolFactory(SobolRsgDirectionIntegers.Jaeckel), discrepancy);
    }

    [Fact]
    public void TestSobolLevitanSobolDiscrepancy()
    {
        // Testing Levitan-Sobol discrepancy...

        Real[][] discrepancy =
        [
            dim002Discr_Sobol, dim003Discr_Sobol, dim005Discr_Sobol, dim010DiscrSobLev_Sobol,
            dim015DiscrSobLev_Sobol, dim030DiscrSobLev_Sobol, dim050DiscrSobLev_Sobol, dim100DiscrSobLev_Sobol,
        ];

        TestGeneratorDiscrepancy(SobolFactory(SobolRsgDirectionIntegers.SobolLevitan), discrepancy);
    }

    [Fact]
    public void TestSobolLevitanLemieuxSobolDiscrepancy()
    {
        // Testing Levitan-Lemieux-Sobol discrepancy...

        Real[][] discrepancy =
        [
            dim002Discr_Sobol, dim003Discr_Sobol, dim005Discr_Sobol, dim010DiscrSobLev_Sobol,
            dim015DiscrSobLev_Sobol, dim030DiscrSobLev_Sobol, dim050DiscrSobLem_Sobol, dim100DiscrSobLem_Sobol,
        ];

        TestGeneratorDiscrepancy(SobolFactory(SobolRsgDirectionIntegers.SobolLevitanLemieux), discrepancy);
    }

    [Fact]
    public void TestUnitSobolDiscrepancy()
    {
        // Testing unit Sobol discrepancy...

        Real[][] discrepancy =
        [
            dim002Discr_Unit_Sobol, dim003Discr_Unit_Sobol, dim005Discr_Unit_Sobol, dim010Discr_Unit_Sobol,
            dim015Discr_Unit_Sobol, dim030Discr_Unit_Sobol, dim050Discr_Unit_Sobol, dim100Discr_Unit_Sobol,
        ];

        TestGeneratorDiscrepancy(SobolFactory(SobolRsgDirectionIntegers.Unit), discrepancy);
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

    // the dimensions each discrepancy case measures
    private static readonly Size[] dimensionality = [2, 3, 5, 10, 15, 30, 50, 100];

    // 7 discrepancy measures for each dimension of all sequence generators
    // would take a few days ... too long for usual/frequent test running
    private const Size discrepancyMeasuresNumber = 1;

    private static readonly Real[] dim002Discr_Sobol = [8.33e-004, 4.32e-004, 2.24e-004, 1.12e-004, 5.69e-005, 2.14e-005];
    private static readonly Real[] dim002DiscrMersenneTwis = [8.84e-003, 5.42e-003, 5.23e-003, 4.47e-003, 4.75e-003, 3.11e-003, 2.97e-003];
    private static readonly Real[] dim002DiscrPlain_Halton = [1.26e-003, 6.73e-004, 3.35e-004, 1.91e-004, 1.11e-004, 5.05e-005, 2.42e-005];
    private static readonly Real[] dim002DiscrRShiftHalton = [1.32e-003, 7.25e-004];
    private static readonly Real[] dim002DiscrRStRShHalton = [1.35e-003, 9.43e-004];
    private static readonly Real[] dim002DiscrRStartHalton = [1.08e-003, 6.40e-004];
    private static readonly Real[] dim002Discr_Unit_Sobol = [8.33e-004, 4.32e-004, 2.24e-004, 1.12e-004, 5.69e-005, 2.14e-005];
    private static readonly Real[] dim003Discr_Sobol = [1.21e-003, 6.37e-004, 3.40e-004, 1.75e-004, 9.21e-005, 4.79e-005, 2.56e-005];
    private static readonly Real[] dim003DiscrMersenneTwis = [7.02e-003, 4.94e-003, 4.82e-003, 4.91e-003, 3.33e-003, 2.80e-003, 2.62e-003];
    private static readonly Real[] dim003DiscrPlain_Halton = [1.63e-003, 9.62e-004, 4.83e-004, 2.67e-004, 1.41e-004, 7.64e-005, 3.93e-005];
    private static readonly Real[] dim003DiscrRShiftHalton = [1.96e-003, 1.03e-003];
    private static readonly Real[] dim003DiscrRStRShHalton = [2.17e-003, 1.54e-003];
    private static readonly Real[] dim003DiscrRStartHalton = [1.48e-003, 7.77e-004];
    private static readonly Real[] dim003Discr_Unit_Sobol = [1.21e-003, 6.37e-004, 3.40e-004, 1.75e-004, 9.21e-005, 4.79e-005, 2.56e-005];
    private static readonly Real[] dim005Discr_Sobol = [1.59e-003, 9.55e-004, 5.33e-004, 3.22e-004, 1.63e-004, 9.41e-005, 5.19e-005];
    private static readonly Real[] dim005DiscrMersenneTwis = [4.28e-003, 3.48e-003, 2.48e-003, 1.98e-003, 1.57e-003, 1.39e-003, 6.33e-004];
    private static readonly Real[] dim005DiscrPlain_Halton = [1.93e-003, 1.23e-003, 6.89e-004, 4.22e-004, 2.13e-004, 1.25e-004, 7.17e-005];
    private static readonly Real[] dim005DiscrRShiftHalton = [2.02e-003, 1.36e-003];
    private static readonly Real[] dim005DiscrRStRShHalton = [2.11e-003, 1.25e-003];
    private static readonly Real[] dim005DiscrRStartHalton = [1.74e-003, 1.08e-003];
    private static readonly Real[] dim005Discr_Unit_Sobol = [1.85e-003, 9.39e-004, 5.19e-004, 2.99e-004, 1.75e-004, 9.51e-005, 5.55e-005];
    private static readonly Real[] dim010DiscrJackel_Sobol = [7.08e-004, 5.31e-004, 3.60e-004, 2.18e-004, 1.57e-004, 1.12e-004, 6.39e-005];
    private static readonly Real[] dim010DiscrSobLev_Sobol = [7.01e-004, 5.10e-004, 3.28e-004, 2.21e-004, 1.57e-004, 1.08e-004, 6.38e-005];
    private static readonly Real[] dim010DiscrMersenneTwis = [8.83e-004, 6.56e-004, 4.87e-004, 3.37e-004, 3.06e-004, 1.73e-004, 1.43e-004];
    private static readonly Real[] dim010DiscrPlain_Halton = [1.23e-003, 6.89e-004, 4.03e-004, 2.83e-004, 1.61e-004, 1.08e-004, 6.69e-005];
    private static readonly Real[] dim010DiscrRShiftHalton = [9.25e-004, 6.40e-004];
    private static readonly Real[] dim010DiscrRStRShHalton = [8.41e-004, 5.42e-004];
    private static readonly Real[] dim010DiscrRStartHalton = [7.89e-004, 5.33e-004];
    private static readonly Real[] dim010Discr_Unit_Sobol = [7.67e-004, 4.92e-004, 3.47e-004, 2.34e-004, 1.39e-004, 9.47e-005, 5.72e-005];
    private static readonly Real[] dim015DiscrJackel_Sobol = [1.59e-004, 1.23e-004, 7.73e-005, 5.51e-005, 3.91e-005, 2.73e-005, 1.96e-005];
    private static readonly Real[] dim015DiscrSobLev_Sobol = [1.48e-004, 1.06e-004, 8.19e-005, 6.29e-005, 4.16e-005, 2.54e-005, 1.73e-005];
    private static readonly Real[] dim015DiscrMersenneTwis = [1.63e-004, 1.12e-004, 8.36e-005, 6.09e-005, 4.34e-005, 2.95e-005, 2.10e-005];
    private static readonly Real[] dim015DiscrPlain_Halton = [5.75e-004, 3.12e-004, 1.70e-004, 9.89e-005, 5.33e-005, 3.45e-005, 2.11e-005];
    private static readonly Real[] dim015DiscrRShiftHalton = [1.75e-004, 1.19e-004];
    private static readonly Real[] dim015DiscrRStRShHalton = [1.66e-004, 1.34e-004];
    private static readonly Real[] dim015DiscrRStartHalton = [2.09e-004, 1.30e-004];
    private static readonly Real[] dim015Discr_Unit_Sobol = [2.24e-004, 1.39e-004, 9.86e-005, 6.02e-005, 4.39e-005, 3.06e-005, 2.32e-005];
    private static readonly Real[] dim030DiscrJackel_Sobol = [6.43e-007, 5.28e-007, 3.88e-007, 2.49e-007, 2.09e-007, 1.55e-007, 1.07e-007];
    private static readonly Real[] dim030DiscrSobLev_Sobol = [1.03e-006, 6.06e-007, 3.81e-007, 2.71e-007, 2.68e-007, 1.73e-007, 1.21e-007];
    private static readonly Real[] dim030DiscrMersenneTwis = [4.38e-007, 3.25e-007, 4.47e-007, 2.85e-007, 2.03e-007, 1.50e-007, 1.17e-007];
    private static readonly Real[] dim030DiscrPlain_Halton = [4.45e-004, 2.23e-004, 1.11e-004, 5.56e-005, 2.78e-005, 1.39e-005, 6.95e-006];
    private static readonly Real[] dim030DiscrRShiftHalton = [8.11e-007, 6.05e-007];
    private static readonly Real[] dim030DiscrRStRShHalton = [1.85e-006, 1.03e-006];
    private static readonly Real[] dim030DiscrRStartHalton = [4.42e-007, 4.64e-007];
    private static readonly Real[] dim030Discr_Unit_Sobol = [4.35e-005, 2.17e-005, 1.09e-005, 5.43e-006, 2.73e-006, 1.37e-006, 6.90e-007];
    private static readonly Real[] dim050DiscrJackel_Sobol = [2.98e-010, 2.91e-010, 2.62e-010, 1.53e-010, 1.48e-010, 1.15e-010, 8.41e-011];
    private static readonly Real[] dim050DiscrSobLev_Sobol = [3.11e-010, 2.52e-010, 1.61e-010, 1.54e-010, 1.11e-010, 8.60e-011, 1.17e-010];
    private static readonly Real[] dim050DiscrSobLem_Sobol = [4.57e-010, 6.84e-010, 3.68e-010, 2.20e-010, 1.81e-010, 1.14e-010, 8.31e-011];
    private static readonly Real[] dim050DiscrMersenneTwis = [3.27e-010, 2.42e-010, 1.47e-010, 1.98e-010, 2.31e-010, 1.30e-010, 8.09e-011];
    private static readonly Real[] dim050DiscrPlain_Halton = [4.04e-004, 2.02e-004, 1.01e-004, 5.05e-005, 2.52e-005, 1.26e-005, 6.31e-006];
    private static readonly Real[] dim050DiscrRShiftHalton = [1.14e-010, 1.25e-010];
    private static readonly Real[] dim050DiscrRStRShHalton = [2.92e-010, 5.02e-010];
    private static readonly Real[] dim050DiscrRStartHalton = [1.93e-010, 6.82e-010];
    private static readonly Real[] dim050Discr_Unit_Sobol = [1.63e-005, 8.14e-006, 4.07e-006, 2.04e-006, 1.02e-006, 5.09e-007, 2.54e-007];
    private static readonly Real[] dim100DiscrJackel_Sobol = [1.26e-018, 1.55e-018, 8.46e-019, 4.43e-019, 4.04e-019, 2.44e-019, 4.86e-019];
    private static readonly Real[] dim100DiscrSobLev_Sobol = [1.17e-018, 2.65e-018, 1.45e-018, 7.28e-019, 6.33e-019, 3.36e-019, 3.43e-019];
    private static readonly Real[] dim100DiscrSobLem_Sobol = [8.79e-019, 4.60e-019, 6.69e-019, 7.17e-019, 5.81e-019, 2.97e-019, 2.64e-019];
    private static readonly Real[] dim100DiscrMersenneTwis = [5.30e-019, 7.29e-019, 3.71e-019, 3.33e-019, 1.33e-017, 6.70e-018, 3.36e-018];
    private static readonly Real[] dim100DiscrPlain_Halton = [3.63e-004, 1.81e-004, 9.07e-005, 4.53e-005, 2.27e-005, 1.13e-005, 5.66e-006];
    private static readonly Real[] dim100DiscrRShiftHalton = [3.36e-019, 2.19e-019];
    private static readonly Real[] dim100DiscrRStRShHalton = [4.44e-019, 2.24e-019];
    private static readonly Real[] dim100DiscrRStartHalton = [9.85e-020, 8.34e-019];
    private static readonly Real[] dim100Discr_Unit_Sobol = [4.97e-006, 2.48e-006, 1.24e-006, 6.20e-007, 3.10e-007, 1.55e-007, 7.76e-008];

    private static void TestRandomizedLatticeRule(LatticeRuleType name, string nameString)
    {
        Size maxDim = 30;
        Size n = 1024;
        Size numberBatches = 32;

        // Testing randomized lattice sequences (nameString) up to dimension maxDim...

        var z = LatticeRule.GetRule(name, n);
        var latticeGenerator = new LatticeRsg(maxDim, z, n);

        long seed = 12345678L;
        var rng = new MersenneTwisterUniformRng(seed);

        var rsg = new RandomSequenceGenerator<MersenneTwisterUniformRng>(maxDim, rng);

        var rldsg = new RandomizedLDS<LatticeRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>>(latticeGenerator, rsg);

        var outerStats = new SequenceStatistics(maxDim);

        for (Size i = 0; i < numberBatches; ++i)
        {
            var innerStats = new SequenceStatistics(maxDim);
            for (Size j = 0; j < n; ++j)
            {
                innerStats.Add(rldsg.NextSequence().Value.Span);
            }

            outerStats.Add(innerStats.Mean().AsSpan());
            rldsg.NextRandomizer();
        }

        Real[] means = [.. outerStats.Mean()];
        Real[] sds = [.. outerStats.ErrorEstimate()];

        var errorInSds = new Real[maxDim];

        for (Size i = 0; i < maxDim; ++i)
        {
            errorInSds[i] = (means[i] - 0.5) / sds[i];
        }

        Real tolerance = 4.0;

        for (Size i = 0; i < maxDim; ++i)
        {
            if (Math.Abs(errorInSds[i]) > tolerance)
            {
                Assert.Fail($"Lattice generator{nameString} returns  a mean of {means[i]} with error equal to  {errorInSds[i]} standard deviations in dimension {i}");
            }
        }
    }

    // testGeneratorDiscrepancy's factories: the generator of a dimension and a seed, and its name
    private static (Func<Size, long, IRsg> Make, string Name) MersenneFactory() =>
        ((dim, seed) => RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(dim, seed), "Mersenne Twister");

    private static (Func<Size, long, IRsg> Make, string Name) SobolFactory(SobolRsgDirectionIntegers unit) =>
        ((dim, seed) => new SobolRsg(dim, seed, unit), unit switch
        {
            SobolRsgDirectionIntegers.Unit => "unit-initialized ",
            SobolRsgDirectionIntegers.Jaeckel => "Jaeckel-initialized ",
            SobolRsgDirectionIntegers.SobolLevitan => "SobolLevitan-initialized ",
            SobolRsgDirectionIntegers.SobolLevitanLemieux => "SobolLevitanLemieux-initialized ",
            SobolRsgDirectionIntegers.Kuo => "Kuo",
            SobolRsgDirectionIntegers.Kuo2 => "Kuo2",
            SobolRsgDirectionIntegers.Kuo3 => "Kuo3",
            _ => throw new NanoQLException("unknown direction integers"),
        } + "Sobol sequences: ");

    private static (Func<Size, long, IRsg> Make, string Name) HaltonFactory(bool randomStart, bool randomShift) =>
        ((dim, seed) => new HaltonRsg(dim, seed, randomStart, randomShift),
            (randomStart ? "random-start " : "") + (randomShift ? "random-shift " : "") + "Halton");

    private static void TestGeneratorDiscrepancy((Func<Size, long, IRsg> Make, string Name) generatorFactory, Real[][] discrepancy)
    {
        Real tolerance = 1.0e-2;

        Real[] point;
        Size dim;
        long seed = 123456;
        Real discr;

        // more than 1 discrepancy measures take long time
        Size sampleLoops = Math.Max(1, discrepancyMeasuresNumber);

        for (Integer i = 0; i < 8; i++)
        {
            dim = dimensionality[i];
            var stat = new DiscrepancyStatistics(dim);

            IRsg rsg = generatorFactory.Make(dim, seed);

            Size j, k = 0, jMin = 10;
            stat.Reset();
            for (j = jMin; j < jMin + sampleLoops; j++)
            {
                Size points = (Size)Math.Pow(2.0, j) - 1;
                for (; k < points; k++)
                {
                    point = rsg.NextSequence().Value.ToArray();
                    stat.Add(point);
                }

                discr = stat.Discrepancy();

                if (Math.Abs(discr - discrepancy[i][j - jMin]) > tolerance * discr)
                {
                    Assert.Fail($"{generatorFactory.Name}discrepancy dimension {dimensionality[i]} at {points} samples is {discr} instead of {discrepancy[i][j - jMin]}");
                }
            }
        }
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
