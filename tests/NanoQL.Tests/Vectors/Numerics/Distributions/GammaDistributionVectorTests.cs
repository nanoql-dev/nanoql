// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The gamma distribution's vectors, <c>tests/vectors/math/distributions/gammadistribution.json</c>:
/// <see cref="CumulativeGammaDistribution"/> for eight shapes at 40 points each, and its constructor's refusals; and
/// <see cref="GammaFunction.LogValue"/> at 200 points, with its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GammaDistributionVectorTests
{
    [Fact]
    public void GammaDistributionVector()
    {
        var file = VectorFile.Load("math/distributions/gammadistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("a", out JsonElement a))
            {
                var cumulative = new CumulativeGammaDistribution(a.GetDouble());
                double[] xs = Numbers(c.Input("x"));
                check.Output(c, "cumulative", xs.Select(x => Deferred(() => cumulative.Evaluate(x))).ToArray());
            }
            else if (c.Inputs.TryGetProperty("as", out JsonElement refused))
            {
                double at = c.Double("x");
                check.Output(
                    c,
                    "cumulative",
                    Numbers(refused).Select(s => Deferred(() => new CumulativeGammaDistribution(s).Evaluate(at))).ToArray());
            }
            else
            {
                var gamma = new GammaFunction();
                double[] xs = Numbers(c.Input("x"));
                check.Output(c, "logValue", xs.Select(x => Deferred(() => gamma.LogValue(x))).ToArray());
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
