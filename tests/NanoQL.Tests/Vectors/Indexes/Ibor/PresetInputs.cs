// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the index presets that the preset vector files describe, as
/// <c>tools/vectors/gen/indexes/ibor/euribor.py</c>'s <c>index_case</c> builds them, and checks a case's outputs.
/// </summary>
/// <remarks>
/// A preset is <c>{"type": &lt;class&gt;}</c>, with a <c>"tenor"</c> (<c>[length, units]</c>) for the classes that take
/// one, the <c>"settlementDays"</c> for the daily-tenor LIBORs, and for <c>Libor</c>, <c>DailyTenorLibor</c> and
/// <c>CustomIborIndex</c> their constructors' other arguments: the family name, the currency's code, the calendars (a
/// pair for a joint calendar), the convention, the end-of-month flag and the day counter; each is built without a
/// forwarding curve, and, where the case lists <c>"curves"</c>, on the first of them and through
/// <see cref="IborIndex.Clone"/> on the others, for its forecasts. A case without dates is a refused construction,
/// whose one output is the name's throw.
/// </remarks>
internal static class PresetInputs
{
    /// <summary>The preset a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The index.</returns>
    public static IborIndex PresetOf(JsonElement spec, YieldTermStructure? h = null)
    {
        string type = spec.GetProperty("type").GetString()!;
        return type switch
        {
            "Euribor" => new Euribor(Tenor(spec), h),
            "Euribor1W" => new Euribor1W(h),
            "Euribor1M" => new Euribor1M(h),
            "Euribor3M" => new Euribor3M(h),
            "Euribor6M" => new Euribor6M(h),
            "Euribor1Y" => new Euribor1Y(h),
            "Euribor365" => new Euribor365(Tenor(spec), h),
            "Sofr" => new Sofr(h),
            "Estr" => new Estr(h),
            "Sonia" => new Sonia(h),
            "Tonar" => new Tonar(h),
            "Tibor" => new Tibor(Tenor(spec), h),
            "Corra" => new Corra(h),
            "Libor" => new Libor(
                spec.GetProperty("familyName").GetString()!,
                Tenor(spec),
                SettlementDays(spec),
                VectorCurrencies.ByCode(spec.GetProperty("currency").GetString()!),
                CalendarOf(spec),
                DayCounterOf(spec),
                h),
            "DailyTenorLibor" => new DailyTenorLibor(
                spec.GetProperty("familyName").GetString()!,
                SettlementDays(spec),
                VectorCurrencies.ByCode(spec.GetProperty("currency").GetString()!),
                CalendarOf(spec),
                DayCounterOf(spec),
                h),
            "USDLibor" => new USDLibor(Tenor(spec), h),
            "USDLiborON" => new USDLiborON(h),
            "DailyTenorUSDLibor" => new DailyTenorUSDLibor(SettlementDays(spec), h),
            "GBPLibor" => new GBPLibor(Tenor(spec), h),
            "GBPLiborON" => new GBPLiborON(h),
            "DailyTenorGBPLibor" => new DailyTenorGBPLibor(SettlementDays(spec), h),
            "JPYLibor" => new JPYLibor(Tenor(spec), h),
            "DailyTenorJPYLibor" => new DailyTenorJPYLibor(SettlementDays(spec), h),
            "DailyTenorCHFLibor" => new DailyTenorCHFLibor(SettlementDays(spec), h),
            "DailyTenorEURLibor" => new DailyTenorEURLibor(SettlementDays(spec), h),
            "EURLiborON" => new EURLiborON(h),
            "Eonia" => new Eonia(h),
            "FedFunds" => new FedFunds(h),
            "Bkbm" => new Bkbm(Tenor(spec), h),
            "Bkbm1M" => new Bkbm1M(h),
            "Bkbm2M" => new Bkbm2M(h),
            "Bkbm3M" => new Bkbm3M(h),
            "Bkbm4M" => new Bkbm4M(h),
            "Bkbm5M" => new Bkbm5M(h),
            "Bkbm6M" => new Bkbm6M(h),
            "Aonia" => new Aonia(h),
            "Cdi" => new Cdi(h),
            "Destr" => new Destr(h),
            "Kofr" => new Kofr(h),
            "Nzocr" => new Nzocr(h),
            "Saron" => new Saron(h),
            "Shir" => new Shir(h),
            "Swestr" => new Swestr(h),
            "Zaronia" => new Zaronia(h),
            "CADLiborON" => new CADLiborON(h),
            "EURLibor1M" => new EURLibor1M(h),
            "EURLibor3M" => new EURLibor3M(h),
            "EURLibor6M" => new EURLibor6M(h),
            "EURLibor1Y" => new EURLibor1Y(h),
            "BiborSW" => new BiborSW(h),
            "Bibor1M" => new Bibor1M(h),
            "Bibor2M" => new Bibor2M(h),
            "Bibor3M" => new Bibor3M(h),
            "Bibor6M" => new Bibor6M(h),
            "Bibor1Y" => new Bibor1Y(h),
            "Bbsw1M" => new Bbsw1M(h),
            "Bbsw2M" => new Bbsw2M(h),
            "Bbsw3M" => new Bbsw3M(h),
            "Bbsw4M" => new Bbsw4M(h),
            "Bbsw5M" => new Bbsw5M(h),
            "Bbsw6M" => new Bbsw6M(h),
            "AUDLibor" => new AUDLibor(Tenor(spec), h),
            "Bbsw" => new Bbsw(Tenor(spec), h),
            "Bibor" => new Bibor(Tenor(spec), h),
            "CADLibor" => new CADLibor(Tenor(spec), h),
            "Cdor" => new Cdor(Tenor(spec), h),
            "CHFLibor" => new CHFLibor(Tenor(spec), h),
            "DKKLibor" => new DKKLibor(Tenor(spec), h),
            "EURLibor" => new EURLibor(Tenor(spec), h),
            "Jibar" => new Jibar(Tenor(spec), h),
            "Mosprime" => new Mosprime(Tenor(spec), h),
            "Nibor" => new Nibor(Tenor(spec), h),
            "NZDLibor" => new NZDLibor(Tenor(spec), h),
            "Pribor" => new Pribor(Tenor(spec), h),
            "Robor" => new Robor(Tenor(spec), h),
            "SEKLibor" => new SEKLibor(Tenor(spec), h),
            "Shibor" => new Shibor(Tenor(spec), h),
            "THBFIX" => new THBFIX(Tenor(spec), h),
            "TRLibor" => new TRLibor(Tenor(spec), h),
            "Wibor" => new Wibor(Tenor(spec), h),
            "Zibor" => new Zibor(Tenor(spec), h),
            "CustomIborIndex" => new CustomIborIndex(
                spec.GetProperty("familyName").GetString()!,
                Tenor(spec),
                SettlementDays(spec),
                VectorCurrencies.ByCode(spec.GetProperty("currency").GetString()!),
                CalendarNamed(spec.GetProperty("fixingCalendar")),
                CalendarNamed(spec.GetProperty("valueCalendar")),
                CalendarNamed(spec.GetProperty("maturityCalendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("endOfMonth").GetBoolean(),
                DayCounterOf(spec),
                h),
            _ => throw new InvalidOperationException($"unknown preset '{type}'"),
        };
    }

    /// <summary>
    /// Checks one case: the preset's name, conventions and fixing calendar, and its fixing, value and maturity dates
    /// at the case's dates; or, for a refused construction, the name's throw. A case's <c>"skip"</c> entries, the
    /// outputs QuantLib's master moves ahead of the pin (ROBOR's, #2919), are null on both sides.
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
        var outputs = new Dictionary<string, Func<object?>[]>
        {
            ["fixingDate"] = Each(dates, index.FixingDate),
            ["valueDate"] = Each(dates, index.ValueDate),
            ["maturityDate"] = Each(dates, index.MaturityDate),
        };
        var forecasts = new List<Func<object?>[]>();
        if (c.Inputs.TryGetProperty("curves", out JsonElement curves))
        {
            // the forecasts on each curve: index_forecast_case's, the preset built on the first curve and cloned onto the
            // others, QuantLib's two routes to an index on a curve
            EvaluationContext context = c.Context();
            foreach (JsonElement curve in curves.EnumerateArray())
            {
                YieldTermStructure h = EquityInputs.CurveOf(curve);
                IborIndex onCurve = forecasts.Count == 0 ? PresetOf(spec, h) : PresetOf(spec).Clone(h);
                forecasts.Add(Each(dates, d => onCurve.Fixing(d, context)));
            }
        }

        if (c.Inputs.TryGetProperty("skip", out JsonElement skip))
        {
            foreach (JsonElement entry in skip.EnumerateArray())
            {
                string output = entry[0].GetString()!;
                Func<object?>[] row = output == "forecast" ? forecasts[entry[1].GetInt32()] : outputs[output];
                row[entry[entry.GetArrayLength() - 1].GetInt32()] = () => null;
            }
        }

        foreach (string name in (string[])["fixingDate", "valueDate", "maturityDate"])
        {
            check.Output(c, name, outputs[name]);
        }

        if (curves.ValueKind != JsonValueKind.Undefined)
        {
            check.Output(c, "forecast", forecasts);
        }
    }

    private static Period Tenor(JsonElement spec)
    {
        JsonElement tenor = spec.GetProperty("tenor");
        return new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32());
    }

    private static int SettlementDays(JsonElement spec) => spec.GetProperty("settlementDays").GetInt32();

    private static Calendar CalendarOf(JsonElement spec) => CalendarNamed(spec.GetProperty("calendar"));

    // A calendar by its name in the spec, or a joint calendar of two, joining their holidays.
    private static Calendar CalendarNamed(JsonElement name) => name.ValueKind == JsonValueKind.Array
        ? new JointCalendar(CalendarNamed(name[0]), CalendarNamed(name[1]), JointCalendarRule.JoinHolidays)
        : name.GetString() switch
        {
            "Canada" => new Canada(),
            "Switzerland" => new Switzerland(),
            "TARGET" => new TARGET(),
            "UnitedKingdom" => new UnitedKingdom(UnitedKingdomMarket.Exchange),
            "UnitedStates" => new UnitedStates(UnitedStatesMarket.Settlement),
            "Japan" => new Japan(),
            var other => throw new InvalidOperationException($"unknown calendar '{other}'"),
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
