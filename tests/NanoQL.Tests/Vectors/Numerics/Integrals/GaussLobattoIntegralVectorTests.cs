// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="GaussLobattoIntegral"/>'s vectors, <c>tests/vectors/math/integrals/gausslobattointegral.json</c>:
/// integrals and evaluation counts of named integrands over intervals forward, reversed and empty, under absolute and
/// relative accuracies, with and without the convergence estimate and with too few evaluations; an integrand whose
/// Kronrod estimate is zero, refused; and the refusal of an accuracy not above machine epsilon.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussLobattoIntegralVectorTests
{
    [Fact]
    public void GaussLobattoIntegralVector()
    {
        var file = VectorFile.Load("math/integrals/gausslobattointegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double? relAccuracy = c.Input("relAccuracy").ValueKind == System.Text.Json.JsonValueKind.Null
                ? null
                : c.Double("relAccuracy");
            IntegralInputs.CheckIntegrals(
                check,
                c,
                () => new GaussLobattoIntegral(
                    c.Int("maxIterations"), c.Double("absAccuracy"), relAccuracy, c.Bool("useConvergenceEstimate")));
        }

        check.Complete();
    }
}
