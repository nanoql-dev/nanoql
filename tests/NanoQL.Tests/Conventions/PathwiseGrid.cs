// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The pathwise engines' facts' grids: six semi-annual rates from 0.5, six unequal periods from 0.25 (the S-mmp plan's
/// MPP7, MPP8), and the semi-annual rates paid 0.2 before their periods' ends; forwards 3% rising 0.1% a period, the
/// evolution at every rate time but the last.
/// </summary>
internal sealed class PathwiseGrid
{
    private PathwiseGrid(double[] rateTimes, double[] paymentTimes)
    {
        RateTimes = rateTimes;
        PaymentTimes = paymentTimes;
        Accruals = [.. Enumerable.Range(0, rateTimes.Length - 1).Select(i => rateTimes[i + 1] - rateTimes[i])];
        Forwards = [.. Enumerable.Range(0, rateTimes.Length - 1).Select(i => 0.03 + (0.001 * i))];
        Evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
    }

    public double[] RateTimes { get; }

    public double[] Accruals { get; }

    public double[] PaymentTimes { get; }

    public double[] Forwards { get; }

    public EvolutionDescription Evolution { get; }

    public static PathwiseGrid Of(string grid)
    {
        double[] semi = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5];
        double[] unequal = [0.25, 0.75, 1.0, 1.75, 2.0, 2.6, 3.0];
        return grid switch
        {
            "unequal" => new(unequal, unequal[1..]),
            "paid inside" => new(semi, [.. semi[1..].Select(t => t - 0.2)]),
            _ => new(semi, semi[1..]),
        };
    }

    // FlatVol 20%, 3 factors, on the given forwards.
    public FlatVol FlatVol(double[] forwards, double displacement)
    {
        Matrix corr = ExpCorrelationsFunctions.ExponentialCorrelations(RateTimes, 0.5, 0.2);
        var c = new TimeHomogeneousForwardCorrelation(corr, RateTimes);
        return new FlatVol(
            [.. Enumerable.Repeat(0.20, forwards.Length)], c, Evolution, 3, forwards, [.. Enumerable.Repeat(displacement, forwards.Length)]);
    }
}
