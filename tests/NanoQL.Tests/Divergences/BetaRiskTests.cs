// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// BetaRisk's event process (the S-xcr plan's XR8 (xlii)): the inter-arrival times cumulated, a Poisson process,
/// where QuantLib draws each event's time afresh, a geometric count whose dates come unordered. At a two-year return
/// period over three years (2 January 2015 to 2018, maximum loss 100, mean 10, standard deviation 15) QuantLib's
/// mean count reads 3.48466 for λT = 1.5, and 51,727 of 10⁵ paths unordered; on <c>testBetaRisk</c>'s risk (return
/// period 100 years) QuantLib reads a count mean of 0.030433 and variance 0.031354832510999998, a loss mean of
/// 0.30134899766945406 and variance 9.7413702370590478 over 10⁶ paths, its checks at 1% passing on its seed (the S-xcr
/// run's <c>c4_facts.cpp</c>). QuantLib's draws are kept up to the parting path: of its first 24 paths with events
/// (the S-xcr plan's XP16, <c>probes\xp_betarisk.cpp</c>), only the two with two events move, their second date alone.
/// </summary>
public class BetaRiskTests
{
    private static readonly Date Start = new(2, January, 2015);
    private static readonly Date End = new(2, January, 2018);

    [Fact]
    public void TheCountIsPoissonsAtAHighRate()
    {
        // 10^5 paths at lambda T = 1.5: the mean within three standard errors (sqrt(1.5 / n)), the variance within three
        // of its own (sqrt((mu4 - sigma^4) / n), mu4 = lambda T (1 + 3 lambda T) for a Poisson count)
        const int paths = 100000;
        CatSimulation simulation = new BetaRisk(100.0, 2.0, 10.0, 15.0).NewSimulation(Start, End);
        var path = new List<KeyValuePair<Date, double>>();
        double sum = 0.0, sumSquares = 0.0;
        for (int i = 0; i < paths; ++i)
        {
            Assert.True(simulation.NextPath(path));
            for (int j = 1; j < path.Count; ++j)
            {
                Assert.True(path[j - 1].Key <= path[j].Key);
            }

            sum += path.Count;
            sumSquares += path.Count * path.Count;
        }

        double lambdaT = 0.5 * new ActualActual(ActualActualConvention.ISDA).YearFraction(Start, End);
        double mean = sum / paths;
        double variance = (sumSquares / paths) - (mean * mean);

        // QuantLib: 3.48466
        Assert.Equal(lambdaT, mean, 3.0 * Math.Sqrt(lambdaT / paths));
        Assert.Equal(lambdaT, variance, 3.0 * Math.Sqrt(((lambdaT * (1 + (3 * lambdaT))) - (lambdaT * lambdaT)) / paths));
    }

    [Fact]
    public void TheCountIsPoissonsOnTestBetaRisksRisk()
    {
        // 10^6 paths at lambda T = 0.03: the mean and variance within three standard errors, where testBetaRisk checks
        // them at 2% and 5% (QuantLib: 0.030433 and 0.031354832510999998, 2.5 and 7.6 of these errors off)
        const int paths = 1000000;
        CatSimulation simulation = new BetaRisk(100.0, 100.0, 10.0, 15.0).NewSimulation(Start, End);
        var path = new List<KeyValuePair<Date, double>>();
        double sum = 0.0, sumSquares = 0.0;
        for (int i = 0; i < paths; ++i)
        {
            simulation.NextPath(path);
            sum += path.Count;
            sumSquares += path.Count * path.Count;
        }

        double lambdaT = 0.01 * new ActualActual(ActualActualConvention.ISDA).YearFraction(Start, End);
        double mean = sum / paths;
        double variance = (sumSquares / paths) - (mean * mean);
        Assert.Equal(lambdaT, mean, 3.0 * Math.Sqrt(lambdaT / paths));
        Assert.Equal(lambdaT, variance, 3.0 * Math.Sqrt(((lambdaT * (1 + (3 * lambdaT))) - (lambdaT * lambdaT)) / paths));
    }

    [Fact]
    public void AMultiEventPathMovesItsLaterDatesOnly()
    {
        // QuantLib's path 42 on testBetaRisk's risk: 42109 21.445890339876595, 42661 1.9661501514045288
        CatSimulation simulation = new BetaRisk(100.0, 100.0, 10.0, 15.0).NewSimulation(Start, End);
        var path = new List<KeyValuePair<Date, double>>();
        for (int i = 0; i <= 42; ++i)
        {
            simulation.NextPath(path);
            if (i == 15)
            {
                // a one-event path is QuantLib's
                Assert.Equal((42181, 12.339526753548526), (path.Single().Key.SerialNumber, path.Single().Value));
            }
        }

        Assert.Equal(2, path.Count);
        Assert.Equal((42109, 21.445890339876595), (path[0].Key.SerialNumber, path[0].Value));
        Assert.Equal(1.9661501514045288, path[1].Value);
        Assert.Equal(42764, path[1].Key.SerialNumber);
    }
}
