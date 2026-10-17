// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The normal distributions' vectors, <c>tests/vectors/math/distributions/normaldistribution.json</c>: for three
/// (average, sigma) pairs, <see cref="NormalDistribution"/> and <see cref="CumulativeNormalDistribution"/> with their
/// derivatives at 200 points, <see cref="InverseCumulativeNormal"/> and <see cref="MoroInverseCumulativeNormal"/> at
/// 200 probabilities, and each constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NormalDistributionVectorTests
{
    [Fact]
    public void NormalDistributionVector()
    {
        var file = VectorFile.Load("math/distributions/normaldistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double average = c.Double("average");
            if (c.Inputs.TryGetProperty("sigmas", out JsonElement refused))
            {
                double[] sigmas = Numbers(refused);
                double at = c.Double("x");
                check.Output(c, "density", Deferred(sigmas, s => new NormalDistribution(average, s).Evaluate(at)));
                check.Output(c, "cumulative", Deferred(sigmas, s => new CumulativeNormalDistribution(average, s).Evaluate(at)));
                check.Output(c, "inverse", Deferred(sigmas, s => new InverseCumulativeNormal(average, s).Evaluate(at)));
                check.Output(c, "moroInverse", Deferred(sigmas, s => new MoroInverseCumulativeNormal(average, s).Evaluate(at)));
                continue;
            }

            double sigma = c.Double("sigma");
            double[] xs = Numbers(c.Input("x"));
            double[] ps = Numbers(c.Input("p"));
            var normal = new NormalDistribution(average, sigma);
            var cumulative = new CumulativeNormalDistribution(average, sigma);
            var inverse = new InverseCumulativeNormal(average, sigma);
            var moro = new MoroInverseCumulativeNormal(average, sigma);
            check.Output(c, "density", Deferred(xs, normal.Evaluate));
            check.Output(c, "densityDerivative", Deferred(xs, normal.Derivative));
            check.Output(c, "cumulative", Deferred(xs, cumulative.Evaluate));
            check.Output(c, "cumulativeDerivative", Deferred(xs, cumulative.Derivative));
            check.Output(c, "inverse", Deferred(ps, inverse.Evaluate));
            check.Output(c, "moroInverse", Deferred(ps, moro.Evaluate));
        }

        check.Complete();
    }

    // Each value computed when compared, so that a throw is checked.
    private static object?[] Deferred(double[] inputs, Func<double, double> f) =>
        inputs.Select(x => (object?)(Func<object?>)(() => f(x))).ToArray();

    // Numbers as the vectors encode them: the non-finite ones as strings.
    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(Number)];

    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
