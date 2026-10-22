// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/gaussianstatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A helper class for precomputed distributions: a mean and a standard deviation, to which the Gaussian risk measures
/// of <see cref="GaussianStatisticsExtensions"/> apply, as QuantLib's <c>GenericGaussianStatistics&lt;StatsHolder&gt;</c>.
/// </summary>
public sealed class StatsHolder : ISummaryStatistics
{
    private readonly double mean;
    private readonly double standardDeviation;

    /// <summary>Initializes a new instance of the <see cref="StatsHolder"/> class.</summary>
    /// <param name="mean">The mean.</param>
    /// <param name="standardDeviation">The standard deviation.</param>
    public StatsHolder(double mean, double standardDeviation)
    {
        this.mean = mean;
        this.standardDeviation = standardDeviation;
    }

    /// <inheritdoc/>
    public double Mean() => mean;

    /// <inheritdoc/>
    public double StandardDeviation() => standardDeviation;
}
