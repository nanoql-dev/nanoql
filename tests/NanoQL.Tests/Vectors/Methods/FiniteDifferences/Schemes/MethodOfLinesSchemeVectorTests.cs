// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MethodOfLinesScheme vectors, <c>tests/vectors/methods/finitedifferences/schemes/methodoflinesscheme.json</c>:
/// rollbacks through <see cref="FdmBackwardSolver"/> under method-of-lines descriptions, on the Black-Scholes operator,
/// and the refusal of a step towards negative time.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MethodOfLinesSchemeVectorTests
{
    [Fact]
    public void MethodOfLinesSchemeVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/schemes/methodoflinesscheme");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckRollback(check, c);
        }

        check.Complete();
    }
}
