// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ExponentialJump1dMesher vectors,
/// <c>tests/vectors/methods/finitedifferences/meshers/exponentialjump1dmesher.json</c>:
/// <see cref="ExponentialJump1dMesher"/>'s locations, dplus and dminus at four parameter sets, three sizes and three
/// values of eps.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExponentialJump1dMesherVectorTests
{
    [Fact]
    public void ExponentialJump1dMesherVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/exponentialjump1dmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (double[] locations, double?[] dplus, double?[] dminus) = Read(new ExponentialJump1dMesher(
                c.Int("steps"), c.Double("beta"), c.Double("jumpIntensity"), c.Double("eta"), c.Double("eps")));
            check.Output(c, "locations", locations);
            check.Output(c, "dplus", dplus);
            check.Output(c, "dminus", dminus);
        }

        check.Complete();
    }
}
