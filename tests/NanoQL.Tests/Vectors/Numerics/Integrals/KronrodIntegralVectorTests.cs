// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Gauss-Kronrod rules' vectors, <c>tests/vectors/math/integrals/kronrodintegral.json</c>:
/// <see cref="GaussKronrodAdaptive"/> and <see cref="GaussKronrodNonAdaptive"/> on named integrands over intervals
/// forward, reversed and empty, each integral with its evaluation count, the adaptive rule's refusal of too few
/// evaluations, and the constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class KronrodIntegralVectorTests
{
    [Fact]
    public void KronrodIntegralVector()
    {
        var file = VectorFile.Load("math/integrals/kronrodintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            IntegralInputs.CheckIntegrals(check, c, () => c.String("rule") == "adaptive"
                ? new GaussKronrodAdaptive(c.Double("tolerance"), c.Int("maxEvaluations"))
                : new GaussKronrodNonAdaptive(c.Double("tolerance"), c.Int("maxEvaluations"), c.Double("relativeAccuracy")));
        }

        check.Complete();
    }
}
