// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The abcd volatility vectors, <c>tests/vectors/termstructures/volatility/abcd.json</c>: on six coefficient sets, the
/// short- and long-term volatilities, the instantaneous volatility, variance and covariance, the primitive, the
/// integrated covariance, the volatility and variance over intervals, the maximum volatility where 1.44 agrees with
/// nanoql, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AbcdVectorTests
{
    [Fact]
    public void AbcdVector()
    {
        var file = VectorFile.Load("termstructures/volatility/abcd");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] abcd = c.Doubles("abcd");
            if (c.Expected.TryGetProperty("refused", out _))
            {
                string call = c.String("call");
                check.Output(c, "refused", () => call switch
                {
                    "reversed_bounds" => (object?)new AbcdFunction(abcd[0], abcd[1], abcd[2], abcd[3]).Covariance(2.0, 1.0, 3.0, 4.0),
                    "reversed_volatility" => new AbcdFunction(abcd[0], abcd[1], abcd[2], abcd[3]).Volatility(2.0, 1.0, 3.0),
                    _ => new AbcdFunction(abcd[0], abcd[1], abcd[2], abcd[3]),
                });
                continue;
            }

            var f = new AbcdFunction(abcd[0], abcd[1], abcd[2], abcd[3]);
            double[][] points = Rows(c.Input("points"));
            double[][] covariances = Rows(c.Input("covariances"));
            double[][] volatilities = Rows(c.Input("volatilities"));
            check.Output(c, "shortTermVolatility", f.ShortTermVolatility());
            check.Output(c, "longTermVolatility", f.LongTermVolatility());
            check.Output(c, "instantaneousVolatility", points.Select(p => f.InstantaneousVolatility(p[0], p[1])).ToArray());
            check.Output(c, "instantaneousVariance", points.Select(p => f.InstantaneousVariance(p[0], p[1])).ToArray());
            check.Output(c, "instantaneousCovariance", points.Select(p => f.InstantaneousCovariance(p[0], p[1], p[2])).ToArray());
            check.Output(c, "covarianceAt", points.Select(p => f.Covariance(p[0], p[1], p[2])).ToArray());
            check.Output(c, "primitive", points.Select(p => f.Primitive(p[0], p[1], p[2])).ToArray());
            check.Output(c, "covariance", covariances.Select(p => f.Covariance(p[0], p[1], p[2], p[3])).ToArray());
            check.Output(c, "volatility", volatilities.Select(p => f.Volatility(p[0], p[1], p[2])).ToArray());
            check.Output(c, "variance", volatilities.Select(p => f.Variance(p[0], p[1], p[2])).ToArray());
            if (c.Expected.TryGetProperty("maximumVolatility", out _))
            {
                check.Output(c, "maximumVolatility", f.MaximumVolatility());
            }
        }

        check.Complete();
    }

    private static double[][] Rows(JsonElement e) =>
        [.. e.EnumerateArray().Select(p => p.EnumerateArray().Select(x => x.GetDouble()).ToArray())];
}
