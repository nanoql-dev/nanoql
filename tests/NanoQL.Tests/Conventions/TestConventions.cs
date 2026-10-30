// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A provider of the tests' own, deriving the extension point: it returns the edits, currency data and
/// <see cref="Business252"/> calendar it is given, the reference otherwise, and records each resolve it answers, so a
/// fact can tell which provider a construction read.
/// </summary>
public sealed class TestConventions : ConventionProvider
{
    private readonly ConcurrentQueue<string> resolved = new();

    /// <summary>Initializes a new instance of the <see cref="TestConventions"/> class.</summary>
    /// <param name="name">The name assertions show.</param>
    public TestConventions(string name)
    {
        Name = name;
    }

    /// <summary>Gets the name assertions show.</summary>
    public string Name { get; }

    /// <summary>Gets the holiday edits by calendar preset.</summary>
    public IReadOnlyDictionary<string, CalendarEdits> Edits { get; init; } = new Dictionary<string, CalendarEdits>();

    /// <summary>Gets the changes to a currency's QuantLib data, by code.</summary>
    public IReadOnlyDictionary<string, Func<CurrencyData, CurrencyData>> Currencies { get; init; } =
        new Dictionary<string, Func<CurrencyData, CurrencyData>>();

    /// <summary>Gets the calendar a <see cref="Business252"/> built without one takes, or null for QuantLib's.</summary>
    public Func<Calendar>? Business252Calendar { get; init; }

    /// <summary>Gets the changes to an Ibor, family or overnight preset's QuantLib conventions, by preset or family.</summary>
    public IReadOnlyDictionary<string, Func<IborIndexConventions, IborIndexConventions>> Indexes { get; init; } =
        new Dictionary<string, Func<IborIndexConventions, IborIndexConventions>>();

    /// <summary>Gets the changes to a region preset's QuantLib data, by preset.</summary>
    public IReadOnlyDictionary<string, Func<RegionData, RegionData>> Regions { get; init; } =
        new Dictionary<string, Func<RegionData, RegionData>>();

    /// <summary>Gets the changes to the swap builder's QuantLib defaults, by the index's currency code.</summary>
    public IReadOnlyDictionary<string, Func<VanillaSwapDefaults, VanillaSwapDefaults>> SwapDefaults { get; init; } =
        new Dictionary<string, Func<VanillaSwapDefaults, VanillaSwapDefaults>>();

    /// <summary>Gets the change to the CDS builder's QuantLib defaults, or null for none.</summary>
    public Func<CdsDefaults, CdsDefaults>? CdsDefaults { get; init; }

    /// <summary>Gets the changes to a bond preset's QuantLib conventions, by preset.</summary>
    public IReadOnlyDictionary<string, Func<BondConventions, BondConventions>> Bonds { get; init; } =
        new Dictionary<string, Func<BondConventions, BondConventions>>();

    /// <summary>Gets each resolve answered, in order: <c>calendar:TARGET</c>, <c>currency:EUR</c>, <c>business252</c>,
    /// <c>family:Euribor@6M</c>, <c>overnight:Sofr</c>, <c>swap:EuriborSwapIsdaFixA@10Y</c>.</summary>
    public IReadOnlyCollection<string> Resolved => resolved;

    /// <summary>A provider adding one holiday to one calendar preset.</summary>
    /// <param name="name">The provider's name.</param>
    /// <param name="preset">The calendar preset.</param>
    /// <param name="holiday">The holiday added.</param>
    /// <returns>The provider.</returns>
    public static TestConventions AddingHoliday(string name, string preset, Date holiday) =>
        new(name) { Edits = new Dictionary<string, CalendarEdits> { [preset] = new([holiday], []) } };

    /// <summary>
    /// A provider marking currencies, its name appended to each one's symbol, so a fact can tell which provider a
    /// currency was built under: the symbol is configuration, where the name and codes are the currency's identity.
    /// </summary>
    /// <param name="name">The provider's name, which the currencies' symbols carry.</param>
    /// <param name="codes">The currencies' codes.</param>
    /// <returns>The provider.</returns>
    public static TestConventions MarkingCurrencies(string name, params string[] codes) =>
        new(name)
        {
            Currencies = codes.ToDictionary(
                c => c, c => (Func<CurrencyData, CurrencyData>)(data => data with { Symbol = $"{data.Symbol}({name})" })),
        };

    /// <summary>The provider a currency of <see cref="MarkingCurrencies"/> was built under, by its symbol's mark.</summary>
    /// <param name="currency">The currency.</param>
    /// <returns>The provider's name, or <c>QuantLib</c> for an unmarked symbol.</returns>
    public static string MarkOf(Currency currency) =>
        currency.Symbol.EndsWith(')') ? currency.Symbol[(currency.Symbol.LastIndexOf('(') + 1)..^1] : "QuantLib";

    /// <summary>The provider an index was built with, read by reflection: the core keeps it internal.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The provider.</returns>
    public static ConventionProvider ProviderOf(FinancialIndex index) =>
        (ConventionProvider)typeof(FinancialIndex)
            .GetProperty("Provider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(index)!;

    /// <summary>Whether a resolve was answered: <c>calendar:TARGET</c>, <c>currency:EUR</c>, <c>business252</c>.</summary>
    /// <param name="key">The resolve.</param>
    /// <returns><see langword="true"/> when answered at least once.</returns>
    public bool HasResolved(string key) => resolved.Contains(key);

    /// <summary>Forgets the resolves answered so far.</summary>
    public void Clear() => resolved.Clear();

    /// <inheritdoc/>
    public override CalendarEdits ResolveCalendarEdits(string preset, CalendarEdits reference)
    {
        resolved.Enqueue("calendar:" + preset);
        return Edits.TryGetValue(preset, out CalendarEdits? edits) ? edits : reference;
    }

    /// <inheritdoc/>
    public override CurrencyData ResolveCurrency(string code, Func<CurrencyData> reference)
    {
        resolved.Enqueue("currency:" + code);
        CurrencyData data = reference();
        return Currencies.TryGetValue(code, out Func<CurrencyData, CurrencyData>? change) ? change(data) : data;
    }

    /// <inheritdoc/>
    public override Calendar ResolveBusiness252Calendar(Func<Calendar> reference)
    {
        resolved.Enqueue("business252");
        return Business252Calendar is null ? reference() : Business252Calendar();
    }

    /// <inheritdoc/>
    public override IborIndexConventions ResolveIborIndex(string preset, Func<IborIndexConventions> reference)
    {
        resolved.Enqueue("ibor:" + preset);
        return Change(Indexes, preset, reference());
    }

    /// <inheritdoc/>
    public override IborIndexConventions ResolveIborFamily(string family, Period tenor, Func<IborIndexConventions> reference)
    {
        resolved.Enqueue($"family:{family}@{tenor}");
        return Change(Indexes, family, reference());
    }

    /// <inheritdoc/>
    public override IborIndexConventions ResolveOvernightIndex(string preset, Func<IborIndexConventions> reference)
    {
        resolved.Enqueue("overnight:" + preset);
        return Change(Indexes, preset, reference());
    }

    /// <inheritdoc/>
    public override SwapIndexConventions ResolveSwapIndex(string family, Period tenor, Func<SwapIndexConventions> reference)
    {
        resolved.Enqueue($"swap:{family}@{tenor}");
        return reference();
    }

    /// <inheritdoc/>
    public override InflationIndexConventions ResolveInflationIndex(string preset, Func<InflationIndexConventions> reference)
    {
        resolved.Enqueue("inflation:" + preset);
        return reference();
    }

    /// <inheritdoc/>
    public override RegionData ResolveRegion(string preset, Func<RegionData> reference)
    {
        resolved.Enqueue("region:" + preset);
        return Change(Regions, preset, reference());
    }

    /// <inheritdoc/>
    public override VanillaSwapDefaults ResolveVanillaSwapDefaults(Currency? currency, Func<VanillaSwapDefaults> reference)
    {
        resolved.Enqueue("vanilla:" + currency?.Code);
        return Change(SwapDefaults, currency?.Code ?? "", reference());
    }

    /// <inheritdoc/>
    public override OisDefaults ResolveOisDefaults(string index, Func<OisDefaults> reference)
    {
        resolved.Enqueue("ois:" + index);
        return reference();
    }

    /// <inheritdoc/>
    public override CdsDefaults ResolveCdsDefaults(Func<CdsDefaults> reference)
    {
        resolved.Enqueue("cds");
        return CdsDefaults is { } change ? change(reference()) : reference();
    }

    /// <inheritdoc/>
    public override CapFloorDefaults ResolveCapFloorDefaults(Func<CapFloorDefaults> reference)
    {
        resolved.Enqueue("capfloor");
        return reference();
    }

    /// <inheritdoc/>
    public override SwaptionDefaults ResolveSwaptionDefaults(Func<SwaptionDefaults> reference)
    {
        resolved.Enqueue("swaption");
        return reference();
    }

    /// <inheritdoc/>
    public override CmsDefaults ResolveCmsDefaults(Func<CmsDefaults> reference)
    {
        resolved.Enqueue("cms");
        return reference();
    }

    /// <inheritdoc/>
    public override MultipleResetsSwapDefaults ResolveMultipleResetsSwapDefaults(Func<MultipleResetsSwapDefaults> reference)
    {
        resolved.Enqueue("multipleresets");
        return reference();
    }

    /// <inheritdoc/>
    public override YoYInflationCapFloorDefaults ResolveYoYInflationCapFloorDefaults(Func<YoYInflationCapFloorDefaults> reference)
    {
        resolved.Enqueue("yoycapfloor");
        return reference();
    }

    /// <inheritdoc/>
    public override BondConventions ResolveBond(string preset, Func<BondConventions> reference)
    {
        resolved.Enqueue("bond:" + preset);
        return Change(Bonds, preset, reference());
    }

    /// <inheritdoc/>
    public override string ToString() => Name;

    private static T Change<T>(IReadOnlyDictionary<string, Func<T, T>> changes, string name, T data) =>
        changes.TryGetValue(name, out Func<T, T>? change) ? change(data) : data;
}
