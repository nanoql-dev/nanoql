// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's coterminal correlation vectors,
/// <c>tests/vectors/cxx/models/marketmodels/correlations/cotswapfromfwdcorrelation.json</c>:
/// <see cref="CotSwapFromFwdCorrelation"/> from exponential and time-homogeneous forward correlations on eight
/// semi-annual and six irregular rates, on flat and rising forwards, undisplaced and displaced: the times, rate times,
/// number of rates and each period's matrix; and the refused curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CotSwapFromFwdCorrelationCxxVectorTests
{
    [Fact]
    public void CotSwapFromFwdCorrelationCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/correlations/cotswapfromfwdcorrelation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PiecewiseConstantCorrelation fwd = CorrelationOf(c.Input("corr"));
            var cs = new LMMCurveState(c.Doubles("rateTimes"));
            cs.SetOnForwardRates(c.Doubles("forwards"));
            if (c.String("call") == "refused")
            {
                check.Output(c, "refused", Lazy(() => new CotSwapFromFwdCorrelation(fwd, cs, c.Double("displacement"))));
                continue;
            }

            var corr = new CotSwapFromFwdCorrelation(fwd, cs, c.Double("displacement"));
            check.Output(c, "times", corr.Times);
            check.Output(c, "rateTimes", corr.RateTimes);
            check.Output(c, "numberOfRates", corr.NumberOfRates);
            check.Output(c, "correlations", corr.Correlations().Select(Rows).ToArray());
        }

        check.Complete();
    }
}
