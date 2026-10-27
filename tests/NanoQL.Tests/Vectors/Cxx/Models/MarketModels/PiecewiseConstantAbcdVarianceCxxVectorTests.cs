// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's abcd variance vectors,
/// <c>tests/vectors/cxx/models/marketmodels/models/piecewiseconstantabcdvariance.json</c>:
/// <see cref="PiecewiseConstantAbcdVariance"/> for three abcd functions on three sets of rate times at every reset
/// index: each step's variance and volatility, the rate times, and the <see cref="PiecewiseConstantVariance"/> base's
/// reads at each step, and <see cref="PiecewiseConstantAbcdVariance.GetABCD"/>; and the refused variances.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseConstantAbcdVarianceCxxVectorTests
{
    [Fact]
    public void PiecewiseConstantAbcdVarianceCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/piecewiseconstantabcdvariance");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("call") == "refused")
            {
                foreach (JsonProperty r in c.Input("variances").EnumerateObject())
                {
                    JsonElement spec = r.Value;
                    check.Output(c, r.Name, Lazy(() => VarianceOf(spec)));
                }

                continue;
            }

            PiecewiseConstantAbcdVariance v = VarianceOf(c.Input("variance"));
            CheckVariance(check, c, "", [v]);
            AbcdCoefficients abcd = v.GetABCD();
            check.Output(c, "abcd", new[] { abcd.A, abcd.B, abcd.C, abcd.D });
        }

        check.Complete();
    }

    // A variance spec's PiecewiseConstantAbcdVariance.
    internal static PiecewiseConstantAbcdVariance VarianceOf(JsonElement spec) =>
        new(
            spec.GetProperty("a").GetDouble(),
            spec.GetProperty("b").GetDouble(),
            spec.GetProperty("c").GetDouble(),
            spec.GetProperty("d").GetDouble(),
            spec.GetProperty("resetIndex").GetInt32(),
            Reals(spec.GetProperty("rateTimes")));

    // The variances' reads under the prefix, each the read of one variance or, for several, an array of them.
    internal static void CheckVariance(VectorAssert check, VectorCase c, string prefix, IReadOnlyList<PiecewiseConstantVariance> vs)
    {
        string Key(string name) => prefix.Length == 0 ? name : prefix + char.ToUpperInvariant(name[0]) + name[1..];
        object Over(Func<PiecewiseConstantVariance, object> read) =>
            prefix.Length == 0 ? read(vs[0]) : vs.Select(read).ToArray();
        check.Output(c, Key("variances"), Over(v => v.Variances));
        check.Output(c, Key("volatilities"), Over(v => v.Volatilities));
        check.Output(c, Key("rateTimes"), Over(v => v.RateTimes));
        check.Output(c, Key("variance"), Over(v => Enumerable.Range(0, v.Variances.Length).Select(v.Variance).ToArray()));
        check.Output(c, Key("volatility"), Over(v => Enumerable.Range(0, v.Variances.Length).Select(v.Volatility).ToArray()));
        check.Output(c, Key("totalVariance"), Over(v => Enumerable.Range(0, v.Variances.Length).Select(v.TotalVariance).ToArray()));
        check.Output(c, Key("totalVolatility"), Over(v => Enumerable.Range(0, v.Variances.Length).Select(v.TotalVolatility).ToArray()));
    }
}
