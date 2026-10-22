// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The IncrementalStatistics vectors, <c>tests/vectors/math/statistics/incrementalstatistics.json</c>:
/// <see cref="IncrementalStatistics"/>'s members on generalstatistics.py's data sets, Boost.Accumulators' arithmetic
/// in Boost's order (the large-mean sets read the weighted variance on its lazy mean), then after <c>Reset</c>; and
/// <c>Add</c>'s refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IncrementalStatisticsVectorTests
{
    [Fact]
    public void IncrementalStatisticsVector() =>
        GeneralStatisticsVectorTests.Check<IncrementalStatistics>(
            VectorFile.Load("math/statistics/incrementalstatistics"));
}
