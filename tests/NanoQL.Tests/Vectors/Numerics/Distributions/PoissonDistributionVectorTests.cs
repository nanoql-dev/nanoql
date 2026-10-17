// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Poisson distributions' vectors, <c>tests/vectors/math/distributions/poissondistribution.json</c>:
/// <see cref="PoissonDistribution"/> and <see cref="CumulativePoissonDistribution"/> for six means at k from 0 to 40
/// and up to 4294967295, and at two refused means; <see cref="InverseCumulativePoisson"/> for six lambdas at 50
/// probabilities, and its constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PoissonDistributionVectorTests
{
    [Fact]
    public void PoissonDistributionVector()
    {
        var file = VectorFile.Load("math/distributions/poissondistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("mu", out JsonElement muInput))
            {
                double mu = Number(muInput);
                long[] ks = [.. c.Input("k").EnumerateArray().Select(k => k.GetInt64())];
                var cumulative = new CumulativePoissonDistribution(mu);
                check.Output(c, "probability", ks.Select(k => Deferred(() => new PoissonDistribution(mu).Evaluate(k))).ToArray());
                check.Output(c, "cumulative", ks.Select(k => Deferred(() => cumulative.Evaluate(k))).ToArray());
            }
            else if (c.Inputs.TryGetProperty("lambda", out JsonElement lambdaInput))
            {
                var inverse = new InverseCumulativePoisson(lambdaInput.GetDouble());
                double[] xs = Numbers(c.Input("x"));
                check.Output(c, "inverse", xs.Select(x => Deferred(() => inverse.Evaluate(x))).ToArray());
            }
            else
            {
                double[] lambdas = Numbers(c.Input("lambdas"));
                double at = c.Double("x");
                check.Output(c, "inverse", lambdas.Select(l => Deferred(() => new InverseCumulativePoisson(l).Evaluate(at))).ToArray());
            }
        }

        check.Complete();
    }

    // A value computed when compared, so that a throw is checked.
    private static object? Deferred(Func<double> f) => (Func<object?>)(() => f());

    // Numbers as the vectors encode them: the non-finite ones as strings.
    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(Number)];

    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
