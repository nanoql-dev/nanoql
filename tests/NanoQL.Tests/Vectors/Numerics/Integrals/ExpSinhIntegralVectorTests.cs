// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="ExpSinhIntegral"/>'s vectors, <c>tests/vectors/math/integrals/expsinhintegral.json</c>: integrals and
/// evaluation counts of named integrands over half-infinite intervals, forward and reversed, Boost's refusals of a
/// finite and of an infinite interval, and the one-argument integral over [0, ∞), at four tolerances.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExpSinhIntegralVectorTests
{
    [Fact]
    public void ExpSinhIntegralVector()
    {
        var file = VectorFile.Load("math/integrals/expsinhintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement tolerance = c.Input("relTolerance");
            ExpSinhIntegral integrator = tolerance.ValueKind == JsonValueKind.Null
                ? new ExpSinhIntegral()
                : new ExpSinhIntegral(tolerance.GetDouble(), c.Int("maxRefinements"));
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

            object?[] halfLine =
            [
                .. c.Input("halfLine").EnumerateArray().Select(e =>
                {
                    Func<double, double> f = IntegralInputs.Integrands[e.GetString()!];
                    return (object?)(Func<object?>)(() =>
                    {
                        IntegrationResult r = integrator.IntegrateWithStatistics(f);
                        return new object[] { r.Value, r.NumberOfEvaluations };
                    });
                }),
            ];
            check.Output(c, "halfLineResult", halfLine);
        }

        check.Complete();
    }
}
