// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's 2-D interpolation vectors, <c>tests/vectors/cxx/math/interpolations/interpolation2d.json</c>: on
/// <see cref="BilinearInterpolation"/>s and the <see cref="FlatExtrapolator2D"/>s decorating them, with and without
/// extrapolation, <see cref="Interpolation2D"/>'s inspectors, <see cref="Interpolation2D.LocateX"/>,
/// <see cref="Interpolation2D.LocateY"/>, <see cref="Interpolation2D.IsInRange"/> and the value inside and outside the
/// grid.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Interpolation2DCxxVectorTests
{
    [Fact]
    public void Interpolation2DCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/interpolation2d");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Interpolation2D f = InterpolationInputs.Build2D(c.Input("interpolation"));
            double[] xc = InterpolationInputs.Numbers(c.Input("xCoordinates"));
            double[] yc = InterpolationInputs.Numbers(c.Input("yCoordinates"));
            double[][] inRange = [.. c.Input("inRange").EnumerateArray().Select(InterpolationInputs.Numbers)];
            double[][] points = [.. c.Input("points").EnumerateArray().Select(InterpolationInputs.Numbers)];
            bool[] allow = InterpolationInputs.Bools(c.Input("allowExtrapolation"));
            Matrix z = f.ZData();

            check.Output(c, "xMin", f.XMin);
            check.Output(c, "xMax", f.XMax);
            check.Output(c, "yMin", f.YMin);
            check.Output(c, "yMax", f.YMax);
            check.Output(c, "xValues", f.XValues());
            check.Output(c, "yValues", f.YValues());
            check.Output(c, "zData", Enumerable.Range(0, z.Rows).Select(i => z.Row(i).ToArray()).ToArray());
            check.Output(c, "locateX", xc.Select(x => f.LocateX(x)).ToArray());
            check.Output(c, "locateY", yc.Select(y => f.LocateY(y)).ToArray());
            check.Output(c, "isInRange", inRange.Select(p => f.IsInRange(p[0], p[1])).ToArray());
            check.Output(
                c, "value", InterpolationInputs.Deferred(Enumerable.Range(0, points.Length), i => f.Evaluate(points[i][0], points[i][1], allow[i])));
        }

        check.Complete();
    }
}
