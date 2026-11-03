// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/sequencestatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The default statistics of N-dimensional samples, each component a <see cref="GeneralStatistics"/>, which keeps its
/// samples: QuantLib's <c>SequenceStatistics</c>, <c>GenericSequenceStatistics&lt;Statistics&gt;</c>, with the
/// empirical risk measures of <see cref="GenericSequenceStatisticsExtensions"/>.
/// </summary>
/// <remarks>
/// Unsealed, an extension point: QuantLib's <c>DiscrepancyStatistics</c> derives from it.
/// </remarks>
public class SequenceStatistics : GenericSequenceStatistics<GeneralStatistics>
{
    /// <summary>Initializes a new instance of the <see cref="SequenceStatistics"/> class.</summary>
    /// <param name="dimension">The samples' dimension; 0 to take it from the first sample.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is negative.</exception>
    public SequenceStatistics(int dimension = 0)
        : base(dimension)
    {
    }
}
