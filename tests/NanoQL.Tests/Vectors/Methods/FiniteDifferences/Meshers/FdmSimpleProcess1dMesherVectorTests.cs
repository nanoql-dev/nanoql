// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmSimpleProcess1dMesher vectors, <c>tests/vectors/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.json</c>:
/// <see cref="FdmSimpleProcess1dMesher"/>'s locations, dplus and dminus on Ornstein-Uhlenbeck and Black-Scholes
/// processes, over sizes, maturities, averaging steps, epsilons and mandatory points.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmSimpleProcess1dMesherVectorTests
{
    [Fact]
    public void FdmSimpleProcess1dMesherVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/fdmsimpleprocess1dmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckMesher1d(check, c);
        }

        check.Complete();
    }
}
