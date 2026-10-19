// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="BicubicSpline"/>'s vectors, <c>tests/vectors/math/interpolations/bicubicsplineinterpolation.json</c>: on
/// twenty-three grids, three of them built by the <see cref="Bicubic"/> factory, the value at 50 points inside and outside
/// the grid, with and without extrapolation; and the grids QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BicubicSplineInterpolationVectorTests
{
    [Fact]
    public void BicubicSplineInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/bicubicsplineinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = InterpolationInputs.Numbers(c.Input("x")), y = InterpolationInputs.Numbers(c.Input("y"));
            Matrix z = InterpolationInputs.MatrixOf(c.Input("z"));
            Interpolation2D Build() => c.Bool("factory") ? new Bicubic().Interpolate(x, y, z) : new BicubicSpline(x, y, z);
            switch (c.String("kind"))
            {
                case "grid":
                    Interpolation2D f = Build();
                    double[][] points = [.. c.Input("points").EnumerateArray().Select(InterpolationInputs.Numbers)];
                    bool[] allow = InterpolationInputs.Bools(c.Input("allowExtrapolation"));
                    check.Output(
                        c, "value", InterpolationInputs.Deferred(Enumerable.Range(0, points.Length), i => f.Evaluate(points[i][0], points[i][1], allow[i])));
                    break;
                case "refusal":
                    check.Output(c, "value", () => Build().Evaluate(x.Length > 0 ? x[0] : 0.0, y[0], true));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }
}
