// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catrisk.hpp ql/experimental/catbonds/catrisk.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// A catastrophe risk whose events arrive as a Poisson process of a return period and whose losses follow a beta
/// distribution of a mean and standard deviation, scaled to a maximum loss.
/// </summary>
/// <remarks>See <see cref="BetaRiskSimulation"/> for the draws.</remarks>
[Experimental("NQL0001")]
public sealed class BetaRisk : CatRisk
{
    private readonly double maxLoss;
    private readonly double lambda;
    private readonly double alpha;
    private readonly double beta;

    /// <summary>Initializes a new instance of the <see cref="BetaRisk"/> class.</summary>
    /// <param name="maxLoss">The maximum loss.</param>
    /// <param name="years">The events' return period, in years.</param>
    /// <param name="mean">The losses' mean.</param>
    /// <param name="stdDev">The losses' standard deviation.</param>
    /// <exception cref="ArgumentException">The mean isn't below the maximum loss, or the standard deviation can't be
    /// a beta distribution's of that mean.</exception>
    public BetaRisk(double maxLoss, double years, double mean, double stdDev)
    {
        this.maxLoss = maxLoss;
        lambda = 1.0 / years;
        if (!(mean < maxLoss))
        {
            throw new ArgumentException(
                $"Mean {DataFormattersFunctions.General(mean)}of the loss distribution must be less than the maximum loss "
                + DataFormattersFunctions.General(maxLoss),
                nameof(mean));
        }

        double normalizedMean = mean / maxLoss;
        double normalizedVar = stdDev * stdDev / (maxLoss * maxLoss);
        if (!(normalizedVar < normalizedMean * (1.0 - normalizedMean)))
        {
            throw new ArgumentException(
                $"Standard deviation of {DataFormattersFunctions.General(stdDev)} is impossible to achieve in gamma "
                + $"distribution with mean {DataFormattersFunctions.General(mean)}",
                nameof(stdDev));
        }

        double nu = (normalizedMean * (1.0 - normalizedMean) / normalizedVar) - 1.0;
        alpha = normalizedMean * nu;
        beta = (1.0 - normalizedMean) * nu;
    }

    /// <inheritdoc/>
    public override CatSimulation NewSimulation(Date start, Date end) =>
        new BetaRiskSimulation(start, end, maxLoss, lambda, alpha, beta);
}
