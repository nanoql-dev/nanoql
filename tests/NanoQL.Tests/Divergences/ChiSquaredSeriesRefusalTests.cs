// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="NonCentralCumulativeChiSquareDistribution"/> past its series' range. When QuantLib's loop ends at
/// <c>itrmax</c> with its bound still negative, the bound passes as converged (<c>chisquaredistribution.cpp:79-88</c>),
/// so above df + 20,000 a partial sum returns; when the first term falls below the normal range (<c>:54-55</c>), the
/// series runs on subnormal numbers, then zeros; and when the Poisson weight's start, exp(−ncp/2) (<c>:39-41</c>),
/// falls below it, from ncp 1,416.8, every term carries fewer bits, and from ncp 1,490.3 every term is 0, which the
/// stop (<c>:82</c>) passes as converged. nanoql refuses each: QuantLib's own "didn't converge" for the first, and a
/// text naming the term for the other two. The subnormal first term and the subnormal weight below pass a check
/// against zero alone, so the refusals test "outside the range of doubles" as below the normal range; at ncp 1,500 the
/// weight is exactly zero.
/// </summary>
public class ChiSquaredSeriesRefusalTests
{
    [Fact]
    public void ASubnormalFirstTermIsRefused()
    {
        // QuantLib reads 1.8101376060711385 at df 10, x 1,547, where the answer is 1
        var e = Assert.Throws<NanoQLException>(
            () => new NonCentralCumulativeChiSquareDistribution(10.0, 0.0).Evaluate(1547.0));
        Assert.Equal("the series' first term is outside the range of doubles at x 1547", e.Message);
    }

    [Fact]
    public void ASeriesStoppedUnconvergedAboveDfIsRefused()
    {
        // QuantLib reads 2.062390703966763e-05 at df 1.2e15, x 1.20000012e15, 2.4 standard deviations above df
        var e = Assert.Throws<NanoQLException>(
            () => new NonCentralCumulativeChiSquareDistribution(1.2e15, 0.0).Evaluate(1.20000012e15));
        Assert.Equal("didn't converge", e.Message);
    }

    [Fact]
    public void ASeriesStoppedUnconvergedInTheLargeDfWindowIsRefused()
    {
        // Above df 1.1e15 and within 1.5e-8 of df, relatively, the series starts from QuantLib's large-df term
        // (chisquaredistribution.cpp:48-51). QuantLib reads 0.0012036176136598462 at df 1.2e15, x 1.200000012e15, 0.24
        // standard deviations above df; at x = df its bound stays above errmax, and it refuses as nanoql does
        var e = Assert.Throws<NanoQLException>(
            () => new NonCentralCumulativeChiSquareDistribution(1.2e15, 0.0).Evaluate(1.200000012e15));
        Assert.Equal("didn't converge", e.Message);
        e = Assert.Throws<NanoQLException>(
            () => new NonCentralCumulativeChiSquareDistribution(1.2e15, 0.0).Evaluate(1.2e15));
        Assert.Equal("didn't converge", e.Message);
    }

    [Fact]
    public void ASubnormalPoissonWeightIsRefused()
    {
        // QuantLib reads 0.5051744561775329 at df 30, ncp 1,470, x 1,500, where its Sankaran approximation reads
        // 0.505159
        var e = Assert.Throws<NanoQLException>(
            () => new NonCentralCumulativeChiSquareDistribution(30.0, 1470.0).Evaluate(1500.0));
        Assert.Equal("the series' Poisson weight exp(-ncp/2) is outside the range of doubles at ncp 1470", e.Message);
        Assert.Equal(0.505159, new NonCentralCumulativeChiSquareSankaranApprox(30.0, 1470.0).Evaluate(1500.0), 5e-6);
    }

    [Fact]
    public void AZeroPoissonWeightIsRefused()
    {
        // QuantLib reads 0.0 at df 30, ncp 1,500, x 1,530, the mean, where its Sankaran approximation reads 0.5051
        var e = Assert.Throws<NanoQLException>(
            () => new NonCentralCumulativeChiSquareDistribution(30.0, 1500.0).Evaluate(1530.0));
        Assert.Equal("the series' Poisson weight exp(-ncp/2) is outside the range of doubles at ncp 1500", e.Message);
        Assert.Equal(0.5051, new NonCentralCumulativeChiSquareSankaranApprox(30.0, 1500.0).Evaluate(1530.0), 5e-5);
    }
}
