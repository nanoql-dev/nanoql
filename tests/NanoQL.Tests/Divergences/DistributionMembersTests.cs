// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Distribution's members on its own density, uniform within each bucket (the S-xcr plan's XR8 (xx)), and the unused
/// classes' slips (XR8 (xxvii)), each checked against its closed form. QuantLib's values are the S-xcr run's
/// <c>c2_facts.cpp</c>: on a uniform of ten buckets over [0, 10] its shortfalls read 5.625 and 9 for 5.25 and 9.5, and
/// its tranche on [2, 5] keeps a probability of 0.6, its shortfall reading the tranche's 3 at 0.5 to 0.99 (the S-xcr
/// code review's C2 O1-1); its cumulative density in the first bucket reads index −1
/// (<c>distribution.cpp:228</c>), its convolution assigns where it adds, its binomial distribution reads a member it
/// never sets, and its binomial probability of at least 0 events wraps to 0.
/// </summary>
public class DistributionMembersTests
{
    [Theory]
    [InlineData(0.05, 5.25)]
    [InlineData(0.5, 7.5)]
    [InlineData(0.9, 9.5)]
    public void TheShortfallIsTheUniformsTailMean(double p, double tail)
    {
        // QuantLib: 5.625 at 0.05, 9 at 0.9
        Assert.Equal(tail, Uniform().ExpectedShortfall(p), 1e-13);
    }

    [Fact]
    public void ATrancheKeepsTheMassAboveItsDetachment()
    {
        // QuantLib: the tranche on [2, 5] of five buckets, a total probability of 0.6
        Distribution d = Uniform();
        d.Tranche(2.0, 5.0);

        // the last two buckets, [5, 6) and the rest, truncated to the detachment: the uniform's 0.5 above 5
        double mass = Enumerable.Range(0, d.Size).Sum(k => d.Density(k) * d.Dx(k));
        Assert.Equal(1.0, mass, 1e-14);
        double atDetachment = Enumerable.Range(0, d.Size).Where(k => d.X(k) == 3.0).Sum(k => d.Density(k) * d.Dx(k));
        Assert.Equal(0.5, atDetachment, 1e-14);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(0.9)]
    [InlineData(0.99)]
    public void ATranchesShortfallStopsAtItsWidth(double p)
    {
        // QuantLib: 3 at each, its left edge; the tranche on [2, 5] loses 3 with probability 0.5, and the buckets'
        // widths read past it would give 3.5 to 3.9875
        Distribution d = Uniform();
        d.Tranche(2.0, 5.0);
        Assert.Equal(3.0, d.ExpectedShortfall(p), 1e-14);
    }

    [Fact]
    public void ATranchesCumulativeDensityStopsAtItsWidth()
    {
        // uniform within [2, 3) of the tranche, every loss at or below its width 3
        Distribution d = Uniform();
        d.Tranche(2.0, 5.0);
        Assert.Equal(0.45, d.CumulativeDensity(2.5), 1e-14);
        Assert.Equal(1.0, d.CumulativeDensity(3.5), 1e-14);
    }

    [Fact]
    public void TheCumulativeDensityStartsFromZero()
    {
        Assert.Equal(0.05, Uniform().CumulativeDensity(0.5), 1e-15);
        Assert.Equal(0.1, Uniform().CumulativeDensity(1.0), 1e-15);
    }

    [Fact]
    public void TheConvolutionIsTheSumsDistribution()
    {
        // two uniforms on [0, 2] in buckets of 1: the sum's masses 1/4, 1/2, 1/4 in buckets of 1
        var d1 = new Distribution(2, 0.0, 2.0);
        var d2 = new Distribution(2, 0.0, 2.0);
        for (int i = 0; i < 2; i++)
        {
            d1.AddDensity(i, 0.5);
            d2.AddDensity(i, 0.5);
        }

        Distribution sum = ManipulateDistribution.Convolve(d1, d2);

        Assert.Equal(3, sum.Size);
        Assert.Equal(new[] { 0.25, 0.5, 0.25 }, Enumerable.Range(0, 3).Select(k => sum.Density(k) * sum.Dx(k)));
        Assert.Equal(1.0, sum.Cumulative(2), 1e-15);
    }

    [Fact]
    public void TheBinomialDistributionReadsItsVolume()
    {
        // four names of volume 1 at 0.3 on [0, 4]: each count's binomial probability in its bucket
        LossDistResult r = new LossDistBinomial(4, 4.0).EvaluateWithDetails(4, 1.0, 0.3);
        double[] binomial = [0.2401, 0.4116, 0.2646, 0.0756, 0.0081];

        Assert.Equal(1.0, r.Distribution.Cumulative(3), 1e-14);
        for (int i = 0; i <= 4; i++)
        {
            Assert.Equal(binomial[i], r.Probability[i], 1e-14);
        }

        Assert.Equal(binomial[0], r.Distribution.Density(0) * r.Distribution.Dx(0), 1e-14);
        var l = new LossDistBinomial(4, 4.0);
        Assert.Equal(r.Distribution.Cumulative(1), l.Evaluate(4, 1.0, 0.3).Cumulative(1));
        Assert.Equal(r.Distribution.Cumulative(1), l.Evaluate([1.0, 1.0, 1.0, 1.0], [0.3, 0.3, 0.3, 0.3]).Cumulative(1));
    }

    [Fact]
    public void AtLeastNoEventIsCertain()
    {
        Assert.Equal(1.0, LossDist.BinomialProbabilityOfAtLeastNEvents(0, [0.3, 0.3]));
        Assert.Equal(1.0, new BinomialProbabilityOfAtLeastNEvents(0).Evaluate([0.3, 0.3]));
    }

    private static Distribution Uniform()
    {
        var d = new Distribution(10, 0.0, 10.0);
        for (int i = 0; i < 10; i++)
        {
            d.AddDensity(i, 0.1);
        }

        return d;
    }
}
