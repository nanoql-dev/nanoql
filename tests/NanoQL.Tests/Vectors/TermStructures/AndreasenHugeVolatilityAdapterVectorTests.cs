// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AndreasenHugeVolatilityAdapter vectors,
/// <c>tests/vectors/termstructures/volatility/equityfx/andreasenhugevolatilityadapter.json</c>: the Black volatility
/// structure of six calibrations at two implied-volatility accuracies, its dates, range, forwards, and its volatilities
/// and variances at the points the stability filter kept.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AndreasenHugeVolatilityAdapterVectorTests
{
    [Fact]
    public void AndreasenHugeVolatilityAdapterVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/andreasenhugevolatilityadapter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var a = new AndreasenHugeVolatilityAdapter(
                AndreasenHugeVolatilityInterplVectorTests.InterplOf(c.Input("calibration"), c.Context().EvaluationDate), c.Double("eps"));
            double[] times = c.Doubles("times");
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "referenceDate", a.ReferenceDate);
            check.Output(c, "maxDate", a.MaxDate);
            check.Output(c, "minStrike", a.MinStrike());
            check.Output(c, "maxStrike", a.MaxStrike());
            check.Output(c, "dayCounter", a.DayCounter.Name);
            check.Output(c, "atmLevel", (object?[])[.. times.Select(t => (object?)a.AtmLevel(t)!.Value)]);
            check.Output(c, "blackVol", (object?[])[.. points.Select(p => Lazy(() => a.BlackVol(p[0], p[1], true)))]);
            check.Output(c, "blackVariance", (object?[])[.. points.Select(p => Lazy(() => a.BlackVariance(p[0], p[1], true)))]);
        }

        check.Complete();
    }
}
