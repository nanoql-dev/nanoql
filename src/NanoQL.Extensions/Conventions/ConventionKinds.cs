// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

// The value kinds whose names are unique among their records: the calendar kinds share one, the day counter kinds
// another; an alias joins its base's, so its name is unique among all of them.
internal static class ConventionKinds
{
    public static ConventionKind Of(ConventionRecord record) => record switch
    {
        CalendarPresetRecord or CalendarRecord or JointCalendarRecord or BespokeCalendarRecord => ConventionKind.Calendar,
        DayCounterPresetRecord or DayCounterRecord => ConventionKind.DayCounter,
        CurrencyPresetRecord => ConventionKind.Currency,
        RegionRecord => ConventionKind.Region,
        IborIndexRecord or IborFamilyRecord or OvernightIndexRecord or SwapIndexRecord or OvernightIndexedSwapIndexRecord
            or ZeroInflationIndexRecord or YoYInflationIndexRecord => ConventionKind.Index,
        AliasRecord => ConventionKind.Alias,
        _ => throw new ArgumentException($"unknown record kind {record.GetType().Name}", nameof(record)),
    };

    public static bool Clash(ConventionKind a, ConventionKind b) =>
        a == b || a == ConventionKind.Alias || b == ConventionKind.Alias;

    // The element a record is written as.
    public static string Element(ConventionRecord record) => record.GetType().Name[..^"Record".Length];
}
