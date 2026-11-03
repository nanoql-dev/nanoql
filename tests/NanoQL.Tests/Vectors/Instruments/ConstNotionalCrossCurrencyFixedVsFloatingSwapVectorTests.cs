// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constant-notional cross-currency fixed-vs-floating swap vectors,
/// <c>tests/vectors/instruments/constnotionalcrosscurrencyfixedvsfloatingswap.json</c>: payer and receiver swaps of a
/// fixed leg against an Ibor or overnight leg in another currency, with payment conventions, lags and calendars,
/// indexed coupons, a stub index, seasoned and expired swaps and a refused lookback; each swap's inspectors, legs, and
/// <see cref="DiscountingConstNotionalCrossCurrencySwapEngine"/>'s results with the fair rate and spread.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstNotionalCrossCurrencyFixedVsFloatingSwapVectorTests
{
    [Fact]
    public void ConstNotionalCrossCurrencyFixedVsFloatingSwapVector()
    {
        var file = VectorFile.Load("instruments/constnotionalcrosscurrencyfixedvsfloatingswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("swap");
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => SwapOf(spec, context));
                continue;
            }

            ConstNotionalCrossCurrencyFixedVsFloatingSwap swap = SwapOf(spec, context);
            check.Output(c, "type", (int)swap.Type);
            check.Output(c, "fixedNominal", swap.FixedNominal);
            check.Output(c, "fixedCurrency", swap.FixedCurrency?.Code);
            check.Output(c, "fixedRate", swap.FixedRate);
            check.Output(c, "fixedDayCount", swap.FixedDayCount.Name);
            check.Output(c, "fixedPaymentBdc", (int)swap.FixedPaymentBdc);
            check.Output(c, "fixedPaymentLag", swap.FixedPaymentLag);
            check.Output(c, "fixedPaymentCalendar", swap.FixedPaymentCalendar?.Name);
            check.Output(c, "floatNominal", swap.FloatNominal);
            check.Output(c, "floatCurrency", swap.FloatCurrency?.Code);
            check.Output(c, "floatIndexName", swap.FloatIndex.Name);
            check.Output(c, "floatSpread", swap.FloatSpread);
            check.Output(c, "floatPaymentBdc", (int)swap.FloatPaymentBdc);
            check.Output(c, "floatPaymentLag", swap.FloatPaymentLag);
            check.Output(c, "floatPaymentCalendar", swap.FloatPaymentCalendar?.Name);
            check.Output(c, "floatCompoundSpread", swap.FloatCompoundSpread);
            check.Output(c, "floatLookbackDays", swap.FloatLookbackDays);
            check.Output(c, "floatLockoutDays", swap.FloatLockoutDays);
            check.Output(c, "floatAveragingMethod", (int)swap.FloatAveragingMethod);
            check.Output(c, "fixedSchedule", swap.FixedSchedule.Dates);
            check.Output(c, "floatSchedule", swap.FloatSchedule.Dates);
            CrossCurrencyInputs.CheckLegs(check, c, swap.Legs, context);

            CrossCurrencyInputs.EngineArguments e = CrossCurrencyInputs.EngineOf(c.Input("engine"));
            ConstNotionalCrossCurrencyFixedVsFloatingSwapResults Price() =>
                DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
                    swap,
                    context,
                    e.DomesticCcy,
                    e.DomesticCurve,
                    e.ForeignCcy,
                    e.ForeignCurve,
                    e.SpotFX,
                    e.IncludeSettlementDateFlows,
                    e.SettlementDate,
                    e.NpvDate,
                    e.SpotFXSettleDate);
            if (c.Expected.TryGetProperty("calculate", out _))
            {
                check.Output(c, "calculate", () => Price());
                continue;
            }

            ConstNotionalCrossCurrencyFixedVsFloatingSwapResults results = Price();
            CrossCurrencyInputs.CheckResults(check, c, results);
            check.Output(c, "fairRate", results.FairFixedRate);

            // a floating leg compounding its spread has its fair spread left out
            if (c.Expected.TryGetProperty("fairSpread", out _))
            {
                check.Output(c, "fairSpread", results.FairSpread);
            }
        }

        check.Complete();
    }

    private static ConstNotionalCrossCurrencyFixedVsFloatingSwap SwapOf(JsonElement spec, EvaluationContext context) =>
        new(
            (SwapType)spec.GetProperty("type").GetInt32(),
            spec.GetProperty("fixedNominal").GetDouble(),
            CrossCurrencyInputs.CurrencyOf(spec.GetProperty("fixedCurrency")),
            SwapInputs.ScheduleOf(spec.GetProperty("fixedSchedule"), context),
            spec.GetProperty("fixedRate").GetDouble(),
            CashFlowInputs.DayCounterOf(spec.GetProperty("fixedDayCount")),
            (BusinessDayConvention)spec.GetProperty("fixedPaymentBdc").GetInt32(),
            spec.GetProperty("fixedPaymentLag").GetInt32(),
            CashFlowInputs.CalendarOf(spec.GetProperty("fixedPaymentCalendar")),
            spec.GetProperty("floatNominal").GetDouble(),
            CrossCurrencyInputs.CurrencyOf(spec.GetProperty("floatCurrency")),
            SwapInputs.ScheduleOf(spec.GetProperty("floatSchedule"), context),
            CrossCurrencyInputs.IndexOf(spec.GetProperty("floatIndex")),
            spec.GetProperty("floatSpread").GetDouble(),
            (BusinessDayConvention)spec.GetProperty("floatPaymentBdc").GetInt32(),
            spec.GetProperty("floatPaymentLag").GetInt32(),
            CashFlowInputs.CalendarOf(spec.GetProperty("floatPaymentCalendar")),
            context,
            false,
            spec.GetProperty("floatCompoundSpread").GetBoolean(),
            CouponInputs.NullableInt(spec.GetProperty("floatLookbackDays")),
            spec.GetProperty("floatObservationShift").GetBoolean(),
            spec.GetProperty("floatLockoutDays").GetInt32(),
            (RateAveragingType)spec.GetProperty("floatAveragingMethod").GetInt32(),
            CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
            CrossCurrencyInputs.StubOf(spec.GetProperty("floatStubIndexSelection")));
}
