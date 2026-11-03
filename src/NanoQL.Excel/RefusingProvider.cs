// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Excel;

/// <summary>
/// The default conventions after a failed settings load: every lookup refuses with the load's error, so no result is
/// ever priced on QuantLib's conventions by accident. It overrides every overridable member of
/// <see cref="ConventionProvider"/>, which a test checks by reflection.
/// </summary>
/// <param name="error">The load's error.</param>
internal sealed class RefusingProvider(string error) : ConventionProvider
{
    /// <inheritdoc/>
    public override CalendarEdits ResolveCalendarEdits(string preset, CalendarEdits reference) => throw No();

    /// <inheritdoc/>
    public override CurrencyData ResolveCurrency(string code, Func<CurrencyData> reference) => throw No();

    /// <inheritdoc/>
    public override Calendar ResolveBusiness252Calendar(Func<Calendar> reference) => throw No();

    /// <inheritdoc/>
    public override IborIndexConventions ResolveIborIndex(string preset, Func<IborIndexConventions> reference) => throw No();

    /// <inheritdoc/>
    public override IborIndexConventions ResolveIborFamily(string family, Period tenor, Func<IborIndexConventions> reference) => throw No();

    /// <inheritdoc/>
    public override IborIndexConventions ResolveOvernightIndex(string preset, Func<IborIndexConventions> reference) => throw No();

    /// <inheritdoc/>
    public override SwapIndexConventions ResolveSwapIndex(string family, Period tenor, Func<SwapIndexConventions> reference) => throw No();

    /// <inheritdoc/>
    public override InflationIndexConventions ResolveInflationIndex(string preset, Func<InflationIndexConventions> reference) => throw No();

    /// <inheritdoc/>
    public override RegionData ResolveRegion(string preset, Func<RegionData> reference) => throw No();

    /// <inheritdoc/>
    public override UnitOfMeasureData ResolveUnitOfMeasure(string preset, Func<UnitOfMeasureData> reference) => throw No();

    /// <inheritdoc/>
    public override VanillaSwapDefaults ResolveVanillaSwapDefaults(Currency? currency, Func<VanillaSwapDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override OisDefaults ResolveOisDefaults(string index, Func<OisDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override CdsDefaults ResolveCdsDefaults(Func<CdsDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override CapFloorDefaults ResolveCapFloorDefaults(Func<CapFloorDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override SwaptionDefaults ResolveSwaptionDefaults(Func<SwaptionDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override CmsDefaults ResolveCmsDefaults(Func<CmsDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override MultipleResetsSwapDefaults ResolveMultipleResetsSwapDefaults(Func<MultipleResetsSwapDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override YoYInflationCapFloorDefaults ResolveYoYInflationCapFloorDefaults(Func<YoYInflationCapFloorDefaults> reference) => throw No();

    /// <inheritdoc/>
    public override BondConventions ResolveBond(string preset, Func<BondConventions> reference) => throw No();

    private XlSettingsException No() => new(error);
}
