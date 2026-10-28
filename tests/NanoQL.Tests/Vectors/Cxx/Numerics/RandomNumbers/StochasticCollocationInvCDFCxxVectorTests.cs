// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The stochastic collocation's harness vectors,
/// <c>tests/vectors/cxx/math/randomnumbers/stochasticcollocationinvcdf.json</c>:
/// <see cref="StochasticCollocationInvCDF"/> of the non-central chi-squared inverse (Boost's quantile, nanoql's
/// internal port) and of the exponential's, its value at deviates and at probabilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StochasticCollocationInvCDFCxxVectorTests
{
    private static readonly Func<double, double, double, double> ChiSquaredQuantile =
        BoostInternals.Function<Func<double, double, double, double>>("NonCentralChiSquaredFunctions", "Quantile");

    [Fact]
    public void StochasticCollocationInvCDFCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/stochasticcollocationinvcdf");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("inverse");
            Func<double, double> inverse;
            if (spec.TryGetProperty("nonCentralChiSquared", out JsonElement chi))
            {
                double k = chi[0].GetDouble(), lambda = chi[1].GetDouble();
                inverse = u => ChiSquaredQuantile(k, lambda, u);
            }
            else
            {
                double lambda = spec.GetProperty("exponential").GetDouble();
                inverse = u => -Math.Log(1.0 - u) / lambda;
            }

            var collocation = new StochasticCollocationInvCDF(inverse, c.Int("order"), Optional(c, "pMax"), Optional(c, "pMin"));
            check.Output(c, "value", Reals(c, "x").Select(collocation.Value).ToArray());
            check.Output(c, "operator()", Reals(c, "u").Select(collocation.Evaluate).ToArray());
        }

        check.Complete();
    }

    private static double? Optional(VectorCase c, string name) =>
        c.Input(name).ValueKind == JsonValueKind.Null ? null : VectorCase.Real(c.Input(name));

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];
}
