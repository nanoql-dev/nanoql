// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated year-on-year inflation curve vectors, <c>tests/vectors/termstructures/inflation/interpolatedyoyinflationcurve.json</c>:
/// <see cref="YoYInflationCurve"/>'s nodes, inspectors, base rate and year-on-year rates by date and by time, with and
/// without extrapolation, and its construction refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedYoYInflationCurveVectorTests
{
    [Fact]
    public void InterpolatedYoYInflationCurveVector()
    {
        var file = VectorFile.Load("termstructures/inflation/interpolatedyoyinflationcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("kind") == "construction")
            {
                check.Output(c, "construction", () => InflationInputs.CurveOf(c.Input("curve")) is null ? 1 : 0);
                continue;
            }

            var curve = (YoYInflationCurve)InflationInputs.CurveOf(c.Input("curve"));
            check.Output(c, "dates", curve.Dates());
            check.Output(c, "times", curve.Times());
            check.Output(c, "data", curve.Data());
            check.Output(c, "rates", curve.Rates());
            check.Output(c, "nodeDates", curve.Nodes().Select(n => n.Key));
            check.Output(c, "nodeRates", curve.Nodes().Select(n => n.Value));
            check.Output(c, "baseDate", curve.BaseDate);
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "baseRate", () => curve.BaseRate());
            bool[] extrapolate = [false, true];
            check.Output(
                c,
                "rateAtDates",
                c.DateInputs("dates").Select(d => extrapolate
                    .Select(e => (Func<object?>)(() => curve.YoyRate(d, e))).ToArray()).ToArray());
            check.Output(
                c,
                "rateAtTimes",
                c.Doubles("times").Select(t => extrapolate
                    .Select(e => (Func<object?>)(() => curve.YoyRate(t, e))).ToArray()).ToArray());
        }

        check.Complete();
    }
}
