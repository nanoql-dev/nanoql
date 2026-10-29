// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's interpolated year-on-year optionlet volatility curve vectors,
/// <c>tests/vectors/cxx/experimental/inflation/yoyinflationoptionletvolatilitystructure2.json</c>: linear curves'
/// dates, base level, nodes, and volatilities and total variances at dates, with extrapolation.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedYoYOptionletVolatilityCurveCxxVectorTests
{
    [Fact]
    public void InterpolatedYoYOptionletVolatilityCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/inflation/yoyinflationoptionletvolatilitystructure2");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("curve");
            var curve = InterpolatedYoYOptionletVolatilityCurve<Linear>.Create(
                s.GetProperty("settlementDays").GetInt32(),
                CashFlowInputs.CalendarOf(s.GetProperty("calendar")),
                (BusinessDayConvention)s.GetProperty("convention").GetInt32(),
                CashFlowInputs.DayCounterOf(s.GetProperty("dayCounter")),
                InflationInputs.LagOf(s.GetProperty("lag")),
                (Frequency)s.GetProperty("frequency").GetInt32(),
                s.GetProperty("indexIsInterpolated").GetBoolean(),
                s.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                s.GetProperty("volatilities").EnumerateArray().Select(x => x.GetDouble()),
                s.GetProperty("minStrike").GetDouble(),
                s.GetProperty("maxStrike").GetDouble(),
                context);
            double strike = c.Input("strike").GetDouble();
            Date[] dates = c.DateInputs("dates");
            check.Output(c, "referenceDate", curve.ReferenceDate);
            check.Output(c, "baseDate", curve.BaseDate());
            check.Output(c, "baseLevel", curve.BaseLevel());
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "times", curve.Times());
            check.Output(c, "dates", curve.Dates().Select(d => (object?)d).ToArray());
            check.Output(c, "data", curve.Data());
            check.Output(c, "nodes", curve.Nodes().Select(n => (object?)new object?[] { n.Key, n.Value }).ToArray());
            check.Output(c, "volatility", dates.Select(d => (Func<object?>)(() =>
                curve.Volatility(d, strike, null, true))).ToArray());
            check.Output(c, "totalVariance", dates.Select(d => (Func<object?>)(() =>
                curve.TotalVariance(d, strike, null, true))).ToArray());
        }

        check.Complete();
    }
}
