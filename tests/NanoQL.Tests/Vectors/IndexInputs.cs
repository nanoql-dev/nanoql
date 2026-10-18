// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the indexes and replays the fixing-store steps that the index vector files describe in their inputs, as
/// their generators build them (<c>tools/vectors/gen/indexes/interestrateindex.py</c>'s functions).
/// </summary>
/// <remarks>
/// An index is <c>{"type": "IborIndex", "familyName", "tenor", "settlementDays", "fixingCalendar", "convention",
/// "endOfMonth", "dayCounter", "curve"}</c> or <c>{"type": "OvernightIndex", "familyName", "settlementDays",
/// "fixingCalendar", "dayCounter", "curve"}</c>, always with QuantLib's empty currency (<see langword="null"/>); the
/// C++ harness's files add <c>{"type": "NoNativeFixingsIndex", "name"}</c>, an index whose fixings may not be stored.
/// A curve is <see langword="null"/> or a flat forward curve spec, as <see cref="CashFlowInputs.CurveOf"/> reads it. A
/// step is <c>{"op": "addFixing", "index", "date", "value", "forceOverwrite"}</c>, <c>{"op": "addFixings", "index",
/// "dates", "values", "forceOverwrite"}</c>, the same with the op <c>"addFixings(TimeSeries)"</c> for the overload
/// taking a series, or <c>{"op": "clearFixings", "index"}</c>, its index a position in the case's indexes.
/// </remarks>
internal static class IndexInputs
{
    /// <summary>The Ibor or overnight index a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static IborIndex IborIndexOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        string familyName = spec.GetProperty("familyName").GetString()!;
        int settlementDays = spec.GetProperty("settlementDays").GetInt32();
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("fixingCalendar"));
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        YieldTermStructure? curve = CurveOf(spec.GetProperty("curve"));
        switch (type)
        {
            case "IborIndex":
                JsonElement tenor = spec.GetProperty("tenor");
                return new IborIndex(
                    familyName,
                    new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()),
                    settlementDays,
                    null,
                    calendar,
                    (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                    spec.GetProperty("endOfMonth").GetBoolean(),
                    dayCounter,
                    curve);
            case "OvernightIndex":
                return new OvernightIndex(familyName, settlementDays, null, calendar, dayCounter, curve);
            default:
                throw new InvalidOperationException($"unknown index type '{type}'");
        }
    }

    /// <summary>The index a spec describes, the C++ harness's index without native fixings among them.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static FinancialIndex IndexOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "NoNativeFixingsIndex"
            ? new NoNativeFixingsIndex(spec.GetProperty("name").GetString()!)
            : IborIndexOf(spec);

    /// <summary>The forwarding curve a spec describes, or <see langword="null"/> for none.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The curve, or <see langword="null"/>.</returns>
    public static YieldTermStructure? CurveOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(spec);

    /// <summary>The store after one step.</summary>
    /// <param name="store">The store before it.</param>
    /// <param name="step">The step.</param>
    /// <param name="indexes">The case's indexes, which the step names by position.</param>
    /// <returns>The store after it.</returns>
    public static IndexFixings Apply(IndexFixings store, JsonElement step, IReadOnlyList<FinancialIndex> indexes)
    {
        FinancialIndex index = indexes[step.GetProperty("index").GetInt32()];
        string op = step.GetProperty("op").GetString()!;
        switch (op)
        {
            case "addFixing":
                return store.AddFixing(
                    index,
                    new Date(step.GetProperty("date").GetInt32()),
                    Real(step.GetProperty("value")),
                    step.GetProperty("forceOverwrite").GetBoolean());
            case "addFixings":
                return store.AddFixings(
                    index,
                    Dates(step.GetProperty("dates")),
                    Reals(step.GetProperty("values")),
                    step.GetProperty("forceOverwrite").GetBoolean());
            case "addFixings(TimeSeries)":
                return store.AddFixings(
                    index,
                    new TimeSeries<double>(Dates(step.GetProperty("dates")), Reals(step.GetProperty("values"))),
                    step.GetProperty("forceOverwrite").GetBoolean());
            case "clearFixings":
                return store.ClearFixings(index);
            default:
                throw new InvalidOperationException($"unknown step '{op}'");
        }
    }

    /// <summary>A real, which the vectors write as a number or as "NaN", "Infinity" or "-Infinity".</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The real.</returns>
    public static double Real(JsonElement e) =>
        e.ValueKind == JsonValueKind.String
            ? double.Parse(e.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : e.GetDouble();

    private static Date[] Dates(JsonElement e) => e.EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray();

    private static double[] Reals(JsonElement e) => e.EnumerateArray().Select(Real).ToArray();

    /// <summary>
    /// The C++ harness's index whose fixings may not be stored: QuantLib refuses every write for it, as for an index
    /// whose fixings are those of the indexes it is built from.
    /// </summary>
    /// <param name="name">The name.</param>
    private sealed class NoNativeFixingsIndex(string name) : FinancialIndex
    {
        public override string Name { get; } = name;

        public override Calendar FixingCalendar { get; } = new NullCalendar();

        public override bool AllowsNativeFixings => false;

        public override bool IsValidFixingDate(Date fixingDate) => true;

        public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false) =>
            throw new InvalidOperationException("the harness's index without native fixings forecasts nothing");
    }
}
