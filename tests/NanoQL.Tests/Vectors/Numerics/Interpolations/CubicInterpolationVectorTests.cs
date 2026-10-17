// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The named cubic interpolations' vectors, <c>tests/vectors/math/interpolations/cubicinterpolation.json</c>:
/// <see cref="CubicNaturalSpline"/>, <see cref="MonotonicCubicNaturalSpline"/>, <see cref="KrugerCubic"/>,
/// <see cref="FritschButlandCubic"/>, <see cref="Parabolic"/> and <see cref="MonotonicParabolic"/> on ten node sets, the
/// value, derivative, second derivative and primitive at 50 points inside and outside the range.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CubicInterpolationVectorTests
{
    [Fact]
    public void CubicInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/cubicinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = InterpolationInputs.Numbers(c.Input("x")), y = InterpolationInputs.Numbers(c.Input("y"));
            string type = c.String("type");
            InterpolationInputs.CheckMembers(check, c, () => InterpolationInputs.Build(type, x, y, c.Inputs));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/interpolations/cubicinterpolation: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
