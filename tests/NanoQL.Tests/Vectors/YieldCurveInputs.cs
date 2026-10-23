// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the original curves the derived-curve vector files describe, as
/// <c>tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py</c>'s <c>original</c> builds them, and
/// checks a derived curve's inspectors and reads as
/// <c>tools/vectors/gen/termstructures/yield/forwardspreadedtermstructure.py</c>'s <c>curve_outputs</c> records them.
/// </summary>
/// <remarks>
/// An original is <c>{"type": "FlatForward", "referenceDate", "forward", "dayCounter", "compounding",
/// "frequency"}</c>, the same with <c>"settlementDays"</c> and <c>"calendar"</c> in place of the reference date (built
/// by <see cref="FlatForward.Create"/> in the case's context), or <c>{"type": "DiscountCurve" | "ZeroCurve" |
/// "ForwardCurve", "dates", "values", "dayCounter", "calendar", "jumps", "jumpDates"}</c>, a null calendar QuantLib's
/// empty one and null jumps none; each with <c>"extrapolate"</c>.
/// </remarks>
internal static class YieldCurveInputs
{
    /// <summary>The original a spec describes, extrapolating where it says so.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure OriginalOf(JsonElement spec, EvaluationContext context)
    {
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        YieldTermStructure curve;
        if (spec.GetProperty("type").GetString() == "FlatForward")
        {
            double forward = spec.GetProperty("forward").GetDouble();
            var compounding = (Compounding)spec.GetProperty("compounding").GetInt32();
            var frequency = (Frequency)spec.GetProperty("frequency").GetInt32();
            curve = spec.TryGetProperty("referenceDate", out JsonElement reference)
                ? new FlatForward(new Date(reference.GetInt32()), forward, dc, compounding, frequency)
                : FlatForward.Create(
                    spec.GetProperty("settlementDays").GetInt32(),
                    CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                    forward,
                    dc,
                    context,
                    compounding,
                    frequency);
        }
        else
        {
            Date[] dates = spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray();
            double[] values = spec.GetProperty("values").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            Calendar? calendar = CashFlowInputs.NullableCalendarOf(spec.GetProperty("calendar"));
            double[]? jumps = spec.GetProperty("jumps").ValueKind == JsonValueKind.Null
                ? null
                : spec.GetProperty("jumps").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            Date[]? jumpDates = spec.GetProperty("jumpDates").ValueKind == JsonValueKind.Null
                ? null
                : spec.GetProperty("jumpDates").EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray();
            curve = spec.GetProperty("type").GetString() switch
            {
                "DiscountCurve" => new DiscountCurve(dates, values, dc, calendar, jumps, jumpDates),
                "ZeroCurve" => new ZeroCurve(dates, values, dc, calendar, jumps, jumpDates),
                "ForwardCurve" => new ForwardCurve(dates, values, dc, calendar, jumps, jumpDates),
                var t => throw new InvalidOperationException($"unknown original '{t}'"),
            };
        }

        return spec.GetProperty("extrapolate").GetBoolean() ? curve.EnableExtrapolation() : curve;
    }

    /// <summary>
    /// Checks a derived curve's reference date, day counter, calendar, maximum date and time and extrapolation flag, and
    /// its discount, Actual/365 (Fixed) continuous zero rate, semiannual six-month forward rate and instantaneous
    /// forward rate at the dates.
    /// </summary>
    /// <param name="check">The file's assertions.</param>
    /// <param name="c">The case.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="dates">The dates.</param>
    public static void CheckCurve(VectorAssert check, VectorCase c, YieldTermStructure curve, Date[] dates)
    {
        var a365 = new Actual365Fixed();
        check.Output(c, "referenceDate", curve.ReferenceDate);
        check.Output(c, "dayCounter", curve.DayCounter.Name);
        check.Output(c, "calendar", curve.Calendar?.Name);
        check.Output(c, "maxDate", () => curve.MaxDate);
        check.Output(c, "maxTime", () => curve.MaxTime);
        check.Output(c, "allowsExtrapolation", curve.AllowsExtrapolation);
        check.Output(c, "discount", Each(dates, d => curve.Discount(d)));
        check.Output(c, "zeroRate", Each(dates, d => curve.ZeroRate(d, a365, Compounding.Continuous, Frequency.Annual).Rate));
        check.Output(
            c,
            "forwardRate",
            Each(dates, d => curve.ForwardRate(
                d, d + new Period(6, TimeUnit.Months), a365, Compounding.Compounded, Frequency.Semiannual).Rate));
        check.Output(
            c,
            "instantaneousForward",
            Each(dates, d => curve.ForwardRate(d, d, a365, Compounding.Continuous, Frequency.NoFrequency).Rate));
    }

    /// <summary>A read at each date, deferred so that a refusal is checked as one.</summary>
    /// <typeparam name="T">The read's type.</typeparam>
    /// <param name="dates">The dates.</param>
    /// <param name="f">The read.</param>
    /// <returns>The deferred reads.</returns>
    public static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) =>
        dates.Select(d => (Func<object?>)(() => f(d))).ToArray();
}
