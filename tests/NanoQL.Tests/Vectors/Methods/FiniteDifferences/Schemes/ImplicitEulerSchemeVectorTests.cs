// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ImplicitEulerScheme vectors, <c>tests/vectors/methods/finitedifferences/schemes/impliciteulerscheme.json</c>:
/// rollbacks through <see cref="FdmBackwardSolver"/> under the implicit Euler description, on the Black-Scholes
/// operator, and the refusal of a step towards negative time; and the scheme stepped on its own, with its
/// <see cref="ImplicitEulerScheme.NumberOfIterations"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ImplicitEulerSchemeVectorTests
{
    [Fact]
    public void ImplicitEulerSchemeVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/schemes/impliciteulerscheme");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("scheme", out _))
            {
                CheckDirectScheme(check, c, op =>
                {
                    var scheme = new ImplicitEulerScheme(op);
                    return (scheme, () => scheme.NumberOfIterations);
                });
                continue;
            }

            CheckRollback(check, c);
        }

        check.Complete();
    }
}
