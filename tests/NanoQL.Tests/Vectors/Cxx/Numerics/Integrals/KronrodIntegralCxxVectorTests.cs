// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Gauss-Kronrod rules' harness vectors, <c>tests/vectors/cxx/math/integrals/kronrodintegral.json</c>:
/// <see cref="GaussKronrodNonAdaptive"/>'s integral, evaluation count, <see cref="IntegrationResult.AbsoluteError"/>
/// and
/// <see cref="Integrator.IntegrationSuccess"/>, and its relative accuracy as constructed and as set.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class KronrodIntegralCxxVectorTests
{
    [Fact]
    public void KronrodIntegralCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/kronrodintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var rule = new GaussKronrodNonAdaptive(c.Double("tolerance"), c.Int("maxEvaluations"), c.Double("relativeAccuracy"));
            if (c.Expected.TryGetProperty("relativeAccuracy", out _))
            {
                check.Output(c, "relativeAccuracy", new object[]
                {
                    rule.RelativeAccuracy, rule.SetRelativeAccuracy(c.Double("newRelativeAccuracy")).RelativeAccuracy,
                });
                continue;
            }

            Func<object?>[] results = [.. c.Input("integrals").EnumerateArray().Select(e => Integral(rule, e))];
            check.Output(c, "result", results);
        }

        check.Complete();
    }

    private static Func<object?> Integral(GaussKronrodNonAdaptive rule, JsonElement e)
    {
        Func<double, double> f = IntegralInputs.Integrands[e.GetProperty("f").GetString()!];
        double a = e.GetProperty("a").GetDouble(), b = e.GetProperty("b").GetDouble();
        return () =>
        {
            IntegrationResult r = rule.IntegrateWithStatistics(f, a, b);
            return new object?[] { r.Value, r.NumberOfEvaluations, r.AbsoluteError, rule.IntegrationSuccess(r) };
        };
    }
}
