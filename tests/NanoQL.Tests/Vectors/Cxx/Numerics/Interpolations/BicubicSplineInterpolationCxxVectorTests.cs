// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's bicubic spline vectors, <c>tests/vectors/cxx/math/interpolations/bicubicsplineinterpolation.json</c>:
/// <see cref="BicubicSpline"/>'s five derivatives, which the wheel doesn't expose, inside and outside the grid.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BicubicSplineInterpolationCxxVectorTests
{
    [Fact]
    public void BicubicSplineInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/bicubicsplineinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var f = new BicubicSpline(
                InterpolationInputs.Numbers(c.Input("x")), InterpolationInputs.Numbers(c.Input("y")), InterpolationInputs.MatrixOf(c.Input("z")));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(InterpolationInputs.Numbers)];
            object?[] At(Func<double, double, double> d) => InterpolationInputs.Deferred(points, p => d(p[0], p[1]));
            check.Output(c, "derivativeX", At(f.DerivativeX));
            check.Output(c, "derivativeY", At(f.DerivativeY));
            check.Output(c, "secondDerivativeX", At(f.SecondDerivativeX));
            check.Output(c, "secondDerivativeY", At(f.SecondDerivativeY));
            check.Output(c, "derivativeXY", At(f.DerivativeXY));
        }

        check.Complete();
    }
}
