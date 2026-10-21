// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Uniform1dMesher vectors, <c>tests/vectors/methods/finitedifferences/meshers/uniform1dmesher.json</c>:
/// <see cref="Uniform1dMesher"/>'s locations, dplus and dminus over seven intervals and six sizes, and the refusals of
/// an end at or below the start.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Uniform1dMesherVectorTests
{
    [Fact]
    public void Uniform1dMesherVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/uniform1dmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckMesher1d(check, c);
        }

        check.Complete();
    }
}
