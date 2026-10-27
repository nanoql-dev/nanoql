// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/sequencestatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Statistics of N-dimensional samples, each component an <see cref="IncrementalStatistics"/>, which keeps running sums
/// and no samples: QuantLib's <c>SequenceStatisticsInc</c>,
/// <c>GenericSequenceStatistics&lt;IncrementalStatistics&gt;</c> (D12). The market-model accounting engines add their
/// paths' values into it.
/// </summary>
public sealed class SequenceStatisticsInc : GenericSequenceStatistics<IncrementalStatistics>
{
    /// <summary>Initializes a new instance of the <see cref="SequenceStatisticsInc"/> class.</summary>
    /// <param name="dimension">The samples' dimension; 0 to take it from the first sample.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is negative.</exception>
    public SequenceStatisticsInc(int dimension = 0)
        : base(dimension)
    {
    }
}
