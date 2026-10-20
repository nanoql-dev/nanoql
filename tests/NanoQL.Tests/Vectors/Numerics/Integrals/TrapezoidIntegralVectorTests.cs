// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="TrapezoidIntegral{TIntegrationPolicy}"/>'s vectors over <see cref="Default"/>,
/// <c>tests/vectors/math/integrals/trapezoidintegral.json</c>: integrals and evaluation counts of named integrands over
/// intervals forward, reversed and empty, at two accuracies and with too few refinements, and the refusal of an
/// accuracy not above machine epsilon.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TrapezoidIntegralVectorTests
{
    [Fact]
    public void TrapezoidIntegralVector()
    {
        var file = VectorFile.Load("math/integrals/trapezoidintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            IntegralInputs.CheckIntegrals(
                check, c, () => new TrapezoidIntegral<Default>(c.Double("accuracy"), c.Int("maxIterations")));
        }

        check.Complete();
    }
}
