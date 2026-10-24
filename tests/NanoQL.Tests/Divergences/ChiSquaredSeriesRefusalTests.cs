// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="NonCentralCumulativeChiSquareDistribution"/> past QuantLib 1.44's series' range. When its first term
/// falls below the normal range (<c>chisquaredistribution.cpp:54-55</c>), the series runs on subnormal numbers, then
/// zeros; when the Poisson weight's start, exp(−ncp/2) (<c>:39-41</c>), falls below it, from ncp 1,416.8, every term
/// carries fewer bits, and from ncp 1,490.3 every term is 0, which the stop (<c>:82</c>) passes as converged; and when
/// its loop ends at <c>itrmax</c> with its bound still negative, the bound passes as converged (<c>:79-88</c>), so above
/// df + 20,000 a partial sum returns. nanoql takes QuantLib's master's series, upstream's #2908 (the S-van plan's VR11):
/// it skips the underflowing weights and terms by their logarithms, so the first two return the distribution's values,
/// Boost's within upstream's 1e-10; and it refuses a loop that ends before its bound can be computed, "didn't converge",
/// as the S-eq port refused it.
/// </summary>
public class ChiSquaredSeriesRefusalTests
{
    [Theory]
    [InlineData(10.0, 0.0, 1547.0, 1.8101376060711385)] // the first term subnormal: the answer is 1
    [InlineData(30.0, 1470.0, 1500.0, 0.5051744561775329)] // the Poisson weight subnormal
    [InlineData(30.0, 1500.0, 1530.0, 0.0)] // the Poisson weight 0, at the mean
    public void PastQuantLibsRangeTheSeriesIsUpstreams(double df, double ncp, double x, double quantLib)
    {
        // QuantLib 1.44's values recorded; #2908's against Boost's distribution, upstream's check
        _ = quantLib;
        var boostCdf = BoostInternals.Function<Func<double, double, double, double>>("NonCentralChiSquaredFunctions", "Cdf");
        double value = new NonCentralCumulativeChiSquareDistribution(df, ncp).Evaluate(x);
        Assert.True(Math.Abs(value - boostCdf(df, ncp, x)) <= 1e-10, $"{value} against Boost's {boostCdf(df, ncp, x)}");
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
}
