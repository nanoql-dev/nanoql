// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's 2-D polynomial spline vectors, <c>tests/vectors/cxx/experimental/inflation/polynomial2Dspline.json</c>:
/// the spline through <see cref="Polynomial"/> read on its nodes, between them and outside, with and without
/// extrapolation; and a grid it refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Polynomial2DSplineCxxVectorTests
{
    [Fact]
    public void Polynomial2DSplineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/inflation/polynomial2Dspline");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = c.Doubles("x");
            double[] y = c.Doubles("y");
            Matrix z = InflationSurfaceInputs.MatrixOf(c.Input("z"));
            if (c.String("kind") == "refused")
            {
                check.Output(c, "construction", () => default(Polynomial).Interpolate(x, y, z));
                continue;
            }

            Interpolation2D f = default(Polynomial).Interpolate(x, y, z);
            bool extrapolate = c.Input("allowExtrapolation").GetBoolean();
            check.Output(c, "values", c.Input("points").EnumerateArray().Select(p => (Func<object?>)(() =>
                f.Evaluate(p[0].GetDouble(), p[1].GetDouble(), extrapolate))).ToArray());
        }

        check.Complete();
    }
}
