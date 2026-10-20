// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The chi-squared distributions' vectors, <c>tests/vectors/math/distributions/chisquaredistribution.json</c>:
/// <see cref="CumulativeChiSquareDistribution"/> with its refusals,
/// <see cref="NonCentralCumulativeChiSquareDistribution"/> outside the region nanoql refuses, and
/// <see cref="InverseNonCentralCumulativeChiSquareDistribution"/> under two settings.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ChiSquareDistributionVectorTests
{
    [Fact]
    public void ChiSquareDistributionVector()
    {
        var file = VectorFile.Load("math/distributions/chisquaredistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("dfs", out JsonElement refused))
            {
                double at = c.Double("x");
                check.Output(
                    c,
                    "cumulative",
                    Numbers(refused).Select(df => Deferred(() => new CumulativeChiSquareDistribution(df).Evaluate(at))).ToArray());
            }
            else if (c.Inputs.TryGetProperty("p", out JsonElement probabilities))
            {
                double df = c.Double("df"), ncp = c.Double("ncp");
                InverseNonCentralCumulativeChiSquareDistribution d = c.Inputs.TryGetProperty("maxEvaluations", out JsonElement e)
                    ? new(df, ncp, e.GetInt32(), c.Double("accuracy"))
                    : new(df, ncp);
                check.Output(c, "quantile", Numbers(probabilities).Select(p => Deferred(() => d.Evaluate(p))).ToArray());
            }
            else if (c.Inputs.TryGetProperty("ncp", out JsonElement ncpElement))
            {
                var d = new NonCentralCumulativeChiSquareDistribution(c.Double("df"), ncpElement.GetDouble());
                check.Output(c, "cumulative", Numbers(c.Input("x")).Select(x => Deferred(() => d.Evaluate(x))).ToArray());
            }
            else
            {
                var d = new CumulativeChiSquareDistribution(c.Double("df"));
                check.Output(c, "cumulative", Numbers(c.Input("x")).Select(x => Deferred(() => d.Evaluate(x))).ToArray());
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
