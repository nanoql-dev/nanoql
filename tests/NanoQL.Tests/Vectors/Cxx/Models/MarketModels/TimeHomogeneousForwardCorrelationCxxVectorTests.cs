// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's time-homogeneous correlation vectors,
/// <c>tests/vectors/cxx/models/marketmodels/correlations/timehomogeneousforwardcorrelation.json</c>:
/// <see cref="TimeHomogeneousForwardCorrelation"/>'s members and <see cref="TimeHomogeneousForwardCorrelation.EvolvedMatrices"/>,
/// and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TimeHomogeneousForwardCorrelationCxxVectorTests
{
    [Fact]
    public void TimeHomogeneousForwardCorrelationCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/correlations/timehomogeneousforwardcorrelation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = MatrixOf(c.Input("fwdCorrelation"));
            if (c.String("call") == "evolved")
            {
                check.Output(c, "matrices", TimeHomogeneousForwardCorrelation.EvolvedMatrices(m).Select(Rows).ToArray());
                continue;
            }

            double[] times = c.Doubles("rateTimes");
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", Lazy(() => new TimeHomogeneousForwardCorrelation(m, times)));
                continue;
            }

            var x = new TimeHomogeneousForwardCorrelation(m, times);
            check.Output(c, "times", x.Times);
            check.Output(c, "rateTimes", x.RateTimes);
            check.Output(c, "numberOfRates", x.NumberOfRates);
            check.Output(c, "correlations", x.Correlations().Select(Rows).ToArray());
        }

        check.Complete();
    }
}
