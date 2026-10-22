// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/gaussianstatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A distribution's mean and standard deviation: what QuantLib's Gaussian risk measures read of the statistics tool
/// they wrap, <c>GenericGaussianStatistics&lt;Stat&gt;</c>'s <c>Stat</c>. Every accumulator has them
/// (<see cref="IStatistics"/>), and so does <see cref="StatsHolder"/>, which holds two precomputed values.
/// </summary>
/// <remarks>
/// The members of <see cref="GaussianStatisticsExtensions"/> apply to any implementation, as QuantLib's template does
/// to any <c>Stat</c> with the two members.
/// </remarks>
public interface ISummaryStatistics
{
    /// <summary>The mean.</summary>
    /// <returns>The mean.</returns>
    double Mean();

    /// <summary>The standard deviation.</summary>
    /// <returns>The standard deviation.</returns>
    double StandardDeviation();
}
