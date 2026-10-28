// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's two-dimensional integral vectors,
/// <c>tests/vectors/cxx/math/integrals/twodimensionalintegral.json</c>: <see cref="TwoDimensionalIntegral"/> of three
/// integrands over three rectangles, nested by the trapezoid, Simpson and Gauss-Lobatto integrators, and integrators
/// whose evaluations run out, refused. The wheel doesn't expose it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TwoDimensionalIntegralCxxVectorTests
{
    // the harness's integrands, written as it writes them
    private static readonly Dictionary<string, Func<double, double, double>> Integrands = new()
    {
        ["x y"] = (x, y) => x * y,
        ["exp(-x) sin(x y)"] = (x, y) => Math.Exp(-x) * Math.Sin(x * y),
        ["1/(1+x x+y y)"] = (x, y) => 1.0 / (1.0 + (x * x) + (y * y)),
    };

    [Fact]
    public void TwoDimensionalIntegralCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/twodimensionalintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Func<double, double, double> f = Integrands[c.String("integrand")];
            double[] a = c.Doubles("a"), b = c.Doubles("b");
            check.Output(c, "integral", () => new TwoDimensionalIntegral(IntegratorOf(c.Input("outer")), IntegratorOf(c.Input("inner")))
                .Integrate(f, (a[0], a[1]), (b[0], b[1])));
        }

        check.Complete();
    }

    // [kind, accuracy, maxEvaluations], as the harness builds it
    private static Integrator IntegratorOf(JsonElement spec)
    {
        double accuracy = spec[1].GetDouble();
        int maxEvaluations = spec[2].GetInt32();
        return spec[0].GetString() switch
        {
            "Trapezoid" => new TrapezoidIntegral<Default>(accuracy, maxEvaluations),
            "Simpson" => new SimpsonIntegral(accuracy, maxEvaluations),
            _ => new GaussLobattoIntegral(maxEvaluations, accuracy),
        };
    }
}
