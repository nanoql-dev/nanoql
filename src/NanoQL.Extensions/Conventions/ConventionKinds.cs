// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

// The value kinds whose names are unique among their records: the calendar kinds share one, the day counter kinds
// another; an alias joins its base's, so its name is unique among the value kinds an alias can name, units of measure
// among them. Each instrument convention kind and each other commodity kind has its own, which no alias joins.
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
        ScheduleConventionRecord => ConventionKind.ScheduleConvention,
        SwapConventionRecord => ConventionKind.SwapConvention,
        OisConventionRecord => ConventionKind.OisConvention,
        CdsConventionRecord => ConventionKind.CdsConvention,
        ZeroCouponInflationSwapConventionRecord => ConventionKind.ZeroCouponInflationSwapConvention,
        YearOnYearInflationSwapConventionRecord => ConventionKind.YearOnYearInflationSwapConvention,
        CrossCurrencyBasisSwapConventionRecord => ConventionKind.CrossCurrencyBasisSwapConvention,
        CrossCurrencySwapConventionRecord => ConventionKind.CrossCurrencySwapConvention,
        FxSwapConventionRecord => ConventionKind.FxSwapConvention,
        CapFloorConventionRecord => ConventionKind.CapFloorConvention,
        SwaptionConventionRecord => ConventionKind.SwaptionConvention,
        CmsConventionRecord => ConventionKind.CmsConvention,
        MultipleResetsSwapConventionRecord => ConventionKind.MultipleResetsSwapConvention,
        YoYInflationCapFloorConventionRecord => ConventionKind.YoYInflationCapFloorConvention,
        BondConventionRecord => ConventionKind.BondConvention,
        CommodityTypeRecord => ConventionKind.CommodityType,
        UnitOfMeasureRecord => ConventionKind.UnitOfMeasure,
        UnitOfMeasureConversionRecord => ConventionKind.UnitOfMeasureConversion,
        PaymentTermRecord => ConventionKind.PaymentTerm,
        CommodityIndexRecord => ConventionKind.CommodityIndex,
        CommoditySettingsRecord => ConventionKind.CommoditySettings,
        QuantitySettingsRecord => ConventionKind.QuantitySettings,
        AliasRecord => ConventionKind.Alias,
        _ => throw new ArgumentException($"unknown record kind {record.GetType().Name}", nameof(record)),
    };

    public static bool Clash(ConventionKind a, ConventionKind b) =>
        a == b || (a == ConventionKind.Alias && Aliased(b)) || (b == ConventionKind.Alias && Aliased(a));

    // The kinds an alias's name shares a name space with: the values an alias can name.
    public static bool Aliased(ConventionKind kind) =>
        kind <= ConventionKind.Index || kind == ConventionKind.UnitOfMeasure || kind >= ConventionKind.Alias;

    // The instrument convention kinds.
    public static bool IsInstrumentConvention(ConventionKind kind) =>
        kind is >= ConventionKind.ScheduleConvention and <= ConventionKind.BondConvention;

    // The commodity kinds.
    public static bool IsCommodity(ConventionKind kind) =>
        kind is >= ConventionKind.CommodityType and <= ConventionKind.QuantitySettings;

    // The element a record is written as.
    public static string Element(ConventionRecord record) => record.GetType().Name[..^"Record".Length];
}
