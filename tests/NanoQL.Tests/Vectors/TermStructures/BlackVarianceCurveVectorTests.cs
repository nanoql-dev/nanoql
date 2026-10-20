// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackVarianceCurve vectors, <c>tests/vectors/termstructures/volatility/equityfx/blackvariancecurve.json</c>:
/// curves of one to six dates, their time extrapolations and interpolations, and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackVarianceCurveVectorTests
{
    [Fact]
    public void BlackVarianceCurveVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/blackvariancecurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", Lazy(() => BlackVolOf(c.Input("structure"), context).BlackVariance(0.1, 100.0)));
                continue;
            }

            BlackVolTermStructure s = BlackVolOf(c.Input("structure"), context);
            double[] times = c.Doubles("times");
            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "dayCounter", s.DayCounter.Name);
            check.Output(c, "blackVariance", times.Select(t => Lazy(() => s.BlackVariance(t, 100.0, true))).ToArray());
            check.Output(c, "blackVol", times.Select(t => Lazy(() => s.BlackVol(t, 100.0, true))).ToArray());
            check.Output(c, "blackVarianceFarStrike", times.Select(t => Lazy(() => s.BlackVariance(t, 1e6, true))).ToArray());
            check.Output(c, "pastMaxWithoutExtrapolation", Lazy(() => s.BlackVariance(15.0, 100.0)));
        }

        check.Complete();
    }
}
