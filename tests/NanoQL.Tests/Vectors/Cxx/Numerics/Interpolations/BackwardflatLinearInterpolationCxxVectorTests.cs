// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's backward-flat-linear interpolation vectors,
/// <c>tests/vectors/cxx/math/interpolations/backwardflatlinearinterpolation.json</c>: on 20 grids, the
/// <see cref="BackwardflatLinearInterpolation"/> built by its constructor and by <see cref="BackwardflatLinear"/>, with
/// and without extrapolation, and its value at the nodes, between them, outside the grid and at NaN coordinates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BackwardflatLinearInterpolationCxxVectorTests
{
    [Fact]
    public void BackwardflatLinearInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/backwardflatlinearinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("interpolation");
            double[] x = InterpolationInputs.Numbers(spec.GetProperty("x"));
            double[] y = InterpolationInputs.Numbers(spec.GetProperty("y"));
            Matrix z = InterpolationInputs.MatrixOf(spec.GetProperty("z"));
            Interpolation2D f = spec.GetProperty("factory").GetBoolean()
                ? new BackwardflatLinear().Interpolate(x, y, z)
                : new BackwardflatLinearInterpolation(x, y, z);
            if (spec.GetProperty("enableExtrapolation").GetBoolean())
            {
                f = f.EnableExtrapolation();
            }

            double[][] points = [.. c.Input("points").EnumerateArray().Select(InterpolationInputs.Numbers)];
            bool[] allow = InterpolationInputs.Bools(c.Input("allowExtrapolation"));
            check.Output(
                c, "value", InterpolationInputs.Deferred(Enumerable.Range(0, points.Length), i => f.Evaluate(points[i][0], points[i][1], allow[i])));
        }

        check.Complete();
    }
}
