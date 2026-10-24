// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="LagrangeInterpolation"/>'s vectors, <c>tests/vectors/math/interpolations/lagrangeinterpolation.json</c>:
/// on seven ascending node sets, the value and derivative at the nodes, near them, between them and outside the range,
/// and the refusals of the primitive and second derivative; the outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LagrangeInterpolationVectorTests
{
    [Fact]
    public void LagrangeInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/lagrangeinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var interpolation = new LagrangeInterpolation(Doubles(c.Input("x")), Doubles(c.Input("y")));
            (double X, bool Flag)[] points =
            [
                .. c.Input("points").EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetBoolean())),
            ];
            check.Output(c, "value", points.Select(p => Lazy(() => interpolation.Evaluate(p.X, p.Flag))).ToArray());
            check.Output(c, "derivative", points.Select(p => Lazy(() => interpolation.Derivative(p.X, p.Flag))).ToArray());
            double mid = (Doubles(c.Input("x"))[0] + Doubles(c.Input("x"))[1]) / 2;
            check.Output(c, "primitive", () => interpolation.Primitive(mid));
            check.Output(c, "secondDerivative", () => interpolation.SecondDerivative(mid));
        }

        check.Complete();
    }
}
