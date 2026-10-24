// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="LagrangeInterpolation"/>'s harness vectors,
/// <c>tests/vectors/cxx/math/interpolations/lagrangeinterpolation.json</c>: <see cref="LagrangeInterpolation.Value"/>,
/// the interpolation of other y values on the same nodes, on seven node sets at points inside and outside the range.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LagrangeInterpolationCxxVectorTests
{
    [Fact]
    public void LagrangeInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/lagrangeinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var interpolation = new LagrangeInterpolation(Doubles(c.Input("x")), Doubles(c.Input("y")));
            double[] other = Doubles(c.Input("otherY"));
            check.Output(c, "value", () => Doubles(c.Input("points")).Select(z => interpolation.Value(other, z)).ToArray());
        }

        check.Complete();
    }
}
