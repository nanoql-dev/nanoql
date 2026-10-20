// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CPI bond helper vectors, <c>tests/vectors/termstructures/yield/cpibondhelper.json</c>:
/// <see cref="CPIBondHelper"/>'s dates and its implied quote on a curve, against QuantLib's bond helper on the same CPI
/// bond.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CPIBondHelperVectorTests
{
    [Fact]
    public void CPIBondHelperVector()
    {
        var file = VectorFile.Load("termstructures/yield/cpibondhelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            JsonElement spec = c.Input("bond");
            JsonElement ex = spec.GetProperty("exCouponPeriod");
            bool hasExCoupon = ex.ValueKind == JsonValueKind.Object;
            var helper = new CPIBondHelper(
                c.Double("price"),
                spec.GetProperty("settlementDays").GetInt32(),
                spec.GetProperty("faceAmount").GetDouble(),
                InflationCashFlowInputs.NullableDoubleOf(spec.GetProperty("baseCPI")),
                InflationCashFlowInputs.PeriodOf(spec.GetProperty("observationLag")),
                (ZeroInflationIndex)index,
                (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32(),
                CashFlowInputs.ScheduleOf(spec.GetProperty("schedule"), context),
                spec.GetProperty("coupons").EnumerateArray().Select(x => x.GetDouble()),
                CashFlowInputs.DayCounterOf(spec.GetProperty("accrualDayCounter")),
                context,
                (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
                InflationCashFlowInputs.NullableDateOf(spec.GetProperty("issueDate")),
                CashFlowInputs.NullableCalendarOf(spec.GetProperty("paymentCalendar")),
                hasExCoupon ? InflationCashFlowInputs.PeriodOf(ex.GetProperty("period")) : default,
                hasExCoupon ? CashFlowInputs.CalendarOf(ex.GetProperty("calendar")) : null,
                hasExCoupon ? (BusinessDayConvention)ex.GetProperty("convention").GetInt32() : BusinessDayConvention.Unadjusted,
                hasExCoupon && ex.GetProperty("endOfMonth").GetBoolean(),
                (BondPriceType)c.Int("priceType"));
            CPIBondHelper bound = helper.SetTermStructure(CashFlowInputs.CurveOf(c.Input("curve")));
            check.Output(c, "earliestDate", helper.EarliestDate);
            check.Output(c, "maturityDate", helper.MaturityDate);
            check.Output(c, "latestRelevantDate", helper.LatestRelevantDate);
            check.Output(c, "pillarDate", helper.PillarDate);
            check.Output(c, "latestDate", helper.LatestDate);
            check.Output(c, "impliedQuote", () => bound.ImpliedQuote(context));
            check.Output(c, "quoteError", () => bound.QuoteError(context));
        }

        check.Complete();
    }
}
