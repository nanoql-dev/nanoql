// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="ForwardFlatInterpolation"/>'s vectors,
/// <c>tests/vectors/math/interpolations/forwardflatinterpolation.json</c>: on nine node sets, the value, derivative and
/// second derivative at 50 points inside and outside the range, and the primitive at the points up to the last node;
/// and a set of one point, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ForwardFlatInterpolationVectorTests
{
    [Fact]
    public void ForwardFlatInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/forwardflatinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = InterpolationInputs.Numbers(c.Input("x")), y = InterpolationInputs.Numbers(c.Input("y"));
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => new ForwardFlatInterpolation(x, y) is null ? 1 : 0);
                continue;
            }

            var f = new ForwardFlatInterpolation(x, y);
            double[] points = InterpolationInputs.Numbers(c.Input("points"));
            bool[] allow = InterpolationInputs.Bools(c.Input("allowExtrapolation"));
            int[] primitivePoints = c.Ints("primitivePoints");
            IEnumerable<int> all = Enumerable.Range(0, points.Length);
            check.Output(c, "value", InterpolationInputs.Deferred(all, k => f.Evaluate(points[k], allow[k])));
            check.Output(c, "derivative", InterpolationInputs.Deferred(all, k => f.Derivative(points[k], allow[k])));
            check.Output(
                c, "secondDerivative", InterpolationInputs.Deferred(all, k => f.SecondDerivative(points[k], allow[k])));
            check.Output(
                c, "primitive", InterpolationInputs.Deferred(primitivePoints, k => f.Primitive(points[k], allow[k])));
        }

        check.Complete();
    }
}
