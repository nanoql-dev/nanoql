// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Concentrating1dMesher vectors,
/// <c>tests/vectors/methods/finitedifferences/meshers/concentrating1dmesher.json</c>:
/// <see cref="Concentrating1dMesher"/>'s locations, dplus and dminus from both constructors (one critical point, with
/// and without its requirement; several points, through the differential equation), and their refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Concentrating1dMesherVectorTests
{
    [Fact]
    public void Concentrating1dMesherVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/concentrating1dmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckMesher1d(check, c);
        }

        check.Complete();
    }
}
