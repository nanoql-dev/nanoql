// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The integrands the integrators' vectors name (<c>tools/vectors/gen/math/integrals/simpsonintegral.py</c>), and the
/// comparison of a case's integrals and evaluation counts.
/// </summary>
internal static class IntegralInputs
{
    /// <summary>The named integrands.</summary>
    internal static readonly Dictionary<string, Func<double, double>> Integrands = new()
    {
        ["exp(x)"] = Math.Exp,
        ["sin(x)"] = Math.Sin,
        ["x x"] = x => x * x,
        ["x x x x"] = x => x * x * x * x,
        ["1/(1+x x)"] = x => 1.0 / (1.0 + (x * x)),
        ["sqrt(x)"] = Math.Sqrt,
        ["exp(-x x/2)"] = x => Math.Exp(-x * x / 2.0),
        ["x"] = x => x,
    };

    /// <summary>
    /// Compares a case's "result" (each integral with its evaluation count) or its "construction" (a refusal of the
    /// integrator).
    /// </summary>
    /// <param name="check">The comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="integrator">The case's integrator, built when compared.</param>
    internal static void CheckIntegrals(VectorAssert check, VectorCase c, Func<Integrator> integrator)
    {
        if (c.Expected.TryGetProperty("construction", out _))
        {
            check.Output(c, "construction", (Func<object?>)(() => integrator() is null ? 1 : 0));
            return;
        }

        Integrator I = integrator();
        object?[] results = [.. c.Input("integrals").EnumerateArray().Select(e => Lazy(I, e))];
        check.Output(c, "result", results);
    }

    private static object? Lazy(Integrator I, JsonElement e)
    {
        Func<double, double> f = Integrands[e.GetProperty("f").GetString()!];
        double a = e.GetProperty("a").GetDouble(), b = e.GetProperty("b").GetDouble();
        return (Func<object?>)(() =>
        {
            IntegrationResult r = I.IntegrateWithStatistics(f, a, b);
            return new object[] { r.Value, r.NumberOfEvaluations };
        });
    }
}
