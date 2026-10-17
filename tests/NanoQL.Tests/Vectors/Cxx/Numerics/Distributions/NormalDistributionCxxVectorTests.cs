// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's normal-distribution vectors, <c>tests/vectors/cxx/math/distributions/normaldistribution.json</c>:
/// <see cref="MaddockCumulativeNormal"/> and <see cref="MaddockInverseCumulativeNormal"/>, the ports of Boost.Math's
/// normal cdf and quantile, for three (average, sigma) pairs over inputs that reach every branch of Boost's
/// <c>erf_imp</c> and <c>erf_inv_imp</c>, and the parameters QuantLib or Boost refuse; and
/// <see cref="InverseCumulativeNormal.StandardValue"/>, which the wheel doesn't expose. It also prints how many of its
/// real outputs are bit-identical to QuantLib's: all of them, since the port keeps Boost's order of operations.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NormalDistributionCxxVectorTests
{
    private const string FilePath = "cxx/math/distributions/normaldistribution";

    [Fact]
    public void NormalDistributionCxxVector()
    {
        var file = VectorFile.Load(FilePath);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("sigma", out JsonElement sigma))
            {
                var cumulative = new MaddockCumulativeNormal(c.Double("average"), sigma.GetDouble());
                var inverse = new MaddockInverseCumulativeNormal(c.Double("average"), sigma.GetDouble());
                check.Output(c, "cumulative", Deferred(Numbers(c.Input("x")), cumulative.Evaluate));
                check.Output(c, "inverse", Deferred(Numbers(c.Input("p")), inverse.Evaluate));
            }
            else if (c.Inputs.TryGetProperty("sigmas", out JsonElement sigmas))
            {
                double[] averages = Numbers(c.Input("averages"));
                double[] ss = Numbers(sigmas);
                double x = c.Double("x"), p = c.Double("p");
                int[] i = [.. Enumerable.Range(0, ss.Length)];
                check.Output(c, "cumulative", i.Select(k => Deferred(() => new MaddockCumulativeNormal(averages[k], ss[k]).Evaluate(x))).ToArray());
                check.Output(c, "inverse", i.Select(k => Deferred(() => new MaddockInverseCumulativeNormal(averages[k], ss[k]).Evaluate(p))).ToArray());
            }
            else
            {
                check.Output(c, "standardValue", Deferred(Numbers(c.Input("x")), InverseCumulativeNormal.StandardValue));
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{FilePath}: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }

    // Each value computed when compared, so that a throw is checked.
    private static object?[] Deferred(double[] inputs, Func<double, double> f) =>
        inputs.Select(x => Deferred(() => f(x))).ToArray();

    private static object? Deferred(Func<double> f) => (Func<object?>)(() => f());

    // Numbers as the vectors encode them: the non-finite ones as strings.
    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(Number)];

    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
