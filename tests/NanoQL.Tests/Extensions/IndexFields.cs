// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>An index's fields and behaviour as text, to compare a resolved index with its coded preset.</summary>
internal static class IndexFields
{
    /// <summary>An interest-rate index's class and fields.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The text.</returns>
    public static string Of(InterestRateIndex i) =>
        $"{i.GetType().Name} | {i.Name} | {i.FamilyName} | {i.Tenor} | {i.FixingDays} | {i.Currency?.Code} | {i.FixingCalendar.Name} | {i.DayCounter.Name}"
        + (i is IborIndex ibor ? $" | {ibor.BusinessDayConvention} | {ibor.EndOfMonth}" : "")
        + (i is Libor libor ? $" | {libor.JointCalendar.Name}" : "")
        + (i is SwapIndex swap ? $" | {swap.FixedLegTenor} | {swap.FixedLegConvention} | [{Of(swap.IborIndex)}]" : "");

    /// <summary>An index's fixing, value and maturity dates for every fixing date of a year.</summary>
    /// <param name="i">The index.</param>
    /// <param name="year">The year.</param>
    /// <returns>Each date's three dates.</returns>
    public static string[] Dates(InterestRateIndex i, int year)
    {
        var out_ = new List<string>();
        for (Date d = new(1, Month.January, year); d.Year == year; ++d)
        {
            if (i.IsValidFixingDate(d))
            {
                Date value = i.ValueDate(d);
                out_.Add($"{d.SerialNumber} {value.SerialNumber} {i.MaturityDate(value).SerialNumber} {i.FixingDate(value).SerialNumber}");
            }
        }

        return [.. out_];
    }

    /// <summary>An overnight index's growth factor and simple accrual on a grid of fixings and spans, bit for bit.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The values' bits.</returns>
    public static string[] Accruals(OvernightIndex i) =>
        [.. from f in new[] { -0.01, 0.0, 0.02, 0.1 }
            from s in new[] { 1 / 360.0, 0.25, 1.0, 5.0 }
            select string.Create(CultureInfo.InvariantCulture,
                $"{BitConverter.DoubleToInt64Bits(i.GrowthFactor(f, s))} {BitConverter.DoubleToInt64Bits(i.SimpleAccrual(f, s))}")];
}
