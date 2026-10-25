// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmCEV1dMesher vectors, <c>tests/vectors/methods/finitedifferences/meshers/fdmcev1dmesher.json</c>:
/// <see cref="FdmCEV1dMesher"/>'s locations, dplus and dminus over elasticities on both sides of 1 and below 0,
/// maturities, sizes, probabilities, scale factors and critical points.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmCEV1dMesherVectorTests
{
    [Fact]
    public void FdmCEV1dMesherVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/fdmcev1dmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckMesher1d(check, c);
        }

        check.Complete();
    }
}
