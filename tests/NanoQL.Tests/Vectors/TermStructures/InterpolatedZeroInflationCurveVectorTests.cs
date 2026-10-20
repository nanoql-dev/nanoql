// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated zero-inflation curve vectors, <c>tests/vectors/termstructures/inflation/interpolatedzeroinflationcurve.json</c>:
/// <see cref="ZeroInflationCurve"/>'s nodes, inspectors and zero rates by date and by time, with and without
/// extrapolation, and its construction refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedZeroInflationCurveVectorTests
{
    [Fact]
    public void InterpolatedZeroInflationCurveVector()
    {
        var file = VectorFile.Load("termstructures/inflation/interpolatedzeroinflationcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("kind") == "construction")
            {
                check.Output(c, "construction", () => InflationInputs.CurveOf(c.Input("curve")) is null ? 1 : 0);
                continue;
            }

            var curve = (ZeroInflationCurve)InflationInputs.CurveOf(c.Input("curve"));
            check.Output(c, "dates", curve.Dates());
            check.Output(c, "times", curve.Times());
            check.Output(c, "data", curve.Data());
            check.Output(c, "rates", curve.Rates());
            check.Output(c, "nodeDates", curve.Nodes().Select(n => n.Key));
            check.Output(c, "nodeRates", curve.Nodes().Select(n => n.Value));
            check.Output(c, "baseDate", curve.BaseDate);
            check.Output(c, "maxDate", curve.MaxDate);
            bool[] extrapolate = [false, true];
            check.Output(
                c,
                "rateAtDates",
                c.DateInputs("dates").Select(d => extrapolate
                    .Select(e => (Func<object?>)(() => curve.ZeroRate(d, e))).ToArray()).ToArray());
            check.Output(
                c,
                "rateAtTimes",
                c.Doubles("times").Select(t => extrapolate
                    .Select(e => (Func<object?>)(() => curve.ZeroRate(t, e))).ToArray()).ToArray());
        }

        check.Complete();
    }
}
