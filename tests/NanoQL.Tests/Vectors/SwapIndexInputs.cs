// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the swap indexes that the swap-index vector files describe in their inputs, as their generators build them
/// (<c>tools/vectors/gen/indexes/swapindex.py</c>'s functions).
/// </summary>
/// <remarks>
/// A swap index is <c>{"type": "SwapIndex", "familyName", "tenor", "settlementDays", "currency", "fixingCalendar",
/// "fixedLegTenor", "fixedLegConvention", "fixedLegDayCounter", "iborIndex", "exogenous",
/// "discountingTermStructure"}</c>, built with the constructor taking a discounting curve when <c>"exogenous"</c> is
/// set, even on a null curve; or <c>{"type": "OvernightIndexedSwapIndex", "familyName", "tenor", "settlementDays",
/// "currency", "overnightIndex", "telescopicValueDates", "averagingMethod"}</c>. A period is <c>[length, units]</c>; a
/// currency a code of <see cref="SwapInputs.CurrencyOf"/>, or <c>null</c> for QuantLib's empty one; calendars, day
/// counters and curves are specs of <see cref="CashFlowInputs"/> and <see cref="IndexInputs"/>, the Ibor index a spec of
/// <see cref="SwapInputs"/> and the overnight index one of <see cref="IndexInputs"/>.
/// </remarks>
internal static class SwapIndexInputs
{
    /// <summary>The swap index a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static SwapIndex SwapIndexOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        string familyName = spec.GetProperty("familyName").GetString()!;
        Period tenor = SwapInputs.PeriodOf(spec.GetProperty("tenor"));
        int settlementDays = spec.GetProperty("settlementDays").GetInt32();
        JsonElement code = spec.GetProperty("currency");
        Currency? currency = code.ValueKind == JsonValueKind.Null ? null : SwapInputs.CurrencyOf(code.GetString()!);
        switch (type)
        {
            case "SwapIndex":
                Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("fixingCalendar"));
                Period fixedLegTenor = SwapInputs.PeriodOf(spec.GetProperty("fixedLegTenor"));
                var convention = (BusinessDayConvention)spec.GetProperty("fixedLegConvention").GetInt32();
                DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("fixedLegDayCounter"));
                IborIndex iborIndex = SwapInputs.IndexOf(spec.GetProperty("iborIndex"));
                return spec.GetProperty("exogenous").GetBoolean()
                    ? new SwapIndex(
                        familyName,
                        tenor,
                        settlementDays,
                        currency,
                        calendar,
                        fixedLegTenor,
                        convention,
                        dayCounter,
                        iborIndex,
                        IndexInputs.CurveOf(spec.GetProperty("discountingTermStructure")))
                    : new SwapIndex(
                        familyName,
                        tenor,
                        settlementDays,
                        currency,
                        calendar,
                        fixedLegTenor,
                        convention,
                        dayCounter,
                        iborIndex);
            case "OvernightIndexedSwapIndex":
                return new OvernightIndexedSwapIndex(
                    familyName,
                    tenor,
                    settlementDays,
                    currency,
                    (OvernightIndex)IndexInputs.IborIndexOf(spec.GetProperty("overnightIndex")),
                    spec.GetProperty("telescopicValueDates").GetBoolean(),
                    (RateAveragingType)spec.GetProperty("averagingMethod").GetInt32());
            default:
                throw new InvalidOperationException($"unknown swap index type '{type}'");
        }
    }
}
