// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's tanh-sinh quadrature's harness vectors, <c>tests/vectors/cxx/boost/math/quadrature/tanh_sinh.json</c>: the
/// integral and Boost's error estimate of named integrands over finite, half-infinite and infinite intervals at five
/// settings, read through <see cref="TanhSinhIntegral"/>'s integral and absolute error.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TanhSinhCxxVectorTests
{
    [Fact]
    public void TanhSinhCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/quadrature/tanh_sinh");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var integrator = new TanhSinhIntegral(c.Double("relTolerance"), c.Int("maxRefinements"), c.Double("minComplement"));
            object?[] results =
            [
                .. c.Input("integrals").EnumerateArray().Select(e =>
                {
                    Func<double, double> f = IntegralInputs.Integrands[e.GetProperty("f").GetString()!];
                    double a = e.GetProperty("a").GetDouble(), b = e.GetProperty("b").GetDouble();
                    return (object?)(Func<object?>)(() =>
                    {
                        IntegrationResult r = integrator.IntegrateWithStatistics(f, a, b);
                        return new object?[] { r.Value, r.AbsoluteError };
                    });
                }),
            ];
            check.Output(c, "result", results);
        }

        check.Complete();
    }
}
