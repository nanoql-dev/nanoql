// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The binomial distributions' vectors, <c>tests/vectors/math/distributions/binomialdistribution.json</c>:
/// <see cref="BinomialDistribution"/> and <see cref="CumulativeBinomialDistribution"/> for eight probabilities and six
/// numbers of trials, and their constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BinomialDistributionVectorTests
{
    [Fact]
    public void BinomialDistributionVector()
    {
        var file = VectorFile.Load("math/distributions/binomialdistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("ps", out JsonElement refused))
            {
                long n = c.Input("n").GetInt64();
                double[] ps = Numbers(refused);
                check.Output(c, "probability", ps.Select(p => Deferred(() => new BinomialDistribution(p, n).Evaluate(1))).ToArray());
                check.Output(
                    c, "cumulative", ps.Select(p => Deferred(() => new CumulativeBinomialDistribution(p, n).Evaluate(1))).ToArray());
            }
            else
            {
                double p = c.Double("p");
                long n = c.Input("n").GetInt64();
                long[] ks = [.. c.Input("k").EnumerateArray().Select(k => k.GetInt64())];
                var pmf = new BinomialDistribution(p, n);
                var cdf = new CumulativeBinomialDistribution(p, n);
                check.Output(c, "probability", ks.Select(k => Deferred(() => pmf.Evaluate(k))).ToArray());
                check.Output(c, "cumulative", ks.Select(k => Deferred(() => cdf.Evaluate(k))).ToArray());
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
