// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's kernel vectors, <c>tests/vectors/cxx/math/interpolations/kernelinterpolation.json</c>:
/// <see cref="KernelInterpolation"/> and <see cref="KernelInterpolation2D"/> under Gaussian and Epanechnikov kernels at
/// the nodes, between and beyond them, a tolerance the solve can't meet, refused, and <see cref="GaussianKernel"/>'s
/// value, derivative and primitive. The wheel exposes none of them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class KernelInterpolationCxxVectorTests
{
    [Fact]
    public void KernelInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/kernelinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("method"))
            {
                case "KernelInterpolation":
                    double[] at = c.Doubles("at");
                    check.Output(c, "values", () =>
                    {
                        var f = new KernelInterpolation(c.Doubles("x"), c.Doubles("y"), KernelOf(c.Input("kernel")), c.Double("epsilon"));
                        return at.Select(x => f.Evaluate(x, true)).ToArray();
                    });
                    break;
                case "KernelInterpolation2D":
                    (double X, double Y)[] points = [.. c.Input("at").EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble()))];
                    check.Output(c, "values", () =>
                    {
                        var f = new KernelInterpolation2D(
                            c.Doubles("x"), c.Doubles("y"), MatrixVectorTests.FromRows(c.Input("z")), KernelOf(c.Input("kernel")));
                        return points.Select(p => f.Evaluate(p.X, p.Y, true)).ToArray();
                    });
                    break;
                default:
                    var k = new GaussianKernel(c.Double("average"), c.Double("sigma"));
                    double[] xs = c.Doubles("at");
                    check.Output(c, "value", xs.Select(k.Evaluate).ToArray());
                    check.Output(c, "derivative", xs.Select(k.Derivative).ToArray());
                    check.Output(c, "primitive", xs.Select(k.Primitive).ToArray());
                    break;
            }
        }

        check.Complete();
    }

    // {gaussian: [average, sigma]}, or interpolations.cpp's Epanechnikov kernel as the harness writes it
    private static Func<double, double> KernelOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.String)
        {
            return u => Math.Abs(u) <= 1 ? (3.0 / 4.0) * (1 - (u * u)) : 0.0;
        }

        JsonElement g = spec.GetProperty("gaussian");
        return new GaussianKernel(g[0].GetDouble(), g[1].GetDouble()).Evaluate;
    }
}
