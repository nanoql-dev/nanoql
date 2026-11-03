// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The expected shortfall where no sample lies below the value-at-risk's level: QuantLib refuses "no data below the
/// target" (riskstatistics.hpp:196-202). With no loss the level is 0, and the port returns it, the value QuantLib's own
/// comment gives ("must be a loss, i.e., capped at 0.0 and negated", line 203); where the tail is an atom at the level,
/// every tail sample sits at it, and the port returns the level, the value-at-risk, QuantLib's own limit as the atom
/// splits. The average shortfall keeps QuantLib's refusal: a conditional average over no data has no value. QuantLib's
/// checks: its own shortfall and value-at-risk on the same samples, and its expected shortfall on the split atom.
/// </summary>
public class ExpectedShortfallNoLossTests
{
    [Fact]
    public void SamplesWithNoLossHaveNoShortfall()
    {
        // 100 gains: QuantLib's valueAtRisk(0.95) -0.0, shortfall(0) 0, expectedShortfall(0.95) and averageShortfall(0)
        // refuse "no data below the target" (the wheel).
        var s = new GeneralStatistics();
        for (int i = 0; i < 100; i++)
        {
            s.Add(1.0 + (i * 0.01));
        }

        Assert.Equal(0.0, s.ValueAtRisk(0.95));
        Assert.Equal(0.0, s.Shortfall(0.0));
        Assert.Equal(0.0, s.ExpectedShortfall(0.95));
        Assert.Equal("no data below the target", Assert.Throws<NanoQLException>(() => s.AverageShortfall(0.0)).Message);
    }

    [Fact]
    public void ATailThatIsAnAtomHasTheAtomsShortfall()
    {
        foreach (int losses in new[] { 6, 10 })
        {
            // QuantLib's valueAtRisk(0.95) 10 and expectedShortfall(0.95) refused "no data below the target" (the wheel).
            var s = new GeneralStatistics();
            for (int k = 0; k < losses; k++)
            {
                s.Add(-10.0);
            }

            for (int i = 0; i < 100 - losses; i++)
            {
                s.Add(1.0 + (i * 0.01));
            }

            Assert.Equal(10.0, s.ValueAtRisk(0.95));
            Assert.Equal(10.0, s.ExpectedShortfall(0.95));

            // The atom split, its losses -10 - k·eps: QuantLib's expected shortfall (the wheel) tends to the atom's 10
            // as eps falls, and the port's on the split sets is QuantLib's.
            foreach ((double eps, double quantLib) in losses == 6
                         ? new[] { (1e-3, 10.003), (1e-6, 10.000003), (1e-9, 10.000000003) }
                         : new[] { (1e-3, 10.007000000000001), (1e-6, 10.000007), (1e-9, 10.000000007) })
            {
                var split = new GeneralStatistics();
                for (int k = 0; k < losses; k++)
                {
                    split.Add(-10.0 - (eps * k));
                }

                for (int i = 0; i < 100 - losses; i++)
                {
                    split.Add(1.0 + (i * 0.01));
                }

                double es = split.ExpectedShortfall(0.95);
                Assert.Equal(quantLib, es, 1e-14);
                Assert.True(Math.Abs(es - 10.0) <= losses * eps);
            }
        }
    }
}
