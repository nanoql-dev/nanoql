// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ExponentialForwardCorrelation vectors, <c>tests/vectors/models/marketmodels/correlations/expcorrelations.json</c>:
/// <see cref="ExponentialForwardCorrelation"/>'s times, rate times, number of rates and matrices at exponents 1, 0.5
/// and 0, on the default and on finer times; and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExponentialForwardCorrelationVectorTests
{
    [Fact]
    public void ExponentialForwardCorrelationVector()
    {
        var file = VectorFile.Load("models/marketmodels/correlations/expcorrelations");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] rateTimes = c.Doubles("rateTimes"), times = c.Doubles("times");
            double corr = c.Double("longTermCorr"), beta = c.Double("beta"), gamma = c.Double("gamma");
            if (c.String("call") == "refused")
            {
                check.Output(c, "construction", Lazy(() => new ExponentialForwardCorrelation(rateTimes, corr, beta, gamma, times)));
                continue;
            }

            var x = new ExponentialForwardCorrelation(rateTimes, corr, beta, gamma, times);
            check.Output(c, "times", x.Times);
            check.Output(c, "rateTimes", x.RateTimes);
            check.Output(c, "numberOfRates", x.NumberOfRates);
            check.Output(c, "correlations", x.Correlations().Select(Rows).ToArray());
            int[] periods = [.. new SortedSet<int> { 0, x.Times.Length - 1 }];
            check.Output(c, "correlation", periods.Select(i => Rows(x.Correlation(i))).ToArray());
        }

        check.Complete();
    }
}
