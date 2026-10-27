// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Garman-Klass estimators' vectors, <c>tests/vectors/models/volatility/garmanklass.json</c>: eight interval-price
/// series through Parkinson's estimator and Garman and Klass's first, third, fourth, fifth and sixth, the output's
/// dates and values.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GarmanKlassVectorTests
{
    [Fact]
    public void GarmanKlassVector()
    {
        var file = VectorFile.Load("models/volatility/garmanklass");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            TimeSeries<IntervalPrice> series = IntervalPrice.MakeSeries(c.DateInputs("dates"), c.Doubles("open"), c.Doubles("close"), c.Doubles("high"), c.Doubles("low"));
            double y = c.Double("yearFraction");
            GarmanKlassAbstract estimator = c.String("estimator") switch
            {
                "ParkinsonSigma" => new ParkinsonSigma(y),
                "GarmanKlassSigma4" => new GarmanKlassSigma4(y),
                "GarmanKlassSigma5" => new GarmanKlassSigma5(y),
                "GarmanKlassSigma1" => new GarmanKlassSigma1(y, c.Double("marketOpenFraction")),
                "GarmanKlassSigma3" => new GarmanKlassSigma3(y, c.Double("marketOpenFraction")),
                "GarmanKlassSigma6" => new GarmanKlassSigma6(y, c.Double("marketOpenFraction")),
                string s => throw new InvalidOperationException($"unknown estimator {s}"),
            };
            TimeSeries<double> result = estimator.Calculate(series);
            check.Output(c, "dates", result.Dates().Select(d => (object?)d));
            check.Output(c, "values", result.Values());
        }

        check.Complete();
    }
}
