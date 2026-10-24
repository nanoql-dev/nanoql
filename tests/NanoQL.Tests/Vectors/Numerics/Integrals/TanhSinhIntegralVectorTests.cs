// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="TanhSinhIntegral"/>'s vectors, <c>tests/vectors/math/integrals/tanhsinhintegral.json</c>: integrals and
/// evaluation counts of named integrands over finite, half-infinite and infinite intervals, forward, reversed and empty,
/// at four tolerances.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TanhSinhIntegralVectorTests
{
    [Fact]
    public void TanhSinhIntegralVector()
    {
        var file = VectorFile.Load("math/integrals/tanhsinhintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement tolerance = c.Input("relTolerance");
            TanhSinhIntegral integrator = tolerance.ValueKind == JsonValueKind.Null
                ? new TanhSinhIntegral()
                : new TanhSinhIntegral(tolerance.GetDouble(), c.Int("maxRefinements"));
            object?[] results =
            [
                .. c.Input("integrals").EnumerateArray().Select(e =>
                {
                    Func<double, double> f = IntegralInputs.Integrands[e.GetProperty("f").GetString()!];
                    double a = Number(e.GetProperty("a")), b = Number(e.GetProperty("b"));
                    return (object?)(Func<object?>)(() =>
                    {
                        IntegrationResult r = integrator.IntegrateWithStatistics(f, a, b);
                        return new object[] { r.Value, r.NumberOfEvaluations };
                    });
                }),
            ];
            check.Output(c, "result", results);
        }

        check.Complete();
    }
}
