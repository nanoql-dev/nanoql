// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CPI bond vectors, <c>tests/vectors/instruments/bonds/cpibond.json</c>: <see cref="CPIBond"/>'s cash flows,
/// notionals and redemption, and its prices by the discounting bond engine.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CPIBondVectorTests
{
    [Fact]
    public void CPIBondVector()
    {
        var file = VectorFile.Load("instruments/bonds/cpibond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            CPIBond bond = BondOf(c.Input("bond"), (ZeroInflationIndex)index, context);
            YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
            BondResults results = DiscountingBondEngine.Calculate(bond, context, nominal);
            check.Output(c, "dates", bond.Cashflows.Select(f => f.Date));
            check.Output(c, "amounts", bond.Cashflows.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
            check.Output(c, "notionals", bond.Notionals);
            check.Output(c, "maturityDate", bond.MaturityDate());
            check.Output(c, "redemptionDate", bond.Redemption().Date);
            check.Output(c, "redemptionAmount", () => bond.Redemption().Amount(context));
            check.Output(c, "settlementDate", bond.SettlementDate(context));
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "cleanPrice", () => bond.CleanPrice(results, context));
            check.Output(c, "dirtyPrice", () => bond.DirtyPrice(results, context));
            check.Output(c, "accruedAmount", () => bond.AccruedAmount(context));
            check.Output(c, "settlementValue", results.SettlementValue);
        }

        check.Complete();
    }

    /// <summary>The bond a <c>"bond"</c> spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The index.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The bond.</returns>
    internal static CPIBond BondOf(JsonElement spec, ZeroInflationIndex index, EvaluationContext context)
    {
        JsonElement ex = spec.GetProperty("exCouponPeriod");
        bool hasExCoupon = ex.ValueKind == JsonValueKind.Object;
        return new CPIBond(
            spec.GetProperty("settlementDays").GetInt32(),
            spec.GetProperty("faceAmount").GetDouble(),
            InflationCashFlowInputs.NullableDoubleOf(spec.GetProperty("baseCPI")),
            InflationCashFlowInputs.PeriodOf(spec.GetProperty("observationLag")),
            index,
            (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32(),
            CashFlowInputs.ScheduleOf(spec.GetProperty("schedule"), context),
            spec.GetProperty("coupons").EnumerateArray().Select(x => x.GetDouble()),
            CashFlowInputs.DayCounterOf(spec.GetProperty("accrualDayCounter")),
            (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
            InflationCashFlowInputs.NullableDateOf(spec.GetProperty("issueDate")),
            CashFlowInputs.NullableCalendarOf(spec.GetProperty("paymentCalendar")),
            hasExCoupon ? InflationCashFlowInputs.PeriodOf(ex.GetProperty("period")) : default,
            hasExCoupon ? CashFlowInputs.CalendarOf(ex.GetProperty("calendar")) : null,
            hasExCoupon ? (BusinessDayConvention)ex.GetProperty("convention").GetInt32() : BusinessDayConvention.Unadjusted,
            hasExCoupon && ex.GetProperty("endOfMonth").GetBoolean());
    }
}
