// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The random numbers' conventions (the S-core plan's C3): the seed rules (CR2: xoshiro's 64 bits, the 32-bit range
/// elsewhere, Ranlux's seed sequence and Knuth's mask, QuantLib's own reductions), the composites' copies of their
/// generators (CR2 (d)), xoshiro's all-zero state and a lattice beyond its generating vector refused (CR7 (xxx),
/// (xxvii)), the lattice tables (CD7) and Faure's digits under MSVC's 32-bit <c>long</c> (CD6); and §2.6's two routes:
/// each Gaussian composite's moments and tails against the normal's, the Student quantile against its cumulative
/// function, and the discrepancy against its definition.
/// </summary>
public class RandomNumberConventionTests
{
    [Fact]
    public void TheSeedsAreCheckedAndXoshirosTakesSixtyFourBits()
    {
        foreach (Func<long, object> make in new Func<long, object>[]
                 {
                     s => new KnuthUniformRng(s), s => new LecuyerUniformRng(s), s => new Ranlux3UniformRng(s),
                     s => new Ranlux4UniformRng(s),
                 })
        {
            Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => make(4294967296L)).ParamName);
            Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => make(-1L)).ParamName);
            _ = make(4294967295L);
        }

        // xoshiro's seed is QuantLib's std::uint64_t, a long's 64 bits: -1 is 18446744073709551615
        var negative = new Xoshiro256StarStarUniformRng(-1L);
        var top = new Xoshiro256StarStarUniformRng(unchecked((long)18446744073709551615UL));
        Assert.Equal(negative.NextInt64(), top.NextInt64());
        Assert.Equal(new Xoshiro256StarStarUniformRng(5).NextReal(), Xoshiro256StarStarUniformRng.Create(5).NextReal());
    }

    [Fact]
    public void QuantLibsSeedReductionsAreKept()
    {
        // Ranlux: the standard engine's seed sequence takes the seed modulo 2147483563, a multiple of it as 1
        Assert.Equal(Draws(new Ranlux3UniformRng(1), 30), Draws(new Ranlux3UniformRng(2147483564L), 30));
        Assert.Equal(Draws(new Ranlux3UniformRng(1), 30), Draws(new Ranlux3UniformRng(2147483563L), 30));
        Assert.NotEqual(Draws(new Ranlux3UniformRng(1), 30), Draws(new Ranlux3UniformRng(2), 30));

        // Knuth: the seed's low 30 bits
        Assert.Equal(Draws(new KnuthUniformRng(5), 30), Draws(new KnuthUniformRng(5 + (1L << 30)), 30));
    }

    [Fact]
    public void XoshirosAllZeroStateIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new Xoshiro256StarStarUniformRng(0, 0, 0, 0));
        Assert.Equal("s0", e.ParamName);
        Assert.StartsWith("the all-zero state is refused", e.Message, StringComparison.Ordinal);
        _ = new Xoshiro256StarStarUniformRng(0, 0, 0, 1);
    }

    [Fact]
    public void TheCompositesCopyTheirGenerators()
    {
        // a composite's draws don't move with its argument's later draws, nor the argument with the composite's
        var knuth = new KnuthUniformRng(7);
        var boxMuller = new BoxMullerKnuthGaussianRng(knuth);
        var centralLimit = new CentralLimitKnuthGaussianRng(knuth);
        Draws(knuth, 50);
        Assert.Equal(Draws(new BoxMullerKnuthGaussianRng(new KnuthUniformRng(7)), 20), Draws(boxMuller, 20));
        Assert.Equal(Draws(new CentralLimitKnuthGaussianRng(new KnuthUniformRng(7)), 20), Draws(centralLimit, 20));

        var xoshiro = new Xoshiro256StarStarUniformRng(7);
        var ziggurat = new ZigguratXoshiro256StarStarGaussianRng(xoshiro);
        Draws(ziggurat, 20);
        Assert.Equal(Draws(new Xoshiro256StarStarUniformRng(7), 20), Draws(xoshiro, 20));

        var sobol = new SobolRsg(2, 1);
        var prs = RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(2, 3);
        var randomized = new RandomizedLDS<SobolRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>>(sobol, prs);
        sobol.NextSequence();
        prs.NextSequence();
        var fresh = new RandomizedLDS<SobolRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>>(
            new SobolRsg(2, 1), RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(2, 3));
        Assert.Equal(fresh.NextSequence().Value.ToArray(), randomized.NextSequence().Value.ToArray());
    }

    [Fact]
    public void ALatticeBeyondItsGeneratingVectorIsRefused()
    {
        var z = LatticeRule.GetRule(LatticeRuleType.B, 1024);
        Assert.Equal(LatticeRule.RuleLength, z.Length);
        Assert.Equal(
            "dimensionality",
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeRsg(LatticeRule.RuleLength + 1, z, 1024)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeRsg(2, z, 0)).ParamName);
        _ = new LatticeRsg(LatticeRule.RuleLength, z, 1024);
    }

    [Fact]
    public void TheLatticeTablesAreWholeNumbersBelowTwoToTheTwenty()
    {
        foreach (LatticeRuleType rule in Enum.GetValues<LatticeRuleType>())
        {
            var z = LatticeRule.GetRule(rule, 1024);
            Assert.Equal(1.0, z[0]);
            Assert.All(z, v => Assert.True(v == Math.Floor(v) && v > 0 && v < (1 << 20), $"{rule}: {v}"));
        }

        Assert.Equal("name", Assert.Throws<ArgumentOutOfRangeException>(() => LatticeRule.GetRule((LatticeRuleType)4, 1024)).ParamName);
    }

    [Fact]
    public void FaureRefusesPastTheDigitsMsvcsLongCarries()
    {
        // base 46349 (the prime at or above 46340 dimensions): ln(2^31 − 1)/ln 46349 is below 2, so the sequence
        // carries one digit and refuses its 46349th draw with QuantLib's text, where a 64-bit long carries three
        var faure = new FaureRsg(46340);
        for (int i = 0; i < 46348; ++i)
        {
            faure.NextIntSequence();
        }

        Assert.Equal("Error processing Faure sequence.", Assert.Throws<NanoQLException>(() => faure.NextSequence()).Message);
    }

    [Fact]
    public void TheGaussianCompositesHaveTheNormalsMoments()
    {
        // §2.6's two routes: each composite's sample moments against the normal's, within five standard errors, and its
        // tail beyond 3 against 2(1 − Φ(3)); the central limit's sum of twelve uniforms has excess kurtosis −1/10
        const int n = 400000;
        (string Name, IRng Rng, double Kurtosis)[] composites =
        [
            ("Box-Muller", new BoxMullerMersenneTwisterGaussianRng(new MersenneTwisterUniformRng(11)), 0.0),
            ("central limit", new CentralLimitLecuyerGaussianRng(new LecuyerUniformRng(11)), -0.1),
            ("ziggurat", new ZigguratXoshiro256StarStarGaussianRng(new Xoshiro256StarStarUniformRng(11)), 0.0),
            ("inverse cumulative", new InvCumulativeKnuthGaussianRng(new KnuthUniformRng(11)), 0.0),
        ];
        double tail = 2.0 * (1.0 - new CumulativeNormalDistribution().Evaluate(3.0));
        foreach ((string name, IRng rng, double kurtosis) in composites)
        {
            var stats = new IncrementalStatistics();
            int beyond = 0;
            for (int i = 0; i < n; ++i)
            {
                double x = rng.Next().Value;
                stats.Add(x);
                beyond += Math.Abs(x) > 3.0 ? 1 : 0;
            }

            Assert.True(Math.Abs(stats.Mean()) < 5.0 / Math.Sqrt(n), $"{name} mean {stats.Mean()}");
            Assert.True(Math.Abs(stats.Variance() - 1.0) < 5.0 * Math.Sqrt(2.0 / n), $"{name} variance {stats.Variance()}");
            Assert.True(Math.Abs(stats.Skewness()) < 5.0 * Math.Sqrt(6.0 / n), $"{name} skewness {stats.Skewness()}");
            Assert.True(Math.Abs(stats.Kurtosis() - kurtosis) < 5.0 * Math.Sqrt(24.0 / n), $"{name} kurtosis {stats.Kurtosis()}");
            if (kurtosis == 0.0)
            {
                Assert.True(
                    Math.Abs(((double)beyond / n) - tail) < 5.0 * Math.Sqrt(tail / n), $"{name} tail {(double)beyond / n}");
            }
        }

        // the ziggurat's own tail sampler starts at R, its base layer's edge, beyond 3: its tail is counted beyond R,
        // over 4e6 draws, about 1,030 expected, the band ±160
        const double r = 3.6541528853610088;
        const int m = 4000000;
        var ziggurat = new ZigguratXoshiro256StarStarGaussianRng(new Xoshiro256StarStarUniformRng(12));
        int beyondR = 0;
        for (int i = 0; i < m; ++i)
        {
            beyondR += Math.Abs(ziggurat.Next().Value) > r ? 1 : 0;
        }

        double tailR = 2.0 * (1.0 - new CumulativeNormalDistribution().Evaluate(r));
        Assert.True(
            Math.Abs(((double)beyondR / m) - tailR) < 5.0 * Math.Sqrt(tailR / m), $"ziggurat tail beyond R {beyondR}");
    }

    [Fact]
    public void TheStudentQuantileInvertsItsCumulativeFunctionAtLargeNu()
    {
        // §2.6's two routes at degrees of freedom QuantLib's test doesn't reach, where the cumulative function's
        // complementary argument (CR7 (xxiii)) keeps the round trip within 1e-13
        foreach (int n in new[] { 1000, 1000000, 100000000 })
        {
            var inverse = new InverseCumulativeStudent(n);
            var cdf = new CumulativeStudentDistribution(n);
            foreach (double p in new[] { 1e-8, 1e-4, 0.01, 0.1, 0.3, 0.6, 0.9, 0.999 })
            {
                double error = Math.Abs(cdf.Evaluate(inverse.Evaluate(p)) - p);
                Assert.True(error <= 1e-13 * Math.Max(p, 1e-2), $"n {n}, p {p}: {error}");
            }
        }
    }

    [Fact]
    public void TheDiscrepancyIsItsDefinition()
    {
        // §2.6's two routes: QuantLib's own commented-out double sum, sqrt(Σᵢⱼ Πₖ (1 − max(rᵢₖ, rⱼₖ))/N²
        // − 2^(1−d)/N · Σᵢ Πₖ (1 − rᵢₖ²) + 3^(−d)), on 25 points in 3 dimensions
        var rng = new MersenneTwisterUniformRng(5);
        double[][] points = [.. Enumerable.Range(0, 25).Select(_ => new[] { rng.NextReal(), rng.NextReal(), rng.NextReal() })];
        var stat = new DiscrepancyStatistics(3);
        foreach (double[] p in points)
        {
            stat.Add(p);
        }

        double a = 0.0, c = 0.0;
        foreach (double[] pi in points)
        {
            c += pi.Aggregate(1.0, (t, r) => t * (1.0 - (r * r)));
            foreach (double[] pj in points)
            {
                a += pi.Zip(pj).Aggregate(1.0, (t, r) => t * (1.0 - Math.Max(r.First, r.Second)));
            }
        }

        double n = points.Length;
        double direct = Math.Sqrt((a / (n * n)) - (Math.Pow(2.0, -2) / n * c) + Math.Pow(3.0, -3));
        Assert.True(Math.Abs(stat.Discrepancy() - direct) <= 1e-13 * direct, $"{stat.Discrepancy()} against {direct}");
    }

    private static double[] Draws(IRng rng, int n) => [.. Enumerable.Range(0, n).Select(_ => rng.Next().Value)];
}
