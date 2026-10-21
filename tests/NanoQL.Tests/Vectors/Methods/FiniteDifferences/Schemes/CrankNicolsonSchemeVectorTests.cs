// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CrankNicolsonScheme vectors, <c>tests/vectors/methods/finitedifferences/schemes/cranknicolsonscheme.json</c>:
/// rollbacks through <see cref="FdmBackwardSolver"/> under Crank-Nicolson descriptions, on the Black-Scholes operator,
/// and the refusal of a step towards negative time; and the scheme stepped on its own, with its
/// <see cref="CrankNicolsonScheme.NumberOfIterations"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CrankNicolsonSchemeVectorTests
{
    [Fact]
    public void CrankNicolsonSchemeVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/schemes/cranknicolsonscheme");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("scheme", out _))
            {
                CheckDirectScheme(check, c, op =>
                {
                    var scheme = new CrankNicolsonScheme(0.5, op);
                    return (scheme, () => scheme.NumberOfIterations);
                });
                continue;
            }

            CheckRollback(check, c);
        }

        check.Complete();
    }
}
