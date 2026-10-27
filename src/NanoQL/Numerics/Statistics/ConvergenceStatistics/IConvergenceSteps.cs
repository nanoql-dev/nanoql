// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/convergencestatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The sampling rule of <see cref="ConvergenceStatistics{TStats, TRule}"/>: the numbers of samples at which it stores
/// the mean, QuantLib's interface for its second template parameter.
/// </summary>
public interface IConvergenceSteps
{
    /// <summary>Gets the number of samples at which the first mean is stored.</summary>
    int InitialSamples { get; }

    /// <summary>The number of samples at which the next mean is stored.</summary>
    /// <param name="current">The number of samples at which the last mean was stored.</param>
    /// <returns>The next number of samples.</returns>
    int NextSamples(int current);
}
