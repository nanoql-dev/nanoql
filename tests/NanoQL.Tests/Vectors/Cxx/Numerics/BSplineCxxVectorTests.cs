// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's B-spline vectors, <c>tests/vectors/cxx/math/bspline.json</c>: <see cref="BSpline"/>'s basis
/// functions, which the wheel doesn't expose, on linear, quadratic and cubic B-splines with distinct knots, at points
/// across and past the knots, and the constructions and indices QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BSplineCxxVectorTests
{
    [Fact]
    public void BSplineCxxVector()
    {
        var file = VectorFile.Load("cxx/math/bspline");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int p = c.Int("p"), n = c.Int("n");
            double[] knots = c.Doubles("knots"), xs = c.Doubles("x");
            int[] indices = c.Ints("indices");
            if (c.Name.StartsWith("refusal: ", StringComparison.Ordinal))
            {
                check.Output(c, "values", () => new BSpline(p, n, knots).Evaluate(indices[0], xs[0]));
                continue;
            }

            var spline = new BSpline(p, n, knots);
            check.Output(
                c,
                "values",
                indices.Select(i => (object?)xs.Select(x => (object?)spline.Evaluate(i, x)).ToArray()).ToArray());
        }

        check.Complete();
    }
}
