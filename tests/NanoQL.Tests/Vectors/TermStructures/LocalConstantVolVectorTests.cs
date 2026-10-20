// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The LocalConstantVol vectors, <c>tests/vectors/termstructures/volatility/equityfx/localconstantvol.json</c>: fixed
/// and moving structures, their dates, strikes, conventions and local volatilities, through the base's localVol.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LocalConstantVolVectorTests
{
    [Fact]
    public void LocalConstantVolVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/localconstantvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            double vol = c.Double("volatility");
            if (c.Inputs.TryGetProperty("settlementDays", out _))
            {
                LocalConstantVol moving = LocalConstantVol.Create(
                    c.Int("settlementDays"), CashFlowInputs.CalendarOf(c.Input("calendar")), vol, dc, context);
                check.Output(c, "referenceDate", moving.ReferenceDate);
                check.Output(c, "localVol", moving.LocalVol(c.DateInput("date"), 100.0));
                continue;
            }

            Date reference = c.DateInput("referenceDate");
            var s = new LocalConstantVol(reference, vol, dc);
            double[] times = c.Doubles("times"), levels = c.Doubles("levels");
            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "dayCounter", s.DayCounter.Name);
            check.Output(c, "calendarEmpty", s.Calendar is null);
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(c, "localVol", times.Select(t => (object?)levels.Select(x => (object?)s.LocalVol(t, x)).ToArray()).ToArray());
            check.Output(c, "localVolDate", c.DateInputs("dates").Select(d => (object?)s.LocalVol(d, 100.0)).ToArray());
            check.Output(c, "negativeTime", Lazy(() => s.LocalVol(-0.5, 100.0)));
            check.Output(c, "dateBeforeReference", Lazy(() => s.LocalVol(reference - 1, 100.0)));
        }

        check.Complete();
    }
}
