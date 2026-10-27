// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's simple local estimator vectors, <c>tests/vectors/cxx/models/volatility/simplelocalestimator.json</c>:
/// <see cref="SimpleLocalEstimator"/> on quote series and <see cref="GarmanKlassSimpleSigma"/> on interval-price
/// series, the output's dates and values.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SimpleLocalEstimatorCxxVectorTests
{
    [Fact]
    public void SimpleLocalEstimatorCxxVector()
    {
        var file = VectorFile.Load("cxx/models/volatility/simplelocalestimator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double y = c.Double("yearFraction");
            TimeSeries<double> result = c.String("call") == "simple"
                ? new SimpleLocalEstimator(y).Calculate(new TimeSeries<double>(c.DateInputs("dates"), c.Doubles("values")))
                : new GarmanKlassSimpleSigma(y).Calculate(
                    IntervalPrice.MakeSeries(c.DateInputs("dates"), c.Doubles("open"), c.Doubles("close"), c.Doubles("high"), c.Doubles("low")));
            check.Output(c, "dates", result.Dates().Select(d => (object?)d));
            check.Output(c, "values", result.Values());
        }

        check.Complete();
    }
}
