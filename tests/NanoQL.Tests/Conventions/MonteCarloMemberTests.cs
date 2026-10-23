// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo framework's public members that no vector or QuantLib case reaches, each against a form the vectors
/// check: the statistics' unweighted sequences, the samples one at a time, and a Sobol multi-path generator's copy,
/// which goes on from its original's state.
/// </summary>
public class MonteCarloMemberTests
{
    private static readonly double[] Data = [0.3, -1.2, 2.5, 0.7, 0.7, -0.4, 1.9, 0.05];

    [Fact]
    public void AnUnweightedSequenceIsItsSamplesAddedInTurn()
    {
        var general = new GeneralStatistics();
        var generalOneByOne = new GeneralStatistics();
        var incremental = new IncrementalStatistics();
        var incrementalOneByOne = new IncrementalStatistics();
        general.AddSequence(Data);
        incremental.AddSequence(Data);
        foreach (double x in Data)
        {
            generalOneByOne.Add(x);
            incrementalOneByOne.Add(x);
        }

        Assert.Equal(generalOneByOne.Samples, general.Samples);
        Assert.Equal(generalOneByOne.Mean(), general.Mean());
        Assert.Equal(generalOneByOne.Variance(), general.Variance());
        Assert.Equal(generalOneByOne.Skewness(), general.Skewness());
        Assert.Equal(incrementalOneByOne.Samples, incremental.Samples);
        Assert.Equal(incrementalOneByOne.Mean(), incremental.Mean());
        Assert.Equal(incrementalOneByOne.Variance(), incremental.Variance());
        Assert.Equal(incrementalOneByOne.Skewness(), incremental.Skewness());
    }

    [Fact]
    public void ASobolGeneratorsCopyGoesOnFromItsState()
    {
        var model = new G2(new FlatForward(new Date(15, Month.January, 2025), 0.03, new Actual365Fixed()), 0.07, 0.011, 0.3, 0.008, -0.6);
        var times = new TimeGrid(1.0, 4);
        int dim = 2 * (times.Size - 1);
        var generator = new GaussianSobolMultiPathGenerator(
            model.Dynamics().Process(), times, LowDiscrepancy.MakeSequenceGenerator(dim, 42));
        _ = generator.Next();
        GaussianSobolMultiPathGenerator copy = generator.Clone();
        for (int k = 0; k < 3; ++k)
        {
            MultiPath expected = generator.Next().Value, actual = copy.Next().Value;
            for (int j = 0; j < 2; ++j)
            {
                for (int i = 0; i < times.Size; ++i)
                {
                    Assert.Equal(expected[j][i], actual[j][i]);
                }
            }
        }
    }
}
