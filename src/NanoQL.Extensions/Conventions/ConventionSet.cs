// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An immutable set of convention records, layered snapshot on snapshot, and the <see cref="ConventionProvider"/>
/// that answers from them: QuantLib's shipped presets (<see cref="QuantLib"/>) or none (<see cref="Empty"/>), with a
/// user's files on top.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="QuantLib"/> is the shipped records; <see cref="ConventionProvider.Default"/>,
/// <see cref="ConventionProvider.Current"/> and <see cref="ConventionProvider.Use"/> are the provider's, so a set
/// configures the system as <c>ConventionProvider.Default = ConventionSet.QuantLib.With(...)</c>.
/// </para>
/// <para>
/// A lookup builds a new object each time, inside the set's scope, so a preset comes out as its own class with the
/// set's values, and an index keeps the set as its provider. A field comes from its record, then the record's base
/// chain, then QuantLib's reference. Calendars, day counters and currencies are code kinds: a name the set doesn't
/// define, but a coded preset has, resolves to QuantLib's preset, with no edits, even on <see cref="Empty"/>.
/// </para>
/// <para>
/// A record named after a coded preset must be one the preset's class can take, since a coded construction can't
/// become another kind: a calendar preset takes its own preset record or a calendar record based on itself, a day
/// counter or currency preset a record of its own kind based on itself, and an alias between two presets only where
/// QuantLib shares their implementation. <see cref="With"/> refuses any other, and resolves every record of the new
/// set, so a name a later layer breaks, a cycle or a value the core refuses fails when the file is loaded.
/// </para>
/// <para>
/// Two sets are equal when they hold equal snapshots in the same order on the same base.
/// </para>
/// </remarks>
public sealed class ConventionSet : ConventionProvider, IEquatable<ConventionSet>
{
    private static readonly Lazy<ConventionSet> Shipped = new(
        () => Validated(new ConventionSet(true, [ConventionsXml.ReadPresets()]), 0));

    // The currencies whose data this thread is building, within one synchronous construction.
    [ThreadStatic]
    private static HashSet<string>? resolvingCurrencies;

    private readonly bool onQuantLib;
    private readonly ImmutableArray<ConventionSnapshot> layers;

    // Each name's records, topmost layer first, by value kind; an alias under its own kind.
    private readonly Dictionary<(ConventionKind Kind, string Name), List<Entry>> index = [];
    private readonly int hash;

    private ConventionSet(bool onQuantLib, ImmutableArray<ConventionSnapshot> layers)
    {
        this.onQuantLib = onQuantLib;
        this.layers = layers;
        var h = default(HashCode);
        h.Add(onQuantLib);
        for (int layer = 0; layer < layers.Length; ++layer)
        {
            h.Add(layers[layer]);
            ConventionSnapshot s = layers[layer];
            for (int i = 0; i < s.Records.Length; ++i)
            {
                ConventionRecord r = s.Records[i];
                var key = (ConventionKinds.Of(r), r.Name);
                if (!index.TryGetValue(key, out List<Entry>? entries))
                {
                    index[key] = entries = [];
                }

                entries.Insert(0, new Entry(layer, r, s.Sources[i]));
            }
        }

        hash = h.ToHashCode();
    }

    /// <summary>
    /// Gets QuantLib's shipped presets: every calendar, day counter and currency QuantLib codes, with QuantLib's
    /// values, and FpML's exact codes as aliases. It hides the provider's <see cref="ConventionProvider.QuantLib"/>,
    /// QuantLib's coded values, which give the same presets.
    /// </summary>
    public static new ConventionSet QuantLib => Shipped.Value;

    /// <summary>
    /// Gets a set without QuantLib's records, for files that define every convention themselves: calendars, day
    /// counters and currencies still resolve to QuantLib's coded presets, which are code.
    /// </summary>
    public static ConventionSet Empty { get; } = new(false, []);

    /// <summary>Gets a value indicating whether the set is built on QuantLib's shipped presets.</summary>
    public bool IsOnQuantLib => onQuantLib;

    /// <summary>Gets the snapshots the set layers, bottom first: QuantLib's shipped one first on
    /// <see cref="QuantLib"/>.</summary>
    public ImmutableArray<ConventionSnapshot> Layers => layers;

    /// <summary>
    /// A set with a snapshot layered on top: its records override the set's of the same names, and the rest stay.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The new set; the receiver and <see cref="ConventionProvider.Default"/> are unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A record names an unknown record, takes a coded preset's name in a kind its class can't take, edits a preset's
    /// QuantLib name, defines a currency QuantLib doesn't have, lacks a field it can't take from a base, or is part
    /// of a cycle; or the core refuses a record's value.
    /// </exception>
    public ConventionSet With(ConventionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Validated(new ConventionSet(onQuantLib, layers.Add(snapshot)), layers.Length);
    }

    /// <summary>A calendar by name, built in the set: a preset as its own class with the set's edits.</summary>
    /// <param name="name">The calendar's name: a preset's, a record's or an alias.</param>
    /// <returns>A new calendar.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No calendar has the name.</exception>
    public Calendar Calendar(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return CalendarOf(name, layers.Length, [], nameof(name));
        }
    }

    /// <summary>A day counter by name, built in the set.</summary>
    /// <param name="name">The day counter's name: a preset's, a record's or an alias.</param>
    /// <returns>A new day counter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No day counter has the name.</exception>
    public DayCounter DayCounter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return DayCounterOf(name, layers.Length, [], nameof(name));
        }
    }

    /// <summary>A currency by its code, built in the set: the preset's class with the set's data.</summary>
    /// <param name="code">The currency's ISO 4217 code, or an alias.</param>
    /// <returns>A new currency.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    /// <exception cref="ArgumentException">No currency has the code.</exception>
    public Currency Currency(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        using (Use(this))
        {
            return CurrencyOf(code, [], nameof(code));
        }
    }

    /// <summary>A region by name, built in the set: a preset as its own class, or a region of a user's own.</summary>
    /// <param name="name">The region's record name (<c>EURegion</c>) or an alias.</param>
    /// <returns>A new region.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No region has the name.</exception>
    public Region Region(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return RegionOf(name, [], nameof(name));
        }
    }

    /// <summary>
    /// An Ibor index by name, built in the set on its forwarding curve: a preset as its own class, an overnight index
    /// among them, or an index of a user's own by its style.
    /// </summary>
    /// <param name="name">The index's name (<c>Euribor6M</c>, <c>Sofr</c>) or an alias.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index, keeping the set as its provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No Ibor index has the name, the name is a family's, which takes a tenor, or
    /// the preset takes its settlement days as an argument (a record based on it gives them).</exception>
    /// <remarks>
    /// Build an index on its curve here, as a preset's own constructor does, rather than clone one: QuantLib's clones
    /// are its base classes', so a clone of <see cref="Sonia"/> is an <see cref="Indexes.OvernightIndex"/>, which a
    /// builder defaulting by class (<see cref="Instruments.MakeOIS"/>'s settlement days) reads as another index.
    /// </remarks>
    public IborIndex IborIndex(string name, YieldTermStructure? h = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return IborIndexOf(name, [], nameof(name), h);
        }
    }

    /// <summary>An Ibor family's index at a tenor, built in the set on its forwarding curve.</summary>
    /// <param name="name">The family's name (<c>Euribor</c>, <c>USDLibor</c>) or an alias (<c>EUR-EURIBOR</c>).</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index; at a tenor a fixed-tenor preset of the family has, that preset's values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No family has the name, or it takes no tenor in the tenor's unit.</exception>
    public IborIndex IborIndex(string name, Period tenor, YieldTermStructure? h = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return FamilyIndexOf(name, tenor, [], nameof(name), h);
        }
    }

    /// <summary>An overnight index by name, built in the set on its forwarding curve.</summary>
    /// <param name="name">The index's name (<c>Sofr</c>) or an alias (<c>USD-SOFR</c>).</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index, of its preset's class.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No overnight index has the name.</exception>
    public OvernightIndex OvernightIndex(string name, YieldTermStructure? h = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return IborIndexOf(name, [], nameof(name), h) as OvernightIndex
                ?? throw new ArgumentException($"the index {name} is no overnight index", nameof(name));
        }
    }

    /// <summary>
    /// A swap index family's index at a tenor, built in the set with its Ibor index on a forwarding curve, which also
    /// discounts, as QuantLib's one-curve constructor builds it.
    /// </summary>
    /// <param name="name">The family's name (<c>EuriborSwapIsdaFixA</c>) or an alias.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index, of its preset's class.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No swap index family has the name.</exception>
    public SwapIndex SwapIndex(string name, Period tenor, YieldTermStructure? forwarding = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return SwapIndexOf(name, tenor, [], nameof(name), forwarding, false, null);
        }
    }

    /// <summary>
    /// A swap index family's index at a tenor, built in the set with its Ibor index on a forwarding curve and an
    /// exogenous discount curve, as QuantLib's two-curve constructor builds it.
    /// </summary>
    /// <param name="name">The family's name (<c>EuriborSwapIsdaFixA</c>) or an alias.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discount curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index, of its preset's class.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No swap index family has the name.</exception>
    public SwapIndex SwapIndex(string name, Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return SwapIndexOf(name, tenor, [], nameof(name), forwarding, true, discounting);
        }
    }

    /// <summary>
    /// An overnight-indexed swap index at a tenor, built in the set with its overnight index on a forwarding curve.
    /// </summary>
    /// <param name="name">The index's record name.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The overnight index's forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No overnight-indexed swap index has the name.</exception>
    public OvernightIndexedSwapIndex OvernightIndexedSwapIndex(string name, Period tenor, YieldTermStructure? h = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return OisIndexOf(name, tenor, [], nameof(name), h);
        }
    }

    /// <summary>A zero inflation index by name, built in the set on its curve.</summary>
    /// <param name="name">The index's name (<c>EUHICP</c>, a record based on <c>AUCPI</c> giving its arguments) or an
    /// alias.</param>
    /// <param name="ts">The zero inflation curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index, of its preset's class.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No zero inflation index has the name, or the preset takes arguments a record
    /// based on it gives.</exception>
    public ZeroInflationIndex ZeroInflationIndex(string name, ZeroInflationTermStructure? ts = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return ZeroIndexOf(name, [], nameof(name), ts);
        }
    }

    /// <summary>A year-on-year inflation index by name, built in the set on its curve.</summary>
    /// <param name="name">The index's name (<c>YYEUHICP</c>) or an alias.</param>
    /// <param name="ts">The year-on-year inflation curve, or <see langword="null"/> for none.</param>
    /// <returns>A new index, quoted or the ratio of its underlying zero index's, which is built without a
    /// curve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No year-on-year inflation index has the name.</exception>
    public YoYInflationIndex YoYInflationIndex(string name, YoYInflationTermStructure? ts = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return YoYIndexOf(name, [], nameof(name), ts);
        }
    }

    /// <summary>A schedule convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No ScheduleConvention record has the name.</exception>
    public ScheduleConvention Schedule(string name) =>
        new(this, Merged<ScheduleConventionRecord>(ConventionKind.ScheduleConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No SwapConvention record has the name.</exception>
    public SwapConvention Swap(string name) =>
        new(this, Merged<SwapConventionRecord>(ConventionKind.SwapConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>An overnight-indexed swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No OisConvention record has the name.</exception>
    public OisConvention Ois(string name) =>
        new(this, Merged<OisConventionRecord>(ConventionKind.OisConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A credit default swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No CdsConvention record has the name.</exception>
    public CdsConvention Cds(string name) =>
        new(this, Merged<CdsConventionRecord>(ConventionKind.CdsConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A zero-coupon inflation swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No ZeroCouponInflationSwapConvention record has the name.</exception>
    public ZeroCouponInflationSwapConvention ZeroCouponInflationSwap(string name) =>
        new(this, Merged<ZeroCouponInflationSwapConventionRecord>(ConventionKind.ZeroCouponInflationSwapConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A year-on-year inflation swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No YearOnYearInflationSwapConvention record has the name.</exception>
    public YearOnYearInflationSwapConvention YearOnYearInflationSwap(string name) =>
        new(this, Merged<YearOnYearInflationSwapConventionRecord>(ConventionKind.YearOnYearInflationSwapConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A cross-currency basis swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No CrossCurrencyBasisSwapConvention record has the name.</exception>
    [Experimental("NQL0001")]
    public CrossCurrencyBasisSwapConvention CrossCurrencyBasisSwap(string name) =>
        new(this, Merged<CrossCurrencyBasisSwapConventionRecord>(ConventionKind.CrossCurrencyBasisSwapConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A cross-currency fixed-against-floating swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No CrossCurrencySwapConvention record has the name.</exception>
    [Experimental("NQL0001")]
    public CrossCurrencySwapConvention CrossCurrencySwap(string name) =>
        new(this, Merged<CrossCurrencySwapConventionRecord>(ConventionKind.CrossCurrencySwapConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>An FX swap and forward convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No FxSwapConvention record has the name.</exception>
    public FxSwapConvention FxSwap(string name) =>
        new(this, Merged<FxSwapConventionRecord>(ConventionKind.FxSwapConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A cap and floor convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No CapFloorConvention record has the name.</exception>
    public CapFloorConvention CapFloor(string name) =>
        new(this, Merged<CapFloorConventionRecord>(ConventionKind.CapFloorConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A swaption convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No SwaptionConvention record has the name.</exception>
    public SwaptionConvention Swaption(string name) =>
        new(this, Merged<SwaptionConventionRecord>(ConventionKind.SwaptionConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A CMS swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No CmsConvention record has the name.</exception>
    public CmsConvention Cms(string name) =>
        new(this, Merged<CmsConventionRecord>(ConventionKind.CmsConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A multiple-resets swap convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No MultipleResetsSwapConvention record has the name.</exception>
    public MultipleResetsSwapConvention MultipleResetsSwap(string name) =>
        new(this, Merged<MultipleResetsSwapConventionRecord>(ConventionKind.MultipleResetsSwapConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A year-on-year inflation cap and floor convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No YoYInflationCapFloorConvention record has the name.</exception>
    public YoYInflationCapFloorConvention YoYInflationCapFloor(string name) =>
        new(this, Merged<YoYInflationCapFloorConventionRecord>(ConventionKind.YoYInflationCapFloorConvention, name, static (a, b) => a.Over(b)).Named(name));

    /// <summary>A government bond market's convention by name, its record's base chain merged.</summary>
    /// <param name="name">The record's name.</param>
    /// <returns>The convention, on the set's names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No BondConvention record has the name.</exception>
    public BondConvention Bond(string name) =>
        new(this, Merged<BondConventionRecord>(ConventionKind.BondConvention, name, static (a, b) => a.Over(b)).Named(name), FixedRateBondDefaults(), BondPresetOf(name));

    /// <summary>
    /// Where each name the set defines comes from, QuantLib's presets or a file's record, and a warning for each
    /// record that overrides no name and that no record references, as a likely misspelling.
    /// </summary>
    /// <returns>The report.</returns>
    public ConventionProvenance Provenance()
    {
        var origins = new List<ConventionOrigin>();
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConventionSnapshot s in layers)
        {
            foreach (ConventionRecord r in s.Records)
            {
                foreach (string name in References(r))
                {
                    referenced.Add(name);
                }
            }
        }

        var warnings = new List<string>();
        foreach (((ConventionKind kind, string name), List<Entry> entries) in index.OrderBy(e => e.Key.Kind).ThenBy(e => e.Key.Name, StringComparer.Ordinal))
        {
            Entry top = entries[0];
            bool shipped = onQuantLib && top.Layer == 0;
            origins.Add(new ConventionOrigin(kind, name, ConventionKinds.Element(top.Record), shipped ? null : top.Source));
            bool overrides = entries.Count > 1 || (onQuantLib && top.Layer > 0 && IsShippedName(name)) || IsCodedPreset(name);
            if (!shipped && !overrides && !referenced.Contains(name) && top.Record is not AliasRecord)
            {
                warnings.Add($"{ConventionKinds.Element(top.Record)} {name}{Where(top.Source)} overrides no name and nothing references it");
            }
        }

        return new ConventionProvenance(origins, warnings);
    }

    /// <inheritdoc/>
    public override CalendarEdits ResolveCalendarEdits(string preset, CalendarEdits reference)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return EditsOf(preset, layers.Length) ?? reference;
    }

    /// <inheritdoc/>
    public override CurrencyData ResolveCurrency(string code, Func<CurrencyData> reference)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(reference);
        if (MergedCurrency(code, layers.Length) is not { } r)
        {
            return reference();
        }

        // a triangulation currency is built through this callback again, so a cycle of them is caught here
        HashSet<string> resolving = resolvingCurrencies ??= new(StringComparer.Ordinal);
        if (!resolving.Add(code))
        {
            throw new ArgumentException($"the currency {code} is part of a cycle of triangulation currencies", "snapshot");
        }

        try
        {
            return new CurrencyData(
                r.QuantLibName!,
                code,
                r.NumericCode!.Value,
                r.Symbol!,
                r.FractionSymbol!,
                r.FractionsPerUnit!.Value,
                r.Rounding!.ToRounding(),
                r.TriangulationCurrency is { Length: > 0 } tri ? CurrencyOf(tri, [], "code") : null);
        }
        finally
        {
            resolving.Remove(code);
        }
    }

    /// <inheritdoc/>
    public override Calendar ResolveBusiness252Calendar(Func<Calendar> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return Business252CalendarOf(layers.Length) is string calendar
            ? CalendarOf(calendar, layers.Length, [], "calendar")
            : reference();
    }

    /// <inheritdoc/>
    public override IborIndexConventions ResolveIborIndex(string preset, Func<IborIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(reference);
        return IborFieldsOf(preset, layers.Length, []) is { } fields
            ? IborConventions(fields, reference, preset)
            : Undefined(preset, reference);
    }

    /// <inheritdoc/>
    public override IborIndexConventions ResolveIborFamily(string family, Period tenor, Func<IborIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(reference);

        // a fixed-tenor preset of the family at the tenor, where the set has its record, else the family at the tenor
        IborFields? fields = FixedPresetOf(family, tenor) is string fixedPreset && Top(ConventionKind.Index, fixedPreset, layers.Length) is not null
            ? IborFieldsOf(fixedPreset, layers.Length, [])
            : FamilyFieldsAt(family, tenor, layers.Length, []);
        return fields is not null ? IborConventions(fields, reference, $"{family} at {tenor}") : Undefined(family, reference);
    }

    /// <inheritdoc/>
    public override IborIndexConventions ResolveOvernightIndex(string preset, Func<IborIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(reference);
        return IborFieldsOf(preset, layers.Length, []) is { } fields
            ? IborConventions(fields, reference, preset)
            : Undefined(preset, reference);
    }

    /// <inheritdoc/>
    public override SwapIndexConventions ResolveSwapIndex(string family, Period tenor, Func<SwapIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(reference);
        if (SwapFieldsOf(family, layers.Length, []) is not { } s)
        {
            return Undefined(family, reference);
        }

        SwapIndexConventions? q = null;
        SwapIndexConventions Q() => q ??= reference();

        // QuantLib's families take their short variant up to and including a year
        bool isShort = !(tenor > new Period(1, TimeUnit.Years));
        (string? ibor, Period? iborTenor) = isShort && s.ShortIborIndex is not null
            ? (s.ShortIborIndex, s.ShortIborIndexTenor)
            : (s.IborIndex, s.IborIndexTenor);
        // QuantLib's reference builds its Ibor index in the set, on the curve: an index the record names that equals it
        // is it, and any other is built on the curve as its own class
        IborIndex iborIndex = Q().IborIndex;
        if (ibor is not null)
        {
            YieldTermStructure? curve = iborIndex.ForwardingTermStructure;
            IborIndex named = iborTenor is Period t ? FamilyIndexOf(ibor, t, [], "family", curve) : IborIndexOf(ibor, [], "family", curve);
            if (!SameIbor(named, iborIndex))
            {
                iborIndex = named;
            }
        }
        return new SwapIndexConventions(
            s.SettlementDays ?? Q().SettlementDays,
            s.Currency is string c ? CurrencyOf(c, [], "family") : Q().Currency,
            s.FixingCalendar is string cal ? CalendarOf(cal, layers.Length, [], "family") : Q().FixingCalendar,
            (isShort ? s.ShortFixedLegTenor : null) ?? s.FixedLegTenor ?? Q().FixedLegTenor,
            s.FixedLegConvention ?? Q().FixedLegConvention,
            s.FixedLegDayCounter is string dc ? DayCounterOf(dc, layers.Length, [], "family") : Q().FixedLegDayCounter,
            iborIndex);
    }

    /// <inheritdoc/>
    public override InflationIndexConventions ResolveInflationIndex(string preset, Func<InflationIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(reference);
        if (InflationFieldsOf(preset, layers.Length, []) is not { } z)
        {
            return Undefined(preset, reference);
        }

        InflationIndexConventions? q = null;
        InflationIndexConventions Q() => q ??= reference();
        return new InflationIndexConventions(
            z.Region is string region ? RegionOf(region, [], "preset") : Q().Region,
            z.Revised ?? Q().Revised,
            z.Frequency ?? Q().Frequency,
            z.AvailabilityLag ?? Q().AvailabilityLag,
            z.Currency is string c ? CurrencyOf(c, [], "preset") : Q().Currency);
    }

    /// <inheritdoc/>
    public override RegionData ResolveRegion(string preset, Func<RegionData> reference)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(reference);
        if (RegionFieldsOf(preset, layers.Length, []) is not { } r)
        {
            return Undefined(preset, reference);
        }

        RegionData? q = null;
        RegionData Q() => q ??= reference();
        return new RegionData(r.RegionName ?? Q().Name, r.Code ?? Q().Code);
    }

    /// <inheritdoc/>
    public override VanillaSwapDefaults ResolveVanillaSwapDefaults(Currency? currency, Func<VanillaSwapDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string name = SwapDefaults + currency?.Code;
        if (currency is null || DefaultRecord<SwapConventionRecord>(ConventionKind.SwapConvention, name, static (a, b) => a.Over(b)) is not { } r)
        {
            return Undefined(name, reference);
        }

        VanillaSwapDefaults? q = null;
        VanillaSwapDefaults Q() => q ??= reference();

        // the threshold comes with the tenor from the record that gives the tenor, or alone from one that gives none
        return new VanillaSwapDefaults(
            r.FixedLegTenor is null ? Q().FixedLegTenor : r.FixedLegTenor,
            r.FixedLegTenor is null ? r.FixedLegTenorThreshold ?? Q().FixedLegTenorThreshold : r.FixedLegTenorThreshold,
            r.FixedLegDayCounter is string dc ? DayCounterOf(dc, layers.Length, [], "currency") : Q().FixedLegDayCounter,
            r.FixedLegConvention ?? Q().FixedLegConvention,
            r.FixedLegTerminationDateConvention ?? Q().FixedLegTerminationDateConvention,
            r.FixedLegRule ?? Q().FixedLegRule,
            r.FixedLegEndOfMonth ?? Q().FixedLegEndOfMonth,
            r.FloatingLegRule ?? Q().FloatingLegRule,
            r.FloatingLegEndOfMonth ?? Q().FloatingLegEndOfMonth);
    }

    /// <inheritdoc/>
    public override OisDefaults ResolveOisDefaults(string index, Func<OisDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(reference);

        // the index's class's record, else the builder's
        if ((DefaultRecord<OisConventionRecord>(ConventionKind.OisConvention, OisDefaultsName + "." + index, static (a, b) => a.Over(b))
            ?? DefaultRecord<OisConventionRecord>(ConventionKind.OisConvention, OisDefaultsName, static (a, b) => a.Over(b))) is not { } r)
        {
            return Undefined(OisDefaultsName, reference);
        }

        OisDefaults? q = null;
        OisDefaults Q() => q ??= reference();

        // the payment frequency is both legs', the fixed leg's unless its own is given, as the builder's setters
        return new OisDefaults(
            r.SettlementDays ?? Q().SettlementDays,
            r.FixedLegFrequency ?? r.PaymentFrequency ?? Q().FixedLegPaymentFrequency,
            r.PaymentFrequency ?? Q().OvernightLegPaymentFrequency,
            r.PaymentConvention ?? Q().PaymentAdjustment,
            r.PaymentLag ?? Q().PaymentLag,
            r.FixedLegConvention ?? Q().FixedLegConvention,
            r.FixedLegTerminationDateConvention ?? Q().FixedLegTerminationDateConvention,
            r.OvernightLegConvention ?? Q().OvernightLegConvention,
            r.OvernightLegTerminationDateConvention ?? Q().OvernightLegTerminationDateConvention,
            r.FixedLegRule ?? Q().FixedLegRule,
            r.OvernightLegRule ?? Q().OvernightLegRule,
            r.TelescopicValueDates ?? Q().TelescopicValueDates,
            r.Averaging ?? Q().AveragingMethod,
            r.LockoutDays ?? Q().LockoutDays,
            r.ObservationShift ?? Q().ApplyObservationShift);
    }

    /// <inheritdoc/>
    public override CdsDefaults ResolveCdsDefaults(Func<CdsDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (DefaultRecord<CdsConventionRecord>(ConventionKind.CdsConvention, nameof(MakeCreditDefaultSwap), static (a, b) => a.Over(b)) is not { } r)
        {
            return Undefined(nameof(MakeCreditDefaultSwap), reference);
        }

        CdsDefaults? q = null;
        CdsDefaults Q() => q ??= reference();
        return new CdsDefaults(
            r.CouponFrequency is Frequency f ? new Period(f) : Q().CouponTenor,
            r.Rule ?? Q().Rule,
            r.PaymentConvention ?? Q().Convention,
            r.DayCounter is string dc ? DayCounterOf(dc, layers.Length, [], "reference") : Q().DayCounter,
            r.LastPeriodDayCounter is string last ? DayCounterOf(last, layers.Length, [], "reference") : Q().LastPeriodDayCounter,
            r.SettlesAccrual ?? Q().SettlesAccrual,
            r.PaysAtDefaultTime ?? Q().PaysAtDefaultTime,
            r.RebatesAccrual ?? Q().RebatesAccrual,
            r.CashSettlementDays ?? Q().CashSettlementDays,
            r.Calendar is string calendar ? CalendarOf(calendar, layers.Length, [], "reference") : Q().Calendar);
    }

    /// <inheritdoc/>
    public override CapFloorDefaults ResolveCapFloorDefaults(Func<CapFloorDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return DefaultRecord<CapFloorConventionRecord>(ConventionKind.CapFloorConvention, nameof(MakeCapFloor), static (a, b) => a.Over(b)) is { } r
            ? new CapFloorDefaults(r.FirstCapletExcluded ?? reference().FirstCapletExcluded)
            : Undefined(nameof(MakeCapFloor), reference);
    }

    /// <inheritdoc/>
    public override SwaptionDefaults ResolveSwaptionDefaults(Func<SwaptionDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (DefaultRecord<SwaptionConventionRecord>(ConventionKind.SwaptionConvention, nameof(MakeSwaption), static (a, b) => a.Over(b)) is not { } r)
        {
            return Undefined(nameof(MakeSwaption), reference);
        }

        SwaptionDefaults? q = null;
        SwaptionDefaults Q() => q ??= reference();
        return new SwaptionDefaults(r.SettlementType ?? Q().SettlementType, r.SettlementMethod ?? Q().SettlementMethod, r.OptionConvention ?? Q().OptionConvention);
    }

    /// <inheritdoc/>
    public override CmsDefaults ResolveCmsDefaults(Func<CmsDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (DefaultRecord<CmsConventionRecord>(ConventionKind.CmsConvention, nameof(MakeCms), static (a, b) => a.Over(b)) is not { } r)
        {
            return Undefined(nameof(MakeCms), reference);
        }

        CmsDefaults? q = null;
        CmsDefaults Q() => q ??= reference();
        return new CmsDefaults(
            r.CmsLegTenor ?? Q().CmsLegTenor,
            r.CmsLegConvention ?? Q().CmsLegConvention,
            r.CmsLegTerminationDateConvention ?? Q().CmsLegTerminationDateConvention,
            r.CmsLegRule ?? Q().CmsLegRule,
            r.CmsLegEndOfMonth ?? Q().CmsLegEndOfMonth,
            r.CmsLegDayCounter is string dc ? DayCounterOf(dc, layers.Length, [], "reference") : Q().CmsLegDayCounter,
            r.FloatingLegRule ?? Q().FloatingLegRule,
            r.FloatingLegEndOfMonth ?? Q().FloatingLegEndOfMonth);
    }

    /// <inheritdoc/>
    public override MultipleResetsSwapDefaults ResolveMultipleResetsSwapDefaults(Func<MultipleResetsSwapDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (DefaultRecord<MultipleResetsSwapConventionRecord>(ConventionKind.MultipleResetsSwapConvention, nameof(MakeMultipleResetsSwap), static (a, b) => a.Over(b)) is not { } r)
        {
            return Undefined(nameof(MakeMultipleResetsSwap), reference);
        }

        MultipleResetsSwapDefaults? q = null;
        MultipleResetsSwapDefaults Q() => q ??= reference();
        return new MultipleResetsSwapDefaults(r.FixedLegConvention ?? Q().FixedLegConvention, r.Averaging ?? Q().AveragingMethod);
    }

    /// <inheritdoc/>
    public override YoYInflationCapFloorDefaults ResolveYoYInflationCapFloorDefaults(Func<YoYInflationCapFloorDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (DefaultRecord<YoYInflationCapFloorConventionRecord>(ConventionKind.YoYInflationCapFloorConvention, nameof(MakeYoYInflationCapFloor), static (a, b) => a.Over(b)) is not { } r)
        {
            return Undefined(nameof(MakeYoYInflationCapFloor), reference);
        }

        YoYInflationCapFloorDefaults? q = null;
        YoYInflationCapFloorDefaults Q() => q ??= reference();
        return new YoYInflationCapFloorDefaults(
            r.PaymentDayCounter is string dc ? DayCounterOf(dc, layers.Length, [], "reference") : Q().PaymentDayCounter,
            r.PaymentAdjustment ?? Q().PaymentAdjustment,
            r.FixingDays ?? Q().FixingDays);
    }

    /// <inheritdoc/>
    public override BondConventions ResolveBond(string preset, Func<BondConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(reference);
        if (DefaultRecord<BondConventionRecord>(ConventionKind.BondConvention, preset, static (a, b) => a.Over(b)) is not { } r)
        {
            return Undefined(preset, reference);
        }

        BondConventions? q = null;
        BondConventions Q() => q ??= reference();
        return new BondConventions(
            r.SettlementDays ?? Q().SettlementDays,
            r.Frequency is Frequency f ? new Period(f) : Q().Tenor,
            r.Calendar is string calendar ? CalendarOf(calendar, layers.Length, [], "preset") : Q().Calendar,
            r.Convention ?? Q().Convention,
            r.TerminationDateConvention ?? Q().TerminationDateConvention,
            r.Rule ?? Q().Rule,
            r.EndOfMonth ?? Q().EndOfMonth,
            r.AccrualDayCounter is string dc ? DayCounterOf(dc, layers.Length, [], "preset") : Q().AccrualDayCounter,
            r.PaymentConvention ?? Q().PaymentConvention,
            r.PaymentCalendar is string payment ? CalendarOf(payment, layers.Length, [], "preset") : Q().PaymentCalendar,
            r.ExCouponPeriod ?? Q().ExCouponPeriod,
            r.ExCouponCalendar is string exCoupon ? CalendarOf(exCoupon, layers.Length, [], "preset") : Q().ExCouponCalendar,
            r.ExCouponConvention ?? Q().ExCouponConvention,
            r.ExCouponEndOfMonth ?? Q().ExCouponEndOfMonth);
    }

    /// <inheritdoc/>
    public bool Equals(ConventionSet? other) =>
        other is not null && (ReferenceEquals(this, other)
            || (hash == other.hash && onQuantLib == other.onQuantLib && layers.SequenceEqual(other.layers)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ConventionSet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    // The shipped presets' names, a coded preset's or an FpML code's: names a user's file may reuse though they hold '/'.
    private static bool IsShippedName(string name) =>
        Shipped.Value.index.Keys.Any(k => k.Name == name);

    // A name ConventionRecords.Describe generates for a part, name/n, beside the record of its name in one snapshot.
    private static bool IsGenerated(string name, ConventionSnapshot snapshot)
    {
        int slash = name.LastIndexOf('/');
        string root = name[..slash], n = name[(slash + 1)..];
        return slash > 0 && n.Length > 0 && n[0] != '0' && n.All(char.IsAsciiDigit)
            && snapshot.Records.Any(r => r.Name == root);
    }

    private static bool IsCodedPreset(string name) =>
        PresetFactories.Calendars.ContainsKey(name) || PresetFactories.DayCounters.ContainsKey(name)
        || PresetFactories.Currencies.ContainsKey(name) || CodedIndexKind(name) is not null;

    private static object Validate(ConventionSet set, string name)
    {
        set.ValidateIndex(name);
        return name;
    }

    private static object ValidateConvention(ConventionSet set, ConventionKind kind, string name)
    {
        set.ValidateConvention(kind, name);
        return name;
    }

    // QuantLib's own aliases between coded presets, its shared implementations: the only aliases a preset's name takes.
    private static string? QuantLibAlias(string name) =>
        Shipped.Value.index.TryGetValue((ConventionKind.Alias, name), out List<Entry>? e) && IsCodedPreset(name)
            ? e[0].Record.Base
            : null;

    private static ConventionSet Validated(ConventionSet set, int from)
    {
        for (int layer = from; layer < set.layers.Length; ++layer)
        {
            ConventionSnapshot s = set.layers[layer];
            for (int i = 0; i < s.Records.Length; ++i)
            {
                set.Check(s.Records[i], s.Sources[i], layer);
            }
        }

        // every record of the new set, its earlier layers' included, resolves
        foreach (((ConventionKind kind, string name), List<Entry> entries) in set.index)
        {
            try
            {
                using (Use(set))
                {
                    _ = kind switch
                    {
                        ConventionKind.Calendar => (object)set.CalendarOf(name, set.layers.Length, [], "snapshot"),
                        ConventionKind.DayCounter => set.DayCounterOf(name, set.layers.Length, [], "snapshot"),
                        ConventionKind.Currency => set.CurrencyOf(name, [], "snapshot"),
                        ConventionKind.Region => set.RegionOf(name, [], "snapshot"),
                        ConventionKind.Index => Validate(set, name),
                        ConventionKind.Alias or ConventionKind.Enumeration => set.AliasTarget(name, []),
                        _ => ValidateConvention(set, kind, name),
                    };
                }
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                Entry top = entries[0];
                throw new ArgumentException(
                    $"{ConventionKinds.Element(top.Record)} {name}{Where(top.Source)}: {e.Message}", "snapshot", e);
            }
        }

        return set;
    }

    private static string Where(string? source) => source is null ? "" : $" ({source})";

    private static ArgumentException Unknown(string kind, string name, string paramName) =>
        new($"unknown {kind} {name}", paramName);

    // The names a record refers to.
    private static IEnumerable<string> References(ConventionRecord r)
    {
        if (r.Base is string b && b != r.Name)
        {
            yield return b;
        }

        switch (r)
        {
            case JointCalendarRecord j:
                foreach (string m in j.Members)
                {
                    yield return m;
                }

                break;
            case DayCounterPresetRecord { Calendar: string c }:
                yield return c;
                break;
            case DayCounterRecord { Calendar: string c }:
                yield return c;
                break;
            case CurrencyPresetRecord { TriangulationCurrency: { Length: > 0 } t }:
                yield return t;
                break;
        }

        foreach (string? name in r.References())
        {
            if (name is not null)
            {
                yield return name;
            }
        }
    }

    private static CalendarEdits Combine(CalendarEdits lower, ImmutableArray<Date> added, ImmutableArray<Date> removed)
    {
        ImmutableSortedSet<Date> a = lower.Added.Except(removed).Union(added);
        ImmutableSortedSet<Date> r = lower.Removed.Except(added).Union(removed);
        return new CalendarEdits(a, r);
    }

    // A record's own rules, checked when its snapshot is layered on.
    private void Check(ConventionRecord r, string? source, int layer)
    {
        string where = $"{ConventionKinds.Element(r)} {r.Name}{Where(source)}";
        void Refuse(string why) => throw new ArgumentException($"{where}: {why}", "snapshot");

        bool shippedLayer = onQuantLib && layer == 0;
        if (!shippedLayer && r.Name.Contains('/') && !IsShippedName(r.Name) && !IsGenerated(r.Name, layers[layer]))
        {
            Refuse("a name of a user's own may not hold '/', which the names ConventionRecords.Describe generates take");
        }

        string? alias = shippedLayer ? null : QuantLibAlias(r.Name);
        switch (r)
        {
            case CalendarPresetRecord p:
                if (!PresetFactories.Calendars.ContainsKey(p.Name))
                {
                    Refuse("no calendar preset has the name");
                }

                if (alias is not null)
                {
                    Refuse($"QuantLib's calendar shares {alias}'s implementation: edit {alias}");
                }

                CheckBase(p, Refuse);
                if (p.Base is null && p.QuantLibName is null)
                {
                    Refuse("QuantLibName is missing");
                }

                CheckQuantLibName(p.QuantLibName, () => PresetFactories.Calendars[p.Name]().Name, Refuse);
                CheckEdits(p.Name, p.AddedHolidays, p.RemovedHolidays, Refuse);
                break;
            case CalendarRecord c:
                if (PresetFactories.Calendars.ContainsKey(c.Name))
                {
                    if (alias is not null)
                    {
                        Refuse($"QuantLib's calendar shares {alias}'s implementation: edit {alias}");
                    }

                    if (c.Base != c.Name)
                    {
                        Refuse("a calendar preset takes only a calendar record based on itself");
                    }
                }

                CheckEdits(c.Name, c.AddedHolidays, c.RemovedHolidays, Refuse);
                break;
            case JointCalendarRecord or BespokeCalendarRecord:
                if (PresetFactories.Calendars.ContainsKey(r.Name))
                {
                    Refuse("a calendar preset takes no joint or bespoke calendar");
                }

                break;
            case DayCounterPresetRecord d:
                if (!PresetFactories.DayCounters.ContainsKey(d.Name))
                {
                    Refuse("no day counter preset has the name");
                }

                if (alias is not null)
                {
                    Refuse($"QuantLib's day counter shares {alias}'s implementation: use {alias}");
                }

                CheckBase(d, Refuse);
                if (d.Base is null && d.QuantLibName is null)
                {
                    Refuse("QuantLibName is missing");
                }

                CheckQuantLibName(d.QuantLibName, () => PresetFactories.DayCounters[d.Name]().Name, Refuse);
                if (d.Calendar is not null && d.Name != "Business252")
                {
                    Refuse("only Business252 counts a calendar's business days");
                }

                break;
            case DayCounterRecord d:
                if (PresetFactories.DayCounters.ContainsKey(d.Name) && d.Base != d.Name)
                {
                    Refuse("a day counter preset takes only a day counter record based on itself");
                }

                break;
            case CurrencyPresetRecord cur:
                if (!PresetFactories.Currencies.ContainsKey(cur.Name))
                {
                    Refuse("no currency of QuantLib's has the code: a user may not define one");
                }

                CheckBase(cur, Refuse);
                if (cur.Base is null && (cur.QuantLibName is null || cur.NumericCode is null || cur.Symbol is null
                    || cur.FractionSymbol is null || cur.FractionsPerUnit is null || cur.Rounding is null))
                {
                    Refuse("a field is missing: QuantLibName, NumericCode, Symbol, FractionSymbol, FractionsPerUnit and Rounding are required without a base");
                }

                CheckQuantLibName(cur.QuantLibName, () => PresetFactories.Currencies[cur.Name]().Name, Refuse);
                CheckQuantLibNumericCode(cur.NumericCode, () => PresetFactories.Currencies[cur.Name]().NumericCode, Refuse);
                if (cur.Rounding is { } rounding)
                {
                    _ = rounding.ToRounding();
                }

                break;
            case RegionRecord region:
                CheckIndex(region, shippedLayer, Refuse);
                if (PresetFactories.Regions.ContainsKey(region.Name))
                {
                    if (region.Base is null && region.RegionName is null)
                    {
                        Refuse("RegionName is missing");
                    }

                    // a region's name is its identity, QuantLib's
                    CheckQuantLibName(region.RegionName, () => PresetFactories.Regions[region.Name]().Name, Refuse);
                }

                break;
            case IborIndexRecord or IborFamilyRecord or OvernightIndexRecord or SwapIndexRecord or OvernightIndexedSwapIndexRecord
                or ZeroInflationIndexRecord or YoYInflationIndexRecord:
                CheckIndex(r, shippedLayer, Refuse);
                break;
            case ConventionRecord when IsInstrumentConvention(ConventionKinds.Of(r)):
                CheckConvention(r, ConventionKinds.Of(r), Refuse);
                break;
            case AliasRecord a:
                // the shipped aliases are the ones QuantLib's shared implementations allow
                if (!shippedLayer && IsCodedPreset(a.Name) && alias != a.Base)
                {
                    Refuse("a preset's name takes an alias only where QuantLib shares the presets' implementation");
                }

                break;
        }
    }

    // A preset record's base: none, or its own name for the record it replaces.
    private static void CheckBase(ConventionRecord r, Action<string> refuse)
    {
        if (r.Base is string b && b != r.Name)
        {
            refuse("a preset record takes only its own name as its base");
        }
    }

    // QuantLib's numeric code of a currency, built with QuantLib's conventions: with the code and the name, the
    // currency's identity, which its equality and the exchange-rate registries read.
    private static void CheckQuantLibNumericCode(int? given, Func<int> quantLibs, Action<string> refuse)
    {
        if (given is null)
        {
            return;
        }

        int expected;
        using (Use(ConventionProvider.QuantLib))
        {
            expected = quantLibs();
        }

        if (given != expected)
        {
            refuse($"QuantLib's numeric code is {expected}, not {given}");
        }
    }

    // QuantLib's name of a coded preset, built with QuantLib's conventions.
    private static void CheckQuantLibName(string? given, Func<string> quantLibs, Action<string> refuse)
    {
        if (given is null)
        {
            return;
        }

        string expected;
        using (Use(ConventionProvider.QuantLib))
        {
            expected = quantLibs();
        }

        if (given != expected)
        {
            refuse($"QuantLib's name is {expected}, not {given}");
        }
    }

    private static void CheckEdits(string name, ImmutableArray<Date> added, ImmutableArray<Date> removed, Action<string> refuse)
    {
        if (name == "NullCalendar" && !(added.IsDefaultOrEmpty && removed.IsDefaultOrEmpty))
        {
            refuse("NullCalendar is a technical placeholder and takes no edits");
        }

        if (added.Intersect(removed).FirstOrDefault() is { SerialNumber: > 0 } both)
        {
            refuse($"{both} is both added and removed");
        }
    }

    // The topmost record of a value kind, or an alias, named name in the layers below upTo.
    private Entry? Top(ConventionKind kind, string name, int upTo)
    {
        Entry? best = null;
        foreach (ConventionKind k in new[] { kind, ConventionKind.Alias })
        {
            if (index.TryGetValue((k, name), out List<Entry>? entries))
            {
                foreach (Entry e in entries)
                {
                    if (e.Layer < upTo)
                    {
                        if (best is null || e.Layer > best.Value.Layer)
                        {
                            best = e;
                        }

                        break;
                    }
                }
            }
        }

        return best;
    }

    private static void Enter(HashSet<string> visiting, string key, string name)
    {
        if (!visiting.Add(key))
        {
            throw new ArgumentException($"{name} is part of a cycle", "snapshot");
        }
    }

    private Calendar CalendarOf(string name, int upTo, HashSet<string> visiting, string paramName)
    {
        string key = $"calendar:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            if (Top(ConventionKind.Calendar, name, upTo) is not Entry e)
            {
                return PresetFactories.Calendars.TryGetValue(name, out Func<Calendar>? preset)
                    ? preset()
                    : throw Unknown("calendar", name, paramName);
            }

            switch (e.Record)
            {
                case AliasRecord a:
                    return CalendarOf(a.Base!, layers.Length, visiting, paramName);
                case CalendarPresetRecord:
                    // its edits, through the chain, come through ResolveCalendarEdits
                    return PresetFactories.Calendars[name]();
                case CalendarRecord c when c.Base == name:
                    return PresetFactories.Calendars.TryGetValue(name, out Func<Calendar>? coded)
                        ? coded()
                        : Edit(CalendarOf(name, e.Layer, visiting, paramName), c);
                case CalendarRecord c:
                    return Edit(CalendarOf(c.Base!, layers.Length, visiting, paramName), c);
                case JointCalendarRecord j:
                {
                    (JointCalendarRule rule, ImmutableArray<string> members) = JointParts(j, e.Layer, visiting);
                    if (members.Length < 2)
                    {
                        throw new ArgumentException($"the joint calendar {name} needs two or more members", paramName);
                    }

                    return new JointCalendar([.. members.Select(m => CalendarOf(m, layers.Length, visiting, paramName))], rule);
                }

                case BespokeCalendarRecord b:
                    return Bespoke(b, e.Layer, visiting);
                default:
                    throw Unknown("calendar", name, paramName);
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private static Calendar Edit(Calendar calendar, CalendarRecord c)
    {
        foreach (Date d in c.AddedHolidays)
        {
            calendar = calendar.AddHoliday(d);
        }

        foreach (Date d in c.RemovedHolidays)
        {
            calendar = calendar.RemoveHoliday(d);
        }

        return calendar;
    }

    // A joint record's rule and members, each from the record, else its base's.
    private (JointCalendarRule Rule, ImmutableArray<string> Members) JointParts(JointCalendarRecord j, int layer, HashSet<string> visiting)
    {
        if (j.Base is null)
        {
            return (j.Rule ?? JointCalendarRule.JoinHolidays, j.Members);
        }

        string key = $"joint:{j.Name}:{layer}";
        Enter(visiting, key, j.Name);
        try
        {
            int upTo = j.Base == j.Name ? layer : layers.Length;
            if (Top(ConventionKind.Calendar, j.Base, upTo) is not { Record: JointCalendarRecord b } e)
            {
                throw new ArgumentException($"the joint calendar {j.Name}'s base {j.Base} is no joint calendar", "snapshot");
            }

            (JointCalendarRule rule, ImmutableArray<string> members) = JointParts(b, e.Layer, visiting);
            return (j.Rule ?? rule, j.Members.IsDefaultOrEmpty ? members : j.Members);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private Calendar Bespoke(BespokeCalendarRecord b, int layer, HashSet<string> visiting)
    {
        var weekends = new List<Weekday>();
        var holidays = new List<Date>();
        var removed = new List<Date>();
        void Collect(BespokeCalendarRecord r, int at)
        {
            if (r.Base is string baseName)
            {
                string key = $"bespoke:{r.Name}:{at}";
                Enter(visiting, key, r.Name);
                int upTo = baseName == r.Name ? at : layers.Length;
                if (Top(ConventionKind.Calendar, baseName, upTo) is not { Record: BespokeCalendarRecord br } e)
                {
                    throw new ArgumentException($"the bespoke calendar {r.Name}'s base {baseName} is no bespoke calendar", "snapshot");
                }

                Collect(br, e.Layer);
                visiting.Remove(key);
            }

            weekends.AddRange(r.Weekends);
            holidays.AddRange(r.Holidays);
            removed.AddRange(r.RemovedHolidays);
        }

        Collect(b, layer);
        BespokeCalendar bespoke = new(b.Name);
        foreach (Weekday w in weekends)
        {
            bespoke = bespoke.AddWeekend(w);
        }

        Calendar calendar = bespoke;
        foreach (Date d in holidays)
        {
            calendar = calendar.AddHoliday(d);
        }

        foreach (Date d in removed)
        {
            calendar = calendar.RemoveHoliday(d);
        }

        return calendar;
    }

    // A calendar preset's edits through its records' chain below upTo, or null where the set has no record of it.
    private CalendarEdits? EditsOf(string preset, int upTo)
    {
        Entry? found = null;
        if (index.TryGetValue((ConventionKind.Calendar, preset), out List<Entry>? entries))
        {
            found = entries.FirstOrDefault(e => e.Layer < upTo);
            found = found is { Record: not null } ? found : null;
        }

        return found switch
        {
            { Record: CalendarPresetRecord p } f => Combine(
                p.Base == preset ? EditsOf(preset, f.Layer) ?? CalendarEdits.None : CalendarEdits.None,
                p.AddedHolidays,
                p.RemovedHolidays),
            { Record: CalendarRecord c } f => Combine(EditsOf(preset, f.Layer) ?? CalendarEdits.None, c.AddedHolidays, c.RemovedHolidays),
            _ => null,
        };
    }

    // Business252's calendar's name through its records' chain below upTo, or null for QuantLib's reference.
    private string? Business252CalendarOf(int upTo)
    {
        if (!index.TryGetValue((ConventionKind.DayCounter, "Business252"), out List<Entry>? entries)
            || entries.FirstOrDefault(e => e.Layer < upTo) is not { Record: not null } found)
        {
            return null;
        }

        string? calendar = found.Record switch
        {
            DayCounterPresetRecord p => p.Calendar,
            DayCounterRecord d => d.Calendar,
            _ => null,
        };
        return calendar ?? (found.Record.Base == "Business252" ? Business252CalendarOf(found.Layer) : null);
    }

    private DayCounter DayCounterOf(string name, int upTo, HashSet<string> visiting, string paramName)
    {
        string key = $"daycounter:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            if (Top(ConventionKind.DayCounter, name, upTo) is not Entry e)
            {
                return PresetFactories.DayCounters.TryGetValue(name, out Func<DayCounter>? preset)
                    ? preset()
                    : throw Unknown("day counter", name, paramName);
            }

            switch (e.Record)
            {
                case AliasRecord a:
                    return DayCounterOf(a.Base!, layers.Length, visiting, paramName);
                case DayCounterPresetRecord:
                    return PresetFactories.DayCounters[name]();
                case DayCounterRecord d when d.Base == name && PresetFactories.DayCounters.TryGetValue(name, out Func<DayCounter>? coded):
                    return coded();
                case DayCounterRecord d:
                {
                    DayCounter b = d.Base == name
                        ? DayCounterOf(name, e.Layer, visiting, paramName)
                        : DayCounterOf(d.Base!, layers.Length, visiting, paramName);
                    if (d.Calendar is not string calendar)
                    {
                        return b;
                    }

                    return b is Business252
                        ? new Business252(CalendarOf(calendar, layers.Length, visiting, paramName))
                        : throw new ArgumentException($"the day counter {name}'s base {d.Base} counts no calendar's days: only Business252 does", paramName);
                }

                default:
                    throw Unknown("day counter", name, paramName);
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private Currency CurrencyOf(string code, HashSet<string> visiting, string paramName)
    {
        string key = $"currency:{code}";
        Enter(visiting, key, code);
        try
        {
            Entry? e = Top(ConventionKind.Currency, code, layers.Length);
            if (e?.Record is AliasRecord a)
            {
                return CurrencyOf(a.Base!, visiting, paramName);
            }

            return PresetFactories.Currencies.TryGetValue(code, out Func<Currency>? preset)
                ? preset()
                : throw Unknown("currency", code, paramName);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    // A currency preset's record merged through its chain below upTo, or null where the set has none.
    private CurrencyPresetRecord? MergedCurrency(string code, int upTo)
    {
        if (!index.TryGetValue((ConventionKind.Currency, code), out List<Entry>? entries)
            || entries.FirstOrDefault(e => e.Layer < upTo) is not { Record: CurrencyPresetRecord r } found)
        {
            return null;
        }

        if (r.Base != code || MergedCurrency(code, found.Layer) is not { } lower)
        {
            return r;
        }

        return r with
        {
            QuantLibName = r.QuantLibName ?? lower.QuantLibName,
            NumericCode = r.NumericCode ?? lower.NumericCode,
            Symbol = r.Symbol ?? lower.Symbol,
            FractionSymbol = r.FractionSymbol ?? lower.FractionSymbol,
            FractionsPerUnit = r.FractionsPerUnit ?? lower.FractionsPerUnit,
            Rounding = r.Rounding ?? lower.Rounding,
            TriangulationCurrency = r.TriangulationCurrency ?? lower.TriangulationCurrency,
        };
    }

    // An alias's target, in whichever kind its base resolves: a calendar, day counter or currency, or a member of a
    // business-day convention.
    private object AliasTarget(string name, HashSet<string> visiting)
    {
        if (Top(ConventionKind.Alias, name, layers.Length) is not { Record: AliasRecord a })
        {
            throw Unknown("alias", name, "snapshot");
        }

        string b = a.Base!;
        if (Enum.TryParse(b, false, out BusinessDayConvention c) && Enum.IsDefined(c) && c.ToString() == b)
        {
            return c;
        }

        bool calendar = Top(ConventionKind.Calendar, b, layers.Length) is not null || PresetFactories.Calendars.ContainsKey(b);
        bool dayCounter = Top(ConventionKind.DayCounter, b, layers.Length) is not null || PresetFactories.DayCounters.ContainsKey(b);
        bool currency = Top(ConventionKind.Currency, b, layers.Length) is not null || PresetFactories.Currencies.ContainsKey(b);
        bool region = Top(ConventionKind.Region, b, layers.Length) is not null || PresetFactories.Regions.ContainsKey(b);
        bool index = Top(ConventionKind.Index, b, layers.Length) is not null || (CodedIndexKind(b) is not null && !PresetFactories.Regions.ContainsKey(b));
        return (calendar, dayCounter, currency, region, index) switch
        {
            (true, false, false, false, false) => CalendarOf(name, layers.Length, visiting, "snapshot"),
            (false, true, false, false, false) => DayCounterOf(name, layers.Length, visiting, "snapshot"),
            (false, false, true, false, false) => CurrencyOf(name, visiting, "snapshot"),
            (false, false, false, true, false) => RegionOf(name, visiting, "snapshot"),
            (false, false, false, false, true) => b,
            (false, false, false, false, false) => throw Unknown("calendar, day counter, currency, region, index or business-day convention", b, "snapshot"),
            _ => throw new ArgumentException($"the alias {name}'s base {b} names values of more than one kind", "snapshot"),
        };
    }

    // The instrument conventions. A record's fields are merged down its base chain, a field left out the next record's;
    // a builder's default record answers the provider's member for its builder, a field it leaves out QuantLib's
    // reference. A default record holds only what its builder fixes, never what the builder derives from its index.

    private const string SwapDefaults = "MakeVanillaSwap.";

    private const string OisDefaultsName = "MakeOIS";

    // The fields each builder's default record may give: what the builder fixes, by the record's name.
    private static readonly Dictionary<string, (ConventionKind Kind, string[] Fields)> DefaultRecords = new(StringComparer.Ordinal)
    {
        [SwapDefaults] = (ConventionKind.SwapConvention, [
            "FixedLegTenor", "FixedLegTenorThreshold", "FixedLegDayCounter", "FixedLegConvention", "FixedLegTerminationDateConvention",
            "FixedLegRule", "FixedLegEndOfMonth", "FloatingLegRule", "FloatingLegEndOfMonth"]),
        [OisDefaultsName] = (ConventionKind.OisConvention, [
            "SettlementDays", "PaymentLag", "PaymentConvention", "PaymentFrequency", "FixedLegFrequency", "FixedLegConvention",
            "FixedLegTerminationDateConvention", "OvernightLegConvention", "OvernightLegTerminationDateConvention", "FixedLegRule",
            "OvernightLegRule", "TelescopicValueDates", "Averaging", "LockoutDays", "ObservationShift"]),
        [nameof(MakeCreditDefaultSwap)] = (ConventionKind.CdsConvention, [
            "Calendar", "CouponFrequency", "PaymentConvention", "Rule", "DayCounter", "LastPeriodDayCounter", "SettlesAccrual",
            "PaysAtDefaultTime", "RebatesAccrual", "CashSettlementDays"]),
        [nameof(MakeCapFloor)] = (ConventionKind.CapFloorConvention, ["FirstCapletExcluded"]),
        [nameof(MakeSwaption)] = (ConventionKind.SwaptionConvention, ["SettlementType", "SettlementMethod", "OptionConvention"]),
        [nameof(MakeCms)] = (ConventionKind.CmsConvention, [
            "CmsLegTenor", "CmsLegConvention", "CmsLegTerminationDateConvention", "CmsLegRule", "CmsLegEndOfMonth", "CmsLegDayCounter",
            "FloatingLegRule", "FloatingLegEndOfMonth"]),
        [nameof(MakeMultipleResetsSwap)] = (ConventionKind.MultipleResetsSwapConvention, ["FixedLegConvention", "Averaging"]),
        [nameof(MakeYoYInflationCapFloor)] = (ConventionKind.YoYInflationCapFloorConvention, ["PaymentDayCounter", "PaymentAdjustment", "FixingDays"]),
        [nameof(FixedRateBond)] = (ConventionKind.BondConvention, ["PaymentConvention", "Redemption", "ExCouponConvention", "ExCouponEndOfMonth", "PriceType"]),
        [nameof(BTP)] = (ConventionKind.BondConvention, [
            "SettlementDays", "Frequency", "Calendar", "Convention", "TerminationDateConvention", "Rule", "EndOfMonth", "AccrualDayCounter",
            "PaymentConvention", "PaymentCalendar", "ExCouponPeriod", "ExCouponCalendar", "ExCouponConvention", "ExCouponEndOfMonth"]),
    };

    // The default record a name is, MakeVanillaSwap.<currency> and MakeOIS.<class> by their prefixes, or null.
    private static string? DefaultRecordOf(string name) =>
        DefaultRecords.ContainsKey(name) ? name
        : name.StartsWith(SwapDefaults, StringComparison.Ordinal) && name.Length > SwapDefaults.Length ? SwapDefaults
        : name.StartsWith(OisDefaultsName + ".", StringComparison.Ordinal) && name.Length > OisDefaultsName.Length + 1 ? OisDefaultsName
        : null;

    private static bool IsInstrumentConvention(ConventionKind kind) => !ConventionKinds.Aliased(kind);

    // Whether an index name is an overnight-indexed swap index's record, through its aliases.
    internal bool IsOvernightIndexedSwapIndex(string name) =>
        Top(ConventionKind.Index, name, layers.Length)?.Record switch
        {
            AliasRecord a => IsOvernightIndexedSwapIndex(a.Base!),
            OvernightIndexedSwapIndexRecord => true,
            _ => false,
        };

    private T Merged<T>(ConventionKind kind, string name, Func<T, T, T> over)
        where T : ConventionRecord
    {
        ArgumentNullException.ThrowIfNull(name);
        return ConventionRecordOf(kind, name, layers.Length, [], over) ?? throw Unknown(kind.ToString(), name, nameof(name));
    }

    private T? DefaultRecord<T>(ConventionKind kind, string name, Func<T, T, T> over)
        where T : ConventionRecord =>
        ConventionRecordOf(kind, name, layers.Length, [], over);

    // An instrument convention's record merged down its base chain below upTo, or null where the set has none of the
    // name in the kind; no alias names one.
    private T? ConventionRecordOf<T>(ConventionKind kind, string name, int upTo, HashSet<string> visiting, Func<T, T, T> over)
        where T : ConventionRecord
    {
        string key = $"{kind}:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            Entry? e = null;
            if (index.TryGetValue((kind, name), out List<Entry>? entries))
            {
                e = entries.FirstOrDefault(x => x.Layer < upTo) is { Record: not null } found ? found : null;
            }

            if (e is not Entry top)
            {
                return null;
            }

            var r = (T)top.Record;
            T? lower = r.Base switch
            {
                null => null,
                string b when b == name => ConventionRecordOf(kind, name, top.Layer, visiting, over),
                string b => ConventionRecordOf(kind, b, layers.Length, visiting, over)
                    ?? throw new ArgumentException($"unknown {kind} {b}", "snapshot"),
            };
            return lower is null ? r : over(r, lower);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private BondConventionRecord? FixedRateBondDefaults() =>
        DefaultRecord<BondConventionRecord>(ConventionKind.BondConvention, nameof(FixedRateBond), static (a, b) => a.Over(b));

    // The coded bond preset a bond record's chain reaches, or null.
    private string? BondPresetOf(string name)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (string? n = name; n is not null && seen.Add(n);)
        {
            if (n == nameof(BTP))
            {
                return n;
            }

            n = index.TryGetValue((ConventionKind.BondConvention, n), out List<Entry>? entries) ? entries[0].Record.Base : null;
        }

        return null;
    }

    // An instrument convention record's own rules: a default record's name keeps its kind, holds only what its builder
    // fixes and bases on its own name or its builder's record; a threshold is a currency's default record's.
    private static void CheckConvention(ConventionRecord r, ConventionKind kind, Action<string> refuse)
    {
        if (DefaultRecordOf(r.Name) is string builder)
        {
            (ConventionKind defaultKind, string[] fields) = DefaultRecords[builder];
            if (kind != defaultKind)
            {
                refuse($"the name is the {builder.TrimEnd('.')} builder's default record, a {defaultKind}");
            }

            if (r.Given().FirstOrDefault(f => !fields.Contains(f)) is string derived)
            {
                refuse($"a default record holds only what its builder fixes, not {derived}");
            }

            if (r.Base is string b && b != r.Name && DefaultRecordOf(b) != builder && !(builder == OisDefaultsName && b == OisDefaultsName))
            {
                refuse("a default record takes only its own name or its builder's record as its base");
            }
        }
        else if (r is SwapConventionRecord { FixedLegTenorThreshold: not null })
        {
            refuse("FixedLegTenorThreshold is a currency's default record's, MakeVanillaSwap.<currency>");
        }
        else if (r.Base is string b && DefaultRecordOf(b) is string defaults && defaults != nameof(BTP))
        {
            refuse($"a convention takes its builder's defaults without basing on them: {b} is the {defaults.TrimEnd('.')} builder's default record");
        }
    }

    // Every name an instrument convention's record gives resolves, and its chain carries no default record's field it
    // can't take.
    private void ValidateConvention(ConventionKind kind, string name)
    {
        if (DefaultRecordOf(name) is not null)
        {
            ValidateDefaults(name);
            return;
        }

        switch (kind)
        {
            case ConventionKind.ScheduleConvention:
                ScheduleConvention schedule = Schedule(name);
                if (schedule.Record is { Tenor: not null, Frequency: not null })
                {
                    throw new ArgumentException("Tenor and Frequency are one option, the schedule's tenor: the record and its bases give both", "snapshot");
                }

                schedule.Validate();
                break;
            case ConventionKind.SwapConvention:
                SwapConvention swap = Swap(name);
                if (swap.Record.FixedLegTenorThreshold is not null)
                {
                    throw new ArgumentException("FixedLegTenorThreshold is a currency's default record's, MakeVanillaSwap.<currency>", "snapshot");
                }

                swap.Validate();
                break;
            case ConventionKind.OisConvention:
                Ois(name).Validate();
                break;
            case ConventionKind.CdsConvention:
                Cds(name).Validate();
                break;
            case ConventionKind.ZeroCouponInflationSwapConvention:
                ZeroCouponInflationSwap(name).Validate();
                break;
            case ConventionKind.YearOnYearInflationSwapConvention:
                YearOnYearInflationSwap(name).Validate();
                break;
            case ConventionKind.CrossCurrencyBasisSwapConvention:
                CrossCurrencyBasisSwap(name).Validate();
                break;
            case ConventionKind.CrossCurrencySwapConvention:
                CrossCurrencySwap(name).Validate();
                break;
            case ConventionKind.FxSwapConvention:
                FxSwap(name).Validate();
                break;
            case ConventionKind.CapFloorConvention:
                CapFloor(name).Validate();
                break;
            case ConventionKind.SwaptionConvention:
                Swaption(name).Validate();
                break;
            case ConventionKind.CmsConvention:
                Cms(name).Validate();
                break;
            case ConventionKind.MultipleResetsSwapConvention:
                MultipleResetsSwap(name).Validate();
                break;
            case ConventionKind.YoYInflationCapFloorConvention:
                YoYInflationCapFloor(name).Validate();
                break;
            case ConventionKind.BondConvention:
                Bond(name).Validate();
                break;
        }
    }

    // A default record resolves as its builder reads it; an OIS builder's and the bonds' default records name nothing.
    private void ValidateDefaults(string name)
    {
        switch (DefaultRecordOf(name))
        {
            case SwapDefaults:
                _ = MakeVanillaSwap.Defaults(CurrencyOf(name[SwapDefaults.Length..], [], "snapshot"), this);
                break;
            case nameof(MakeCreditDefaultSwap):
                _ = MakeCreditDefaultSwap.Defaults(this);
                break;
            case nameof(MakeCapFloor):
                _ = MakeCapFloor.Defaults(this);
                break;
            case nameof(MakeSwaption):
                _ = MakeSwaption.Defaults(this);
                break;
            case nameof(MakeCms):
                _ = MakeCms.Defaults(this);
                break;
            case nameof(MakeMultipleResetsSwap):
                _ = MakeMultipleResetsSwap.Defaults(this);
                break;
            case nameof(MakeYoYInflationCapFloor):
                _ = MakeYoYInflationCapFloor.Defaults(this);
                break;
            case nameof(BTP):
                using (Use(this))
                {
                    _ = new BTP(new Date(15, Month.October, 2036), 0.03, new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) });
                }

                break;
        }
    }

    // The index and region kinds. A record's fields are merged down its base chain, a field left out the next record's,
    // then, for a coded preset, QuantLib's reference. A family's fields are taken at a tenor by the tenor's unit.

    // The coded fixed-tenor Ibor preset of a family at a tenor, or null.
    private static string? FixedPresetOf(string family, Period tenor) =>
        PresetFactories.FixedTenorPresets.FirstOrDefault(p => p.Value.Family == family && p.Value.Tenor == tenor).Key;

    private static Period? FixedPresetTenor(string name) =>
        PresetFactories.FixedTenorPresets.TryGetValue(name, out (string Family, Period Tenor) p) ? p.Tenor : null;

    // The record kind a coded index or region preset's name takes, or null for a name no coded preset has.
    private static Type? CodedIndexKind(string name) =>
        PresetFactories.IborIndexes.ContainsKey(name) || PresetFactories.IborIndexesWithSettlementDays.ContainsKey(name)
            || PresetFactories.OtherIndexes.ContainsKey(name) ? typeof(IborIndexRecord)
        : PresetFactories.IborFamilies.ContainsKey(name) ? typeof(IborFamilyRecord)
        : PresetFactories.OvernightIndexes.ContainsKey(name) ? typeof(OvernightIndexRecord)
        : PresetFactories.SwapIndexes.ContainsKey(name) ? typeof(SwapIndexRecord)
        : PresetFactories.ZeroInflationIndexes.ContainsKey(name) || PresetFactories.ZeroInflationIndexesWithArguments.ContainsKey(name)
            ? typeof(ZeroInflationIndexRecord)
        : PresetFactories.YoYInflationIndexes.ContainsKey(name) || PresetFactories.YoYInflationIndexesWithArguments.ContainsKey(name)
            ? typeof(YoYInflationIndexRecord)
        : PresetFactories.Regions.ContainsKey(name) ? typeof(RegionRecord)
        : null;

    private static bool TakesArguments(string name) =>
        PresetFactories.IborIndexesWithSettlementDays.ContainsKey(name)
        || PresetFactories.ZeroInflationIndexesWithArguments.ContainsKey(name)
        || PresetFactories.YoYInflationIndexesWithArguments.ContainsKey(name);

    private static ArgumentException Missing(string kind, string name, string field) =>
        new($"the {kind} {name} gives no {field}", "snapshot");

    private static bool SameIbor(IborIndex a, IborIndex b) =>
        a.GetType() == b.GetType() && a.FamilyName == b.FamilyName && a.Tenor == b.Tenor && a.FixingDays == b.FixingDays
        && a.Currency == b.Currency && a.FixingCalendar == b.FixingCalendar && a.BusinessDayConvention == b.BusinessDayConvention
        && a.EndOfMonth == b.EndOfMonth && a.DayCounter == b.DayCounter;

    // A coded preset's values where the set has no record of it: QuantLib's on QuantLib's presets, none on an empty set.
    private T Undefined<T>(string preset, Func<T> reference) =>
        onQuantLib ? reference() : throw new ArgumentException($"the set defines no {preset}, and isn't built on QuantLib's presets", "preset");

    // The coded preset an index record's chain reaches whose constructor takes arguments, or null.
    private string? ArgumentBaseOf(string name, HashSet<string> visiting)
    {
        for (string? n = name; n is not null;)
        {
            if (n != name && TakesArguments(n))
            {
                return n;
            }

            string key = $"arguments:{n}";
            if (!visiting.Add(key))
            {
                throw new ArgumentException($"{n} is part of a cycle", "snapshot");
            }

            string? b = Top(ConventionKind.Index, n, layers.Length)?.Record.Base;
            n = b == n ? null : b;
        }

        return null;
    }

    private IborFields? IborFieldsOf(string name, int upTo, HashSet<string> visiting)
    {
        string key = $"ibor:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            if (Top(ConventionKind.Index, name, upTo) is not Entry e)
            {
                return null;
            }

            switch (e.Record)
            {
                case AliasRecord a:
                    return IborFieldsOf(a.Base!, layers.Length, visiting);
                case IborIndexRecord r:
                {
                    var own = new IborFields(r.Family, r.Tenor, r.FixingDays, r.Currency, r.FixingCalendar, r.Convention,
                        r.EndOfMonth, r.DayCounter, r.Style, r.FinancialCenterCalendar, r.ValueCalendar, r.MaturityCalendar);
                    return own.Over(r.Base switch
                    {
                        null => null,
                        string b when b == name => IborFieldsOf(name, e.Layer, visiting),
                        string b when IsFamily(b) => FamilyFieldsAt(
                            b, r.Tenor ?? throw new ArgumentException($"the index {name} is based on the family {b} and gives no tenor", "snapshot"),
                            layers.Length,
                            visiting),
                        string b => IborFieldsOf(b, layers.Length, visiting) ?? KnownBase(b),
                    });
                }

                case OvernightIndexRecord o:
                {
                    var own = new IborFields(o.Family, new Period(1, TimeUnit.Days), o.FixingDays, o.Currency, o.FixingCalendar,
                        BusinessDayConvention.Following, false, o.DayCounter, null, null, null, null);
                    return own.Over(o.Base switch
                    {
                        null => null,
                        string b when b == name => IborFieldsOf(name, e.Layer, visiting),
                        string b => IborFieldsOf(b, layers.Length, visiting) ?? KnownBase(b),
                    });
                }

                default:
                    throw new ArgumentException($"{name} is a {ConventionKinds.Element(e.Record)}, no Ibor or overnight index", "snapshot");
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    // A base no record defines: a coded preset's, whose values are QuantLib's reference, or an unknown name.
    private static IborFields? KnownBase(string name) =>
        CodedIndexKind(name) is not null ? null : throw new ArgumentException($"unknown index {name}", "snapshot");

    private bool IsFamily(string name) =>
        Top(ConventionKind.Index, name, layers.Length)?.Record is IborFamilyRecord
        || (Top(ConventionKind.Index, name, layers.Length) is null && PresetFactories.IborFamilies.ContainsKey(name));

    // A family's records merged down their chain, or null where the set has none.
    private IborFamilyRecord? FamilyRecordOf(string name, int upTo, HashSet<string> visiting)
    {
        string key = $"family:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            switch (Top(ConventionKind.Index, name, upTo)?.Record)
            {
                case null:
                    return null;
                case AliasRecord a:
                    return FamilyRecordOf(a.Base!, layers.Length, visiting);
                case IborFamilyRecord f:
                {
                    int layer = Top(ConventionKind.Index, name, upTo)!.Value.Layer;
                    IborFamilyRecord? lower = f.Base switch
                    {
                        null => null,
                        string b when b == name => FamilyRecordOf(name, layer, visiting),
                        string b => FamilyRecordOf(b, layers.Length, visiting)
                            ?? (PresetFactories.IborFamilies.ContainsKey(b) ? null : throw new ArgumentException($"unknown Ibor family {b}", "snapshot")),
                    };
                    return lower is null ? f : f with
                    {
                        Family = f.Family ?? lower.Family,
                        FixingDays = f.FixingDays ?? lower.FixingDays,
                        FixingDaysAtOneDay = f.FixingDaysAtOneDay ?? lower.FixingDaysAtOneDay,
                        Currency = f.Currency ?? lower.Currency,
                        FixingCalendar = f.FixingCalendar ?? lower.FixingCalendar,
                        FinancialCenterCalendar = f.FinancialCenterCalendar ?? lower.FinancialCenterCalendar,
                        DayCounter = f.DayCounter ?? lower.DayCounter,
                        Units = f.Units.IsDefaultOrEmpty ? lower.Units : f.Units,
                    };
                }

                case ConventionRecord other:
                    throw new ArgumentException($"{name} is a {ConventionKinds.Element(other)}, no Ibor family", "snapshot");
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    // A family's fields at a tenor: the convention and end of month by the tenor's unit, the fixing days at a day.
    private IborFields? FamilyFieldsAt(string family, Period tenor, int upTo, HashSet<string> visiting)
    {
        if (FamilyRecordOf(family, upTo, visiting) is not { } f)
        {
            return null;
        }

        IborFamilyUnit unit = f.Units.FirstOrDefault(u => u.TimeUnit == tenor.Units)
            ?? throw new ArgumentException($"the family {family} takes no tenor in {tenor.Units}", "tenor");
        int? fixingDays = tenor == new Period(1, TimeUnit.Days) && f.FixingDaysAtOneDay is int atOneDay ? atOneDay : f.FixingDays;
        return new IborFields(f.Family, tenor, fixingDays, f.Currency, f.FixingCalendar, unit.Convention, unit.EndOfMonth,
            f.DayCounter, null, f.FinancialCenterCalendar, null, null);
    }

    // A coded preset's conventions: its fields, each left out QuantLib's reference.
    private IborIndexConventions IborConventions(IborFields f, Func<IborIndexConventions> reference, string what)
    {
        IborIndexConventions? q = null;
        IborIndexConventions Q() => q ??= reference();
        return new IborIndexConventions(
            f.FixingDays ?? Q().FixingDays,
            f.Currency is string c ? CurrencyOf(c, [], what) : Q().Currency,
            f.FixingCalendar is string cal ? CalendarOf(cal, layers.Length, [], what) : Q().FixingCalendar,
            f.Convention ?? Q().Convention,
            f.EndOfMonth ?? Q().EndOfMonth,
            f.DayCounter is string dc ? DayCounterOf(dc, layers.Length, [], what) : Q().DayCounter,
            f.FinancialCenterCalendar is string fc ? CalendarOf(fc, layers.Length, [], what) : Q().FinancialCenterCalendar);
    }

    private SwapIndexRecord? SwapFieldsOf(string name, int upTo, HashSet<string> visiting)
    {
        string key = $"swap:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            Entry? e = Top(ConventionKind.Index, name, upTo);
            switch (e?.Record)
            {
                case null:
                    return null;
                case AliasRecord a:
                    return SwapFieldsOf(a.Base!, layers.Length, visiting);
                case SwapIndexRecord s:
                {
                    SwapIndexRecord? lower = s.Base switch
                    {
                        null => null,
                        string b when b == name => SwapFieldsOf(name, e.Value.Layer, visiting),
                        string b => SwapFieldsOf(b, layers.Length, visiting)
                            ?? (PresetFactories.SwapIndexes.ContainsKey(b) ? null : throw new ArgumentException($"unknown swap index {b}", "snapshot")),
                    };
                    return lower is null ? s : s with
                    {
                        Family = s.Family ?? lower.Family,
                        SettlementDays = s.SettlementDays ?? lower.SettlementDays,
                        Currency = s.Currency ?? lower.Currency,
                        FixingCalendar = s.FixingCalendar ?? lower.FixingCalendar,
                        FixedLegTenor = s.FixedLegTenor ?? lower.FixedLegTenor,
                        FixedLegConvention = s.FixedLegConvention ?? lower.FixedLegConvention,
                        FixedLegDayCounter = s.FixedLegDayCounter ?? lower.FixedLegDayCounter,
                        IborIndex = s.IborIndex ?? lower.IborIndex,
                        IborIndexTenor = s.IborIndex is null ? s.IborIndexTenor ?? lower.IborIndexTenor : s.IborIndexTenor,
                        ShortIborIndex = s.ShortIborIndex ?? lower.ShortIborIndex,
                        ShortIborIndexTenor = s.ShortIborIndex is null ? s.ShortIborIndexTenor ?? lower.ShortIborIndexTenor : s.ShortIborIndexTenor,
                        ShortFixedLegTenor = s.ShortFixedLegTenor ?? lower.ShortFixedLegTenor,
                    };
                }

                case ConventionRecord other:
                    throw new ArgumentException($"{name} is a {ConventionKinds.Element(other)}, no swap index", "snapshot");
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private OvernightIndexedSwapIndexRecord? OisFieldsOf(string name, int upTo, HashSet<string> visiting)
    {
        string key = $"ois:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            Entry? e = Top(ConventionKind.Index, name, upTo);
            switch (e?.Record)
            {
                case null:
                    return null;
                case AliasRecord a:
                    return OisFieldsOf(a.Base!, layers.Length, visiting);
                case OvernightIndexedSwapIndexRecord o:
                {
                    OvernightIndexedSwapIndexRecord? lower = o.Base switch
                    {
                        null => null,
                        string b when b == name => OisFieldsOf(name, e.Value.Layer, visiting),
                        string b => OisFieldsOf(b, layers.Length, visiting) ?? throw new ArgumentException($"unknown overnight-indexed swap index {b}", "snapshot"),
                    };
                    return lower is null ? o : o with
                    {
                        Family = o.Family ?? lower.Family,
                        SettlementDays = o.SettlementDays ?? lower.SettlementDays,
                        Currency = o.Currency ?? lower.Currency,
                        OvernightIndex = o.OvernightIndex ?? lower.OvernightIndex,
                        TelescopicValueDates = o.TelescopicValueDates ?? lower.TelescopicValueDates,
                        Averaging = o.Averaging ?? lower.Averaging,
                    };
                }

                case ConventionRecord other:
                    throw new ArgumentException($"{name} is a {ConventionKinds.Element(other)}, no overnight-indexed swap index", "snapshot");
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    // A zero or year-on-year index's fields merged down its chain, or null where the set has no record.
    private YoYInflationIndexRecord? InflationFieldsOf(string name, int upTo, HashSet<string> visiting)
    {
        string key = $"inflation:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            Entry? e = Top(ConventionKind.Index, name, upTo);
            YoYInflationIndexRecord own;
            switch (e?.Record)
            {
                case null:
                    return null;
                case AliasRecord a:
                    return InflationFieldsOf(a.Base!, layers.Length, visiting);
                case ZeroInflationIndexRecord z:
                    own = new YoYInflationIndexRecord(z.Name, z.Base)
                    {
                        Family = z.Family, Region = z.Region, Revised = z.Revised, Frequency = z.Frequency,
                        AvailabilityLag = z.AvailabilityLag, Currency = z.Currency,
                    };
                    break;
                case YoYInflationIndexRecord y:
                    own = y;
                    break;
                case ConventionRecord other:
                    throw new ArgumentException($"{name} is a {ConventionKinds.Element(other)}, no inflation index", "snapshot");
            }

            YoYInflationIndexRecord? lower = own.Base switch
            {
                null => null,
                string b when b == name => InflationFieldsOf(name, e!.Value.Layer, visiting),
                string b => InflationFieldsOf(b, layers.Length, visiting)
                    ?? (CodedIndexKind(b) is not null ? null : throw new ArgumentException($"unknown inflation index {b}", "snapshot")),
            };
            return lower is null ? own : own with
            {
                UnderlyingIndex = own.UnderlyingIndex ?? lower.UnderlyingIndex,
                Family = own.Family ?? lower.Family,
                Region = own.Region ?? lower.Region,
                Revised = own.Revised ?? lower.Revised,
                Frequency = own.Frequency ?? lower.Frequency,
                AvailabilityLag = own.AvailabilityLag ?? lower.AvailabilityLag,
                Currency = own.Currency ?? lower.Currency,
            };
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private RegionRecord? RegionFieldsOf(string name, int upTo, HashSet<string> visiting)
    {
        string key = $"region:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            Entry? e = Top(ConventionKind.Region, name, upTo);
            switch (e?.Record)
            {
                case null:
                    return null;
                case AliasRecord a:
                    return RegionFieldsOf(a.Base!, layers.Length, visiting);
                case RegionRecord r:
                {
                    RegionRecord? lower = r.Base switch
                    {
                        null => null,
                        string b when b == name => RegionFieldsOf(name, e.Value.Layer, visiting),
                        string b => RegionFieldsOf(b, layers.Length, visiting)
                            ?? (PresetFactories.Regions.ContainsKey(b) ? null : throw new ArgumentException($"unknown region {b}", "snapshot")),
                    };
                    return lower is null ? r : r with { RegionName = r.RegionName ?? lower.RegionName, Code = r.Code ?? lower.Code };
                }

                default:
                    throw new ArgumentException($"{name} is no region", "snapshot");
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private Region RegionOf(string name, HashSet<string> visiting, string paramName)
    {
        if (Top(ConventionKind.Region, name, layers.Length)?.Record is AliasRecord alias)
        {
            return RegionOf(alias.Base!, visiting, paramName);
        }

        if (PresetFactories.Regions.TryGetValue(name, out Func<Region>? preset))
        {
            return preset();
        }

        return RegionFieldsOf(name, layers.Length, visiting) is { } r
            ? new CustomRegion(r.RegionName ?? throw Missing("region", name, "RegionName"), r.Code ?? throw Missing("region", name, "Code"))
            : throw Unknown("region", name, paramName);
    }

    private IborIndex IborIndexOf(string name, HashSet<string> visiting, string paramName, YieldTermStructure? h)
    {
        Entry? e = Top(ConventionKind.Index, name, layers.Length);
        if (e?.Record is AliasRecord alias)
        {
            return IborIndexOf(alias.Base!, visiting, paramName, h);
        }

        if (PresetFactories.IborIndexes.TryGetValue(name, out Func<YieldTermStructure?, IborIndex>? preset))
        {
            return preset(h);
        }

        if (PresetFactories.OvernightIndexes.TryGetValue(name, out Func<YieldTermStructure?, OvernightIndex>? overnight))
        {
            return overnight(h);
        }

        if (PresetFactories.IborIndexesWithSettlementDays.ContainsKey(name))
        {
            throw new ArgumentException($"the {name} preset takes its settlement days: name a record based on it that gives them", paramName);
        }

        if (e?.Record is IborFamilyRecord || (e is null && PresetFactories.IborFamilies.ContainsKey(name)))
        {
            throw new ArgumentException($"{name} is an Ibor family, which takes a tenor", paramName);
        }

        IborFields f = e is null
            ? throw Unknown("Ibor index", name, paramName)
            : IborFieldsOf(name, layers.Length, visiting) ?? throw Unknown("Ibor index", name, paramName);
        if (ArgumentBaseOf(name, []) is string dailyTenor)
        {
            return PresetFactories.IborIndexesWithSettlementDays[dailyTenor](f.FixingDays ?? throw Missing("index", name, "FixingDays"), h);
        }

        Currency currency = CurrencyOf(f.Currency ?? throw Missing("index", name, "Currency"), [], "snapshot");
        DayCounter dayCounter = DayCounterOf(f.DayCounter ?? throw Missing("index", name, "DayCounter"), layers.Length, [], "snapshot");
        int fixingDays = f.FixingDays ?? throw Missing("index", name, "FixingDays");
        if (e.Value.Record is OvernightIndexRecord)
        {
            return new OvernightIndex(f.Family ?? name, fixingDays, currency, IndexCalendar(f.FixingCalendar, name), dayCounter, h);
        }

        Period tenor = f.Tenor ?? throw Missing("index", name, "Tenor");
        return (f.Style ?? IborIndexStyle.Ibor) switch
        {
            IborIndexStyle.Libor => new Libor(
                f.Family ?? name, tenor, fixingDays, currency, IndexCalendar(f.FinancialCenterCalendar, name, "FinancialCenterCalendar"), dayCounter, h),
            IborIndexStyle.Custom => new CustomIborIndex(
                f.Family ?? name,
                tenor,
                fixingDays,
                currency,
                IndexCalendar(f.FixingCalendar, name),
                IndexCalendar(f.ValueCalendar, name, "ValueCalendar"),
                IndexCalendar(f.MaturityCalendar, name, "MaturityCalendar"),
                f.Convention ?? throw Missing("index", name, "Convention"),
                f.EndOfMonth ?? throw Missing("index", name, "EndOfMonth"),
                dayCounter,
                h),
            _ => new IborIndex(
                f.Family ?? name,
                tenor,
                fixingDays,
                currency,
                IndexCalendar(f.FixingCalendar, name),
                f.Convention ?? throw Missing("index", name, "Convention"),
                f.EndOfMonth ?? throw Missing("index", name, "EndOfMonth"),
                dayCounter,
                h),
        };
    }

    // A calendar a user's own index names.
    private Calendar IndexCalendar(string? calendar, string index, string field = "FixingCalendar") =>
        CalendarOf(calendar ?? throw Missing("index", index, field), layers.Length, [], "snapshot");

    private IborIndex FamilyIndexOf(string name, Period tenor, HashSet<string> visiting, string paramName, YieldTermStructure? h)
    {
        Entry? e = Top(ConventionKind.Index, name, layers.Length);
        if (e?.Record is AliasRecord alias)
        {
            return FamilyIndexOf(alias.Base!, tenor, visiting, paramName, h);
        }

        if (PresetFactories.IborFamilies.TryGetValue(name, out Func<Period, YieldTermStructure?, IborIndex>? preset))
        {
            return preset(tenor, h);
        }

        if (e?.Record is not IborFamilyRecord)
        {
            throw e is null ? Unknown("Ibor family", name, paramName) : new ArgumentException($"{name} is no Ibor family", paramName);
        }

        IborFields f = FamilyFieldsAt(name, tenor, layers.Length, visiting)!;
        Currency currency = CurrencyOf(f.Currency ?? throw Missing("family", name, "Currency"), [], "snapshot");
        DayCounter dayCounter = DayCounterOf(f.DayCounter ?? throw Missing("family", name, "DayCounter"), layers.Length, [], "snapshot");
        int fixingDays = f.FixingDays ?? throw Missing("family", name, "FixingDays");
        if (f.FinancialCenterCalendar is not string center)
        {
            return new IborIndex(f.Family ?? name, tenor, fixingDays, currency, IndexCalendar(f.FixingCalendar, name), f.Convention!.Value, f.EndOfMonth!.Value, dayCounter, h);
        }

        // a Libor family's convention and end of month are Libor's own rule by the tenor's unit, which its units keep
        var libor = new Libor(f.Family ?? name, tenor, fixingDays, currency, CalendarOf(center, layers.Length, [], "snapshot"), dayCounter, h);
        return libor.BusinessDayConvention == f.Convention && libor.EndOfMonth == f.EndOfMonth
            ? libor
            : throw new ArgumentException(
                $"the Libor family {name}'s unit {tenor.Units} gives {f.Convention}, end of month {f.EndOfMonth}, where Libor's rule is {libor.BusinessDayConvention}, {libor.EndOfMonth}",
                "snapshot");
    }

    private SwapIndex SwapIndexOf(
        string name, Period tenor, HashSet<string> visiting, string paramName, YieldTermStructure? forwarding, bool exogenous, YieldTermStructure? discounting)
    {
        Entry? e = Top(ConventionKind.Index, name, layers.Length);
        if (e?.Record is AliasRecord alias)
        {
            return SwapIndexOf(alias.Base!, tenor, visiting, paramName, forwarding, exogenous, discounting);
        }

        if (PresetFactories.SwapIndexes.TryGetValue(name, out Func<Period, YieldTermStructure?, bool, YieldTermStructure?, SwapIndex>? preset))
        {
            return preset(tenor, forwarding, exogenous, discounting);
        }

        SwapIndexRecord s = (e is null ? null : SwapFieldsOf(name, layers.Length, visiting)) ?? throw Unknown("swap index", name, paramName);
        bool isShort = !(tenor > new Period(1, TimeUnit.Years));
        (string? ibor, Period? iborTenor) = isShort && s.ShortIborIndex is not null ? (s.ShortIborIndex, s.ShortIborIndexTenor) : (s.IborIndex, s.IborIndexTenor);
        string iborName = ibor ?? throw Missing("swap index", name, "IborIndex");
        string family = s.Family ?? name;
        int settlementDays = s.SettlementDays ?? throw Missing("swap index", name, "SettlementDays");
        Currency currency = CurrencyOf(s.Currency ?? throw Missing("swap index", name, "Currency"), [], "snapshot");
        Calendar fixingCalendar = CalendarOf(s.FixingCalendar ?? throw Missing("swap index", name, "FixingCalendar"), layers.Length, [], "snapshot");
        Period fixedLegTenor = (isShort ? s.ShortFixedLegTenor : null) ?? s.FixedLegTenor ?? throw Missing("swap index", name, "FixedLegTenor");
        BusinessDayConvention fixedLegConvention = s.FixedLegConvention ?? throw Missing("swap index", name, "FixedLegConvention");
        DayCounter fixedLegDayCounter = DayCounterOf(s.FixedLegDayCounter ?? throw Missing("swap index", name, "FixedLegDayCounter"), layers.Length, [], "snapshot");
        IborIndex iborIndex = iborTenor is Period t
            ? FamilyIndexOf(iborName, t, [], "snapshot", forwarding)
            : IborIndexOf(iborName, [], "snapshot", forwarding);
        return exogenous
            ? new SwapIndex(family, tenor, settlementDays, currency, fixingCalendar, fixedLegTenor, fixedLegConvention, fixedLegDayCounter, iborIndex, discounting)
            : new SwapIndex(family, tenor, settlementDays, currency, fixingCalendar, fixedLegTenor, fixedLegConvention, fixedLegDayCounter, iborIndex);
    }

    private OvernightIndexedSwapIndex OisIndexOf(string name, Period tenor, HashSet<string> visiting, string paramName, YieldTermStructure? h)
    {
        OvernightIndexedSwapIndexRecord o = OisFieldsOf(name, layers.Length, visiting) ?? throw Unknown("overnight-indexed swap index", name, paramName);
        string overnight = o.OvernightIndex ?? throw Missing("overnight-indexed swap index", name, "OvernightIndex");
        return new OvernightIndexedSwapIndex(
            o.Family ?? name,
            tenor,
            o.SettlementDays ?? throw Missing("overnight-indexed swap index", name, "SettlementDays"),
            CurrencyOf(o.Currency ?? throw Missing("overnight-indexed swap index", name, "Currency"), [], "snapshot"),
            IborIndexOf(overnight, [], "snapshot", h) as OvernightIndex
                ?? throw new ArgumentException($"the overnight-indexed swap index {name}'s index {overnight} is no overnight index", "snapshot"),
            o.TelescopicValueDates ?? false,
            o.Averaging ?? RateAveragingType.Compound);
    }

    private ZeroInflationIndex ZeroIndexOf(string name, HashSet<string> visiting, string paramName, ZeroInflationTermStructure? ts)
    {
        if (Top(ConventionKind.Index, name, layers.Length)?.Record is AliasRecord alias)
        {
            return ZeroIndexOf(alias.Base!, visiting, paramName, ts);
        }

        if (PresetFactories.ZeroInflationIndexes.TryGetValue(name, out Func<ZeroInflationTermStructure?, ZeroInflationIndex>? preset))
        {
            return preset(ts);
        }

        if (PresetFactories.ZeroInflationIndexesWithArguments.ContainsKey(name))
        {
            throw new ArgumentException($"the {name} preset takes its frequency and revised flag: name a record based on it that gives them", paramName);
        }

        YoYInflationIndexRecord z = InflationFieldsOf(name, layers.Length, visiting) ?? throw Unknown("zero inflation index", name, paramName);
        if (Top(ConventionKind.Index, name, layers.Length)?.Record is not ZeroInflationIndexRecord)
        {
            throw new ArgumentException($"{name} is no zero inflation index", paramName);
        }

        if (ArgumentBaseOf(name, []) is string withArguments)
        {
            return PresetFactories.ZeroInflationIndexesWithArguments[withArguments](
                z.Frequency ?? throw Missing("zero inflation index", name, "Frequency"), z.Revised ?? throw Missing("zero inflation index", name, "Revised"), ts);
        }

        return new ZeroInflationIndex(
            z.Family ?? name,
            RegionOf(z.Region ?? throw Missing("zero inflation index", name, "Region"), [], "snapshot"),
            z.Revised ?? throw Missing("zero inflation index", name, "Revised"),
            z.Frequency ?? throw Missing("zero inflation index", name, "Frequency"),
            z.AvailabilityLag ?? throw Missing("zero inflation index", name, "AvailabilityLag"),
            CurrencyOf(z.Currency ?? throw Missing("zero inflation index", name, "Currency"), [], "snapshot"),
            ts);
    }

    private YoYInflationIndex YoYIndexOf(string name, HashSet<string> visiting, string paramName, YoYInflationTermStructure? ts)
    {
        if (Top(ConventionKind.Index, name, layers.Length)?.Record is AliasRecord alias)
        {
            return YoYIndexOf(alias.Base!, visiting, paramName, ts);
        }

        if (PresetFactories.YoYInflationIndexes.TryGetValue(name, out Func<YoYInflationTermStructure?, YoYInflationIndex>? preset))
        {
            return preset(ts);
        }

        if (PresetFactories.YoYInflationIndexesWithArguments.ContainsKey(name))
        {
            throw new ArgumentException($"the {name} preset takes its frequency and revised flag: name a record based on it that gives them", paramName);
        }

        YoYInflationIndexRecord y = InflationFieldsOf(name, layers.Length, visiting) ?? throw Unknown("year-on-year inflation index", name, paramName);
        if (Top(ConventionKind.Index, name, layers.Length)?.Record is not YoYInflationIndexRecord)
        {
            throw new ArgumentException($"{name} is no year-on-year inflation index", paramName);
        }

        if (ArgumentBaseOf(name, []) is string withArguments)
        {
            return PresetFactories.YoYInflationIndexesWithArguments[withArguments](
                y.Frequency ?? throw Missing("year-on-year inflation index", name, "Frequency"), y.Revised ?? throw Missing("year-on-year inflation index", name, "Revised"), ts);
        }

        if (y.UnderlyingIndex is string underlying)
        {
            return new YoYInflationIndex(ZeroIndexOf(underlying, [], "snapshot", null), ts);
        }

        return new YoYInflationIndex(
            y.Family ?? name,
            RegionOf(y.Region ?? throw Missing("year-on-year inflation index", name, "Region"), [], "snapshot"),
            y.Revised ?? throw Missing("year-on-year inflation index", name, "Revised"),
            y.Frequency ?? throw Missing("year-on-year inflation index", name, "Frequency"),
            y.AvailabilityLag ?? throw Missing("year-on-year inflation index", name, "AvailabilityLag"),
            CurrencyOf(y.Currency ?? throw Missing("year-on-year inflation index", name, "Currency"), [], "snapshot"),
            ts);
    }

    // Every index record of a new set resolves: an index as it is looked up, a family at a tenor of each unit it takes,
    // a swap index at a year and at ten, a preset taking arguments at QuantLib's first ones.
    private void ValidateIndex(string name)
    {
        switch (Top(ConventionKind.Index, name, layers.Length)?.Record)
        {
            case IborIndexRecord when PresetFactories.IborIndexesWithSettlementDays.TryGetValue(name, out Func<int, YieldTermStructure?, IborIndex>? daily):
                _ = daily(2, null);
                break;
            case IborIndexRecord when PresetFactories.OtherIndexes.TryGetValue(name, out Func<YieldTermStructure?, InterestRateIndex>? other):
                _ = other(null);
                break;
            case IborIndexRecord or OvernightIndexRecord:
                _ = IborIndexOf(name, [], "snapshot", null);
                break;
            case IborFamilyRecord:
                foreach (IborFamilyUnit u in FamilyRecordOf(name, layers.Length, [])!.Units)
                {
                    _ = FamilyIndexOf(name, new Period(1, u.TimeUnit), [], "snapshot", null);
                }

                break;
            case SwapIndexRecord:
                _ = SwapIndexOf(name, new Period(1, TimeUnit.Years), [], "snapshot", null, false, null);
                _ = SwapIndexOf(name, new Period(10, TimeUnit.Years), [], "snapshot", null, false, null);
                break;
            case OvernightIndexedSwapIndexRecord:
                _ = OisIndexOf(name, new Period(10, TimeUnit.Years), [], "snapshot", null);
                break;
            case ZeroInflationIndexRecord when PresetFactories.ZeroInflationIndexesWithArguments.TryGetValue(name, out var zero):
                _ = zero(Frequency.Monthly, false, null);
                break;
            case ZeroInflationIndexRecord:
                _ = ZeroIndexOf(name, [], "snapshot", null);
                break;
            case YoYInflationIndexRecord when PresetFactories.YoYInflationIndexesWithArguments.TryGetValue(name, out var yoy):
                _ = yoy(Frequency.Monthly, false, null);
                break;
            case YoYInflationIndexRecord:
                _ = YoYIndexOf(name, [], "snapshot", null);
                break;
        }
    }

    // A coded index preset's name keeps its class (CVD28), and a preset's arguments are its constructor's.
    private void CheckIndex(ConventionRecord r, bool shippedLayer, Action<string> refuse)
    {
        if (CodedIndexKind(r.Name) is Type kind)
        {
            if (r.GetType() != kind)
            {
                refuse($"the name is a coded preset's, which takes a {kind.Name[..^"Record".Length]} record");
            }

            // a record of a coded preset takes its own name, or the preset QuantLib's records base it on, as its base
            if (r.Base is string recordBase && recordBase != r.Name
                && !(PresetFactories.PresetBases.TryGetValue(r.Name, out string? quantLibs) && quantLibs == recordBase))
            {
                refuse("a preset record takes only its own name, or the preset QuantLib's record bases it on, as its base");
            }

            switch (r)
            {
                case IborIndexRecord { Style: not null }:
                    refuse("a preset takes no style");
                    break;
                case IborIndexRecord { FixingDays: not null } when PresetFactories.IborIndexesWithSettlementDays.ContainsKey(r.Name):
                    refuse("its settlement days are its constructor's argument");
                    break;
                case IborIndexRecord b when PresetFactories.OtherIndexes.ContainsKey(r.Name)
                    && (b.Convention is not null || b.EndOfMonth is not null || b.FinancialCenterCalendar is not null
                        || b.ValueCalendar is not null || b.MaturityCalendar is not null || (b.Tenor is Period t && t != new Period(1, TimeUnit.Weeks))):
                    refuse("the BMA index takes no convention, end of month or calendar but its fixing calendar, and its tenor is a week");
                    break;
                case IborIndexRecord { Tenor: Period t } when FixedPresetTenor(r.Name) is Period own && t != own:
                    refuse($"its tenor is its class's, {own}");
                    break;
                case ZeroInflationIndexRecord z when PresetFactories.ZeroInflationIndexesWithArguments.ContainsKey(r.Name) && (z.Frequency is not null || z.Revised is not null):
                    refuse("its frequency and revised flag are its constructor's arguments");
                    break;
                case YoYInflationIndexRecord y when PresetFactories.YoYInflationIndexesWithArguments.ContainsKey(r.Name) && (y.Frequency is not null || y.Revised is not null):
                    refuse("its frequency and revised flag are its constructor's arguments");
                    break;
                case YoYInflationIndexRecord { UnderlyingIndex: not null }:
                    refuse("a quoted preset takes no underlying index");
                    break;
            }

            return;
        }

        // a user's own Ibor index takes the fields its style builds, and no other (CVD38)
        if (r is IborIndexRecord && IborFieldsOf(r.Name, layers.Length, []) is { } f && ArgumentBaseOf(r.Name, []) is null)
        {
            string? ignored = (f.Style ?? IborIndexStyle.Ibor) switch
            {
                IborIndexStyle.Libor when f.FixingCalendar is not null || f.Convention is not null || f.EndOfMonth is not null
                    || f.ValueCalendar is not null || f.MaturityCalendar is not null =>
                    "a Libor's fixing calendar, convention and end of month are Libor's own, and it has no value or maturity calendar",
                IborIndexStyle.Custom when f.FinancialCenterCalendar is not null => "a custom index has no financial-centre calendar",
                IborIndexStyle.Ibor when f.FinancialCenterCalendar is not null || f.ValueCalendar is not null || f.MaturityCalendar is not null =>
                    "an Ibor-style index has no financial-centre, value or maturity calendar",
                _ => null,
            };
            if (ignored is not null)
            {
                refuse(ignored);
            }
        }

        // a record of a user's own based on a preset taking arguments gives only them
        if (ArgumentBaseOf(r.Name, []) is string withArguments)
        {
            bool onlyArguments = r switch
            {
                IborIndexRecord i => i with { FixingDays = null } == new IborIndexRecord(i.Name, i.Base) { Foreign = i.Foreign },
                ZeroInflationIndexRecord z => z with { Frequency = null, Revised = null } == new ZeroInflationIndexRecord(z.Name, z.Base) { Foreign = z.Foreign },
                YoYInflationIndexRecord y => y with { Frequency = null, Revised = null } == new YoYInflationIndexRecord(y.Name, y.Base) { Foreign = y.Foreign },
                _ => false,
            };
            if (!onlyArguments)
            {
                refuse($"a record based on {withArguments} gives only its constructor's arguments");
            }
        }
    }

    // An Ibor or overnight index's fields, merged down its records' chain.
    private sealed record IborFields(
        string? Family,
        Period? Tenor,
        int? FixingDays,
        string? Currency,
        string? FixingCalendar,
        BusinessDayConvention? Convention,
        bool? EndOfMonth,
        string? DayCounter,
        IborIndexStyle? Style,
        string? FinancialCenterCalendar,
        string? ValueCalendar,
        string? MaturityCalendar)
    {
        // These fields, each left out the lower record's.
        public IborFields Over(IborFields? lower) => lower is null ? this : new(
            Family ?? lower.Family,
            Tenor ?? lower.Tenor,
            FixingDays ?? lower.FixingDays,
            Currency ?? lower.Currency,
            FixingCalendar ?? lower.FixingCalendar,
            Convention ?? lower.Convention,
            EndOfMonth ?? lower.EndOfMonth,
            DayCounter ?? lower.DayCounter,
            Style ?? lower.Style,
            FinancialCenterCalendar ?? lower.FinancialCenterCalendar,
            ValueCalendar ?? lower.ValueCalendar,
            MaturityCalendar ?? lower.MaturityCalendar);
    }

    private readonly record struct Entry(int Layer, ConventionRecord Record, string? Source);
}
