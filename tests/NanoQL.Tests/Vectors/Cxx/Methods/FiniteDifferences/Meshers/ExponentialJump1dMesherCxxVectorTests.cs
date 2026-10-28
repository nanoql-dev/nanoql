// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's jump-size vectors, <c>tests/vectors/cxx/methods/finitedifferences/meshers/
/// exponentialjump1dmesher.json</c>: <see cref="ExponentialJump1dMesher"/>'s stationary density and distribution and
/// those at a time t, at four parameter sets with βt from 0.01 to 100, where QuantLib's integral of its density
/// matches the closed form nanoql integrates, and QuantLib's density at t the closed form's derivative. The wheel's
/// mesher doesn't expose them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExponentialJump1dMesherCxxVectorTests
{
    [Fact]
    public void ExponentialJump1dMesherCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/meshers/exponentialjump1dmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var m = new ExponentialJump1dMesher(10, c.Double("beta"), c.Double("jumpIntensity"), c.Double("eta"), 1e-3);
            double[] xs = c.Doubles("x");
            double t = c.Double("t");
            check.Output(c, "density", xs.Select(x => m.JumpSizeDensity(x)).ToArray());
            check.Output(c, "densityAt", xs.Select(x => m.JumpSizeDensity(x, t)).ToArray());
            check.Output(c, "distribution", xs.Select(x => m.JumpSizeDistribution(x)).ToArray());
            check.Output(c, "distributionAt", xs.Select(x => m.JumpSizeDistribution(x, t)).ToArray());
        }

        check.Complete();
    }
}
