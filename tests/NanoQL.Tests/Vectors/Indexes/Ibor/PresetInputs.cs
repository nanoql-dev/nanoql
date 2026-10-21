// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the index presets that the preset vector files describe, as
/// <c>tools/vectors/gen/indexes/ibor/euribor.py</c>'s <c>index_case</c> builds them, and checks a case's outputs.
/// </summary>
/// <remarks>
/// A preset is <c>{"type": &lt;class&gt;}</c>, with a <c>"tenor"</c> (<c>[length, units]</c>) for the classes that take
/// one, the <c>"settlementDays"</c> for the daily-tenor LIBORs, and for <c>Libor</c> and <c>DailyTenorLibor</c> their
/// constructors' other arguments: the family name, the currency's code, the financial center's calendar and the day
/// counter, by name; each is built without a forwarding curve. A case without dates is a refused construction, whose
/// one output is the name's throw.
/// </remarks>
internal static class PresetInputs
{
    /// <summary>The preset a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static IborIndex PresetOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        return type switch
        {
            "Euribor" => new Euribor(Tenor(spec)),
            "Euribor1W" => new Euribor1W(),
            "Euribor1M" => new Euribor1M(),
            "Euribor3M" => new Euribor3M(),
            "Euribor6M" => new Euribor6M(),
            "Euribor1Y" => new Euribor1Y(),
            "Euribor365" => new Euribor365(Tenor(spec)),
            "Sofr" => new Sofr(),
            "Estr" => new Estr(),
            "Sonia" => new Sonia(),
            "Tonar" => new Tonar(),
            "Tibor" => new Tibor(Tenor(spec)),
            "Corra" => new Corra(),
            "Libor" => new Libor(
                spec.GetProperty("familyName").GetString()!,
                Tenor(spec),
                SettlementDays(spec),
                VectorCurrencies.ByCode(spec.GetProperty("currency").GetString()!),
                CalendarOf(spec),
                DayCounterOf(spec)),
            "DailyTenorLibor" => new DailyTenorLibor(
                spec.GetProperty("familyName").GetString()!,
                SettlementDays(spec),
                VectorCurrencies.ByCode(spec.GetProperty("currency").GetString()!),
                CalendarOf(spec),
                DayCounterOf(spec)),
            "USDLibor" => new USDLibor(Tenor(spec)),
            "USDLiborON" => new USDLiborON(),
            "DailyTenorUSDLibor" => new DailyTenorUSDLibor(SettlementDays(spec)),
            "GBPLibor" => new GBPLibor(Tenor(spec)),
            "GBPLiborON" => new GBPLiborON(),
            "DailyTenorGBPLibor" => new DailyTenorGBPLibor(SettlementDays(spec)),
            "JPYLibor" => new JPYLibor(Tenor(spec)),
            "DailyTenorJPYLibor" => new DailyTenorJPYLibor(SettlementDays(spec)),
            "Eonia" => new Eonia(),
            "FedFunds" => new FedFunds(),
            "Bkbm" => new Bkbm(Tenor(spec)),
            "Bkbm1M" => new Bkbm1M(),
            "Bkbm2M" => new Bkbm2M(),
            "Bkbm3M" => new Bkbm3M(),
            "Bkbm4M" => new Bkbm4M(),
            "Bkbm5M" => new Bkbm5M(),
            "Bkbm6M" => new Bkbm6M(),
            _ => throw new InvalidOperationException($"unknown preset '{type}'"),
        };
    }

    /// <summary>
    /// Checks one case: the preset's name, conventions and fixing calendar, and its fixing, value and maturity dates
    /// at the case's dates; or, for a refused construction, the name's throw.
    /// </summary>
    /// <param name="check">The file's check.</param>
    /// <param name="c">The case.</param>
    public static void Check(VectorAssert check, VectorCase c)
    {
        JsonElement spec = c.Input("index");
        if (!c.Inputs.TryGetProperty("dates", out _))
        {
            check.Output(c, "name", () => PresetOf(spec).Name);
            return;
        }

        IborIndex index = PresetOf(spec);
        Date[] dates = c.DateInputs("dates");
        check.Output(c, "name", index.Name);
        check.Output(c, "familyName", index.FamilyName);
        check.Output(c, "tenor", new object[] { index.Tenor.Length, (int)index.Tenor.Units });
        check.Output(c, "fixingDays", index.FixingDays);
        check.Output(c, "currency", index.Currency?.Code);
        check.Output(c, "fixingCalendar", index.FixingCalendar.Name);
        check.Output(c, "dayCounter", index.DayCounter.Name);
        check.Output(c, "businessDayConvention", index.BusinessDayConvention);
        check.Output(c, "endOfMonth", index.EndOfMonth);
        check.Output(c, "fixingDate", Each(dates, index.FixingDate));
        check.Output(c, "valueDate", Each(dates, index.ValueDate));
        check.Output(c, "maturityDate", Each(dates, index.MaturityDate));
    }

    private static Period Tenor(JsonElement spec)
    {
        JsonElement tenor = spec.GetProperty("tenor");
        return new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32());
    }

    private static int SettlementDays(JsonElement spec) => spec.GetProperty("settlementDays").GetInt32();

    private static Calendar CalendarOf(JsonElement spec) => spec.GetProperty("calendar").GetString() switch
    {
        "Canada" => new Canada(),
        "Switzerland" => new Switzerland(),
        "TARGET" => new TARGET(),
        var name => throw new InvalidOperationException($"unknown calendar '{name}'"),
    };

    private static DayCounter DayCounterOf(JsonElement spec) => spec.GetProperty("dayCounter").GetString() switch
    {
        "Actual365Fixed" => new Actual365Fixed(),
        "Actual360" => new Actual360(),
        var name => throw new InvalidOperationException($"unknown day counter '{name}'"),
    };

    private static Func<object?>[] Each<TOut>(Date[] dates, Func<Date, TOut> f) =>
        dates.Select(d => (Func<object?>)(() => f(d))).ToArray();
}
