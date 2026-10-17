// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="BackwardFlatInterpolation"/>'s vectors, <c>tests/vectors/math/interpolations/backwardflatinterpolation.json</c>:
/// on ten node sets, one of a single point, the value, derivative, second derivative and primitive at 50 points inside
/// and outside the range.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BackwardFlatInterpolationVectorTests
{
    [Fact]
    public void BackwardFlatInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/backwardflatinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = InterpolationInputs.Numbers(c.Input("x")), y = InterpolationInputs.Numbers(c.Input("y"));
            InterpolationInputs.CheckMembers(check, c, () => new BackwardFlatInterpolation(x, y));
        }

        check.Complete();
    }
}
