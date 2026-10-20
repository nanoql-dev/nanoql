// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="SimpsonIntegral"/>'s vectors, <c>tests/vectors/math/integrals/simpsonintegral.json</c>: integrals and
/// evaluation counts of named integrands over intervals forward, reversed and empty, at two accuracies and with too few
/// refinements, and the refusal of an accuracy not above machine epsilon.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SimpsonIntegralVectorTests
{
    [Fact]
    public void SimpsonIntegralVector()
    {
        var file = VectorFile.Load("math/integrals/simpsonintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            IntegralInputs.CheckIntegrals(check, c, () => new SimpsonIntegral(c.Double("accuracy"), c.Int("maxIterations")));
        }

        check.Complete();
    }
}
