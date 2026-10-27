// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's convex-monotone vectors,
/// <c>tests/vectors/cxx/math/interpolations/convexmonotoneinterpolation.json</c>:
/// <see cref="ConvexMonotoneInterpolation"/>'s values and primitives, built whole with and without a flat final period,
/// and built a node at a time through <see cref="ConvexMonotone.LocalInterpolate"/>, as the local bootstrap builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConvexMonotoneInterpolationCxxVectorTests
{
    [Fact]
    public void ConvexMonotoneInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/convexmonotoneinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = c.Doubles("x"), y = c.Doubles("y"), points = c.Doubles("points");
            double q = c.Double("quadraticity"), m = c.Double("monotonicity");
            bool fp = c.Bool("forcePositive");
            if (c.String("form") == "whole")
            {
                var f = new ConvexMonotoneInterpolation(x, y, q, m, fp, c.Bool("flatFinalPeriod"));
                check.Output(c, "values", points.Select(p => f.Evaluate(p, true)).ToArray());
                check.Output(c, "primitives", points.Select(p => f.Primitive(p, true)).ToArray());
                continue;
            }

            var cm = new ConvexMonotone(q, m, fp);
            int localisation = c.Int("localisation");
            Interpolation? previous = null;
            var values = new List<double[]>();
            var primitives = new List<double[]>();
            for (int length = localisation + 1; length <= x.Length; ++length)
            {
                Interpolation f = cm.LocalInterpolate(x.AsSpan(0, length), y.AsSpan(0, length), localisation, previous, x.Length);
                values.Add(points.Select(p => f.Evaluate(p, true)).ToArray());
                primitives.Add(points.Select(p => f.Primitive(p, true)).ToArray());
                previous = f;
            }

            check.Output(c, "values", values.ToArray());
            check.Output(c, "primitives", primitives.ToArray());
        }

        check.Complete();
    }
}
