// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="SegmentIntegral"/>'s vectors, <c>tests/vectors/math/integrals/segmentintegral.json</c>: integrals and
/// evaluation counts of named integrands over intervals forward, reversed and empty, on one to 1,000 intervals, and the
/// refusal of none.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SegmentIntegralVectorTests
{
    [Fact]
    public void SegmentIntegralVector()
    {
        var file = VectorFile.Load("math/integrals/segmentintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            IntegralInputs.CheckIntegrals(check, c, () => new SegmentIntegral(c.Int("intervals")));
        }

        check.Complete();
    }
}
