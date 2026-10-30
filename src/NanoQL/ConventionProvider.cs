// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// The market conventions the library's presets and market builders read when they are built: QuantLib's coded values,
/// the system's configured defaults, or a provider of a user's own.
/// </summary>
/// <remarks>
/// <para>
/// The provider follows <see cref="System.Globalization.CultureInfo"/>'s shape. <see cref="QuantLib"/> returns
/// QuantLib's coded values unchanged; <see cref="Default"/> is the system's defaults, <see cref="QuantLib"/> until set;
/// <see cref="Current"/> is the innermost active scope's provider, opened by <see cref="Use"/>, or else
/// <see cref="Default"/>. A scope flows with asynchronous calls, as a culture does, so two flows can build under two
/// providers side by side. A provider is immutable: setting <see cref="Default"/> replaces it whole.
/// </para>
/// <para>
/// A preset reads <see cref="Current"/> once, in its constructor, and passes its QuantLib values, its reference, to the
/// provider, which returns the values to use: the reference unchanged when nothing is configured. A preset's nested
/// presets (an index's calendar, a currency's triangulation currency) are built inside the same provider's scope, so a
/// construction takes all its values from one provider.
/// </para>
/// <para>
/// The provider is read only when an object is built, never while it prices, so a price doesn't depend on the scope
/// it runs in. An index keeps the provider it was built with, and every clone of it keeps the same one. A market
/// builder takes the provider of its index, reads its defaults for the options it isn't given from it when it builds,
/// and builds inside it; the CDS builder, with no index, takes the provider current when it is created. An object that
/// builds a builder inside passes its own provider to it: a swap index's underlying swap takes the swap index's
/// provider, even on an Ibor index built under another, so its defaults are the swap index's provider's and its
/// floating leg's conventions the Ibor index's.
/// Whatever is built on an index after its construction follows the index: a swap index's underlying swap, a rate
/// helper's swap when a bootstrap re-dates it, an engine's auxiliary swaps. An object that builds after its
/// construction with no index to follow keeps <see cref="Current"/> from its construction. QuantLib's registry
/// defaults (<see cref="Currencies.ExchangeRates.Default"/>, <see cref="Experimental.UnitOfMeasureConversions.Default"/>
/// and <see cref="EvaluationContext"/>'s defaults) are built under <see cref="QuantLib"/>, whatever is current when
/// they are first read.
/// </para>
/// <para>
/// So a change of <see cref="Default"/> reaches the objects built after it, and everything built on them. Objects built
/// before it keep their conventions, and an object built on an index takes that index's provider, so set
/// <see cref="Default"/> before building indexes, and rebuild them to take a change, as curves are rebuilt.
/// </para>
/// <para>
/// A provider of a user's own (a database's, a service's) derives from this class and overrides the members whose
/// values it configures. Each member takes the preset's or the builder's name and its reference and returns the values
/// to use, and the base class returns the reference. Every member is called inside the provider's own scope.
/// </para>
/// </remarks>
public abstract class ConventionProvider
{
    // The innermost scope's provider on this asynchronous flow, null outside any scope.
    private static readonly AsyncLocal<ConventionProvider?> Scope = new();

    // Null until set, standing for QuantLib's; written and read as one reference.
    private static ConventionProvider? defaultProvider;

    /// <summary>Initializes a new instance of the <see cref="ConventionProvider"/> class.</summary>
    protected ConventionProvider()
    {
    }

    /// <summary>Gets QuantLib's coded conventions: every member returns its reference unchanged.</summary>
    public static ConventionProvider QuantLib { get; } = new QuantLibConventions();

    /// <summary>
    /// Gets or sets the system's default conventions, which <see cref="Current"/> returns outside any scope;
    /// <see cref="QuantLib"/> until set, and setting <see langword="null"/> restores it.
    /// </summary>
    /// <remarks>
    /// The provider is replaced whole and atomically, so a construction reading it while another thread sets it takes
    /// one provider or the other. Objects built before the change keep their conventions.
    /// </remarks>
    [AllowNull]
    public static ConventionProvider Default
    {
        get => Volatile.Read(ref defaultProvider) ?? QuantLib;
        set => Volatile.Write(ref defaultProvider, value);
    }

    /// <summary>
    /// Gets the conventions a construction reads now: the innermost scope's provider on this asynchronous flow, or
    /// <see cref="Default"/> outside any scope.
    /// </summary>
    public static ConventionProvider Current => Scope.Value ?? Default;

    /// <summary>
    /// Opens a scope in which <see cref="Current"/> is the given provider on this asynchronous flow, until the returned
    /// object is disposed.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The scope, which restores the previous one when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The scope flows with asynchronous calls started inside it, and a flow started outside it doesn't see it. Scopes
    /// nest: disposing one restores the scope that was active when it was opened.
    /// </remarks>
    public static IDisposable Use(ConventionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ConventionProvider? previous = Scope.Value;
        if (ReferenceEquals(previous, provider))
        {
            return Unchanged.Instance;
        }

        Scope.Value = provider;
        return new Restore(previous);
    }

    /// <summary>The holiday edits of a calendar preset.</summary>
    /// <param name="preset">The preset's name, its type and market (<c>TARGET</c>, <c>UnitedStates.NYSE</c>).</param>
    /// <param name="reference">QuantLib's edits, none.</param>
    /// <returns>The edits to apply to the preset's holiday rules.</returns>
    public virtual CalendarEdits ResolveCalendarEdits(string preset, CalendarEdits reference) => reference;

    /// <summary>The data of a currency preset.</summary>
    /// <param name="code">The currency's ISO 4217 code.</param>
    /// <param name="reference">Builds QuantLib's data.</param>
    /// <returns>
    /// The currency's data. Its ISO code, numeric code and name are its identity, which its equality and the exchange-rate
    /// registries read, so they are QuantLib's; the preset refuses data that changes them. The symbols, the fractions,
    /// the rounding and the triangulation currency are the provider's to configure.
    /// </returns>
    public virtual CurrencyData ResolveCurrency(string code, Func<CurrencyData> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The calendar of a <see cref="Business252"/> day counter built without one.</summary>
    /// <param name="reference">Builds QuantLib's, the Brazilian settlement calendar.</param>
    /// <returns>The calendar whose business days are counted.</returns>
    public virtual Calendar ResolveBusiness252Calendar(Func<Calendar> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The conventions of an index preset its class names: a daily-tenor Libor, or the BMA index.</summary>
    /// <param name="preset">The preset's name, its class's (<c>DailyTenorUSDLibor</c>, <c>USDLiborON</c>,
    /// <c>BMAIndex</c>).</param>
    /// <param name="reference">Builds QuantLib's conventions, a daily-tenor Libor's settlement days its argument.</param>
    /// <returns>The conventions the preset is built on.</returns>
    public virtual IborIndexConventions ResolveIborIndex(string preset, Func<IborIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The conventions of an Ibor or Libor family at a tenor, its fixed-tenor presets' among them.</summary>
    /// <param name="family">The family's class name (<c>Euribor</c>, <c>USDLibor</c>).</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="reference">Builds QuantLib's conventions at the tenor.</param>
    /// <returns>The conventions the index is built on.</returns>
    /// <remarks>A fixed-tenor preset (<see cref="Euribor6M"/>) is its family at its tenor, so the two read one
    /// member.</remarks>
    public virtual IborIndexConventions ResolveIborFamily(string family, Period tenor, Func<IborIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The conventions of an overnight index preset.</summary>
    /// <param name="preset">The preset's class name (<c>Sofr</c>).</param>
    /// <param name="reference">Builds QuantLib's conventions.</param>
    /// <returns>The conventions the index is built on: an overnight index's convention is Following, without end of
    /// month, which the preset refuses to change.</returns>
    public virtual IborIndexConventions ResolveOvernightIndex(string preset, Func<IborIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The conventions of a swap index family at a tenor.</summary>
    /// <param name="family">The family's class name (<c>EuriborSwapIsdaFixA</c>).</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="reference">Builds QuantLib's conventions at the tenor, the Ibor index on the swap index's
    /// forwarding curve.</param>
    /// <returns>The conventions the index is built on.</returns>
    public virtual SwapIndexConventions ResolveSwapIndex(string family, Period tenor, Func<SwapIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The conventions of a zero or quoted year-on-year inflation index preset.</summary>
    /// <param name="preset">The preset's class name (<c>EUHICP</c>, <c>YYEUHICP</c>).</param>
    /// <param name="reference">Builds QuantLib's conventions, with the preset's arguments.</param>
    /// <returns>The conventions the index is built on.</returns>
    public virtual InflationIndexConventions ResolveInflationIndex(string preset, Func<InflationIndexConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The data of a region preset.</summary>
    /// <param name="preset">The preset's class name (<c>EURegion</c>).</param>
    /// <param name="reference">Builds QuantLib's data.</param>
    /// <returns>The region's data. Its name is its identity, which its equality and its inflation indexes' names read,
    /// so it is QuantLib's, and the preset refuses data that changes it; the code is the provider's to configure.</returns>
    public virtual RegionData ResolveRegion(string preset, Func<RegionData> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The data of a unit of measure preset.</summary>
    /// <param name="preset">The preset's class name (<c>BarrelUnitOfMeasure</c>).</param>
    /// <param name="reference">Builds QuantLib's data.</param>
    /// <returns>The unit's data. Its code is its identity, which its equality and the conversions' lookups read, so it
    /// is QuantLib's, and the preset refuses data that changes it; the name, the type and the triangulation unit are
    /// the provider's to configure.</returns>
    [Experimental("NQL0001")]
    public virtual UnitOfMeasureData ResolveUnitOfMeasure(string preset, Func<UnitOfMeasureData> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeVanillaSwap"/> takes on an index of a currency.</summary>
    /// <param name="currency">The index's currency, or <see langword="null"/> for none.</param>
    /// <param name="reference">Builds QuantLib's defaults for the currency.</param>
    /// <returns>The defaults; a fixed leg's tenor or day counter left <see langword="null"/> is unknown for the
    /// currency, which the builder refuses when it needs one. A threshold is compared with the swap's tenor, or, with
    /// a termination date given, with QuantLib's approximate months of the swap's length, (12 days + 182) / 365, when
    /// the builder needs its fixed leg's tenor.</returns>
    public virtual VanillaSwapDefaults ResolveVanillaSwapDefaults(Currency? currency, Func<VanillaSwapDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeOIS"/> takes on an overnight index.</summary>
    /// <param name="index">The index's class name (<c>Sonia</c>, <c>OvernightIndex</c>), by which QuantLib chooses the
    /// settlement days.</param>
    /// <param name="reference">Builds QuantLib's defaults for the class.</param>
    /// <returns>The defaults.</returns>
    public virtual OisDefaults ResolveOisDefaults(string index, Func<OisDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeCreditDefaultSwap"/> takes.</summary>
    /// <param name="reference">Builds QuantLib's defaults.</param>
    /// <returns>The defaults.</returns>
    public virtual CdsDefaults ResolveCdsDefaults(Func<CdsDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeCapFloor"/> takes.</summary>
    /// <param name="reference">Builds QuantLib's defaults.</param>
    /// <returns>The defaults.</returns>
    public virtual CapFloorDefaults ResolveCapFloorDefaults(Func<CapFloorDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeSwaption"/> takes.</summary>
    /// <param name="reference">Builds QuantLib's defaults.</param>
    /// <returns>The defaults.</returns>
    public virtual SwaptionDefaults ResolveSwaptionDefaults(Func<SwaptionDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeCms"/> takes.</summary>
    /// <param name="reference">Builds QuantLib's defaults.</param>
    /// <returns>The defaults.</returns>
    public virtual CmsDefaults ResolveCmsDefaults(Func<CmsDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeMultipleResetsSwap"/> takes.</summary>
    /// <param name="reference">Builds QuantLib's defaults.</param>
    /// <returns>The defaults.</returns>
    public virtual MultipleResetsSwapDefaults ResolveMultipleResetsSwapDefaults(Func<MultipleResetsSwapDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The defaults a <see cref="MakeYoYInflationCapFloor"/> takes.</summary>
    /// <param name="reference">Builds QuantLib's defaults.</param>
    /// <returns>The defaults.</returns>
    public virtual YoYInflationCapFloorDefaults ResolveYoYInflationCapFloorDefaults(Func<YoYInflationCapFloorDefaults> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    /// <summary>The conventions of a fixed-rate bond preset.</summary>
    /// <param name="preset">The preset's class name (<c>BTP</c>).</param>
    /// <param name="reference">Builds QuantLib's conventions.</param>
    /// <returns>The conventions the bond is built on; its terms are its constructor's arguments.</returns>
    public virtual BondConventions ResolveBond(string preset, Func<BondConventions> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference();
    }

    // The presets' reads, each of Current once, inside its scope.
    internal static CalendarEdits ReadCalendarEdits(string preset)
    {
        ConventionProvider provider = Current;
        using (Use(provider))
        {
            return provider.ResolveCalendarEdits(preset, CalendarEdits.None)
                ?? throw new NanoQLException($"no holiday edits for the {preset} calendar");
        }
    }

    internal static CurrencyData ReadCurrency(string code, Func<CurrencyData> reference)
    {
        ConventionProvider provider = Current;
        using (Use(provider))
        {
            // QuantLib's data is built in the provider's scope when the provider asks for it; the identity check reads
            // that, or else data built under QuantLib's conventions, so it builds no preset through the provider
            CurrencyData? built = null;
            CurrencyData QuantLibs() => built ??= reference();
            CurrencyData data = provider.ResolveCurrency(code, QuantLibs)
                ?? throw new NanoQLException($"no data for the {code} currency");
            CurrencyData quantLibs = built ?? Identity(reference);

            // the codes and the name are the currency's identity, which its equality and the registries read
            return data.Code == code && data.Name == quantLibs.Name && data.NumericCode == quantLibs.NumericCode
                ? data
                : throw new NanoQLException(
                    $"the data for the {code} currency changes its identity: {data.Code}, {data.NumericCode}, "
                    + $"\"{data.Name}\" for QuantLib's {code}, {quantLibs.NumericCode}, \"{quantLibs.Name}\"");
        }
    }

    internal static Calendar ReadBusiness252Calendar(Func<Calendar> reference)
    {
        ConventionProvider provider = Current;
        using (Use(provider))
        {
            return provider.ResolveBusiness252Calendar(reference)
                ?? throw new NanoQLException("no calendar for the Business/252 day counter");
        }
    }

    // An index preset's conventions with the provider they came from, which the index keeps.
    internal static (IborIndexConventions Values, ConventionProvider Provider) ReadIborIndex(
        string preset, Func<IborIndexConventions> reference) =>
        Read(p => p.ResolveIborIndex(preset, reference), $"the {preset} index");

    internal static (IborIndexConventions Values, ConventionProvider Provider) ReadIborFamily(
        string family, Period tenor, Func<IborIndexConventions> reference) =>
        Read(p => p.ResolveIborFamily(family, tenor, reference), $"the {family} family at {tenor}");

    internal static (IborIndexConventions Values, ConventionProvider Provider) ReadOvernightIndex(
        string preset, Func<IborIndexConventions> reference)
    {
        (IborIndexConventions values, ConventionProvider provider) = Read(p => p.ResolveOvernightIndex(preset, reference), $"the {preset} index");

        // QuantLib's overnight index fixes both, so a provider's other values would be ignored
        return values.Convention == BusinessDayConvention.Following && !values.EndOfMonth
            ? (values, provider)
            : throw new NanoQLException(
                $"the {preset} overnight index's convention is Following without end of month, not {values.Convention}"
                + (values.EndOfMonth ? " with end of month" : ""));
    }

    internal static (SwapIndexConventions Values, ConventionProvider Provider) ReadSwapIndex(
        string family, Period tenor, Func<SwapIndexConventions> reference) =>
        Read(p => p.ResolveSwapIndex(family, tenor, reference), $"the {family} family at {tenor}");

    internal static (InflationIndexConventions Values, ConventionProvider Provider) ReadInflationIndex(
        string preset, Func<InflationIndexConventions> reference) =>
        Read(p => p.ResolveInflationIndex(preset, reference), $"the {preset} index");

    internal static RegionData ReadRegion(string preset, Func<RegionData> reference)
    {
        ConventionProvider provider = Current;
        using (Use(provider))
        {
            RegionData? built = null;
            RegionData QuantLibs() => built ??= reference();
            RegionData data = provider.ResolveRegion(preset, QuantLibs)
                ?? throw new NanoQLException($"no data for the {preset} region");

            // the name is the region's identity, which its equality and its indexes' names read
            return data.Name == QuantLibs().Name
                ? data
                : throw new NanoQLException($"the data for the {preset} region changes its name: \"{data.Name}\" for QuantLib's \"{QuantLibs().Name}\"");
        }
    }

    internal static UnitOfMeasureData ReadUnitOfMeasure(string preset, Func<UnitOfMeasureData> reference)
    {
        ConventionProvider provider = Current;
        using (Use(provider))
        {
            // QuantLib's data is built in the provider's scope when the provider asks for it; the identity check reads
            // that, or else data built under QuantLib's conventions, so it builds no preset through the provider
            UnitOfMeasureData? built = null;
            UnitOfMeasureData QuantLibs() => built ??= reference();
            UnitOfMeasureData data = provider.ResolveUnitOfMeasure(preset, QuantLibs)
                ?? throw new NanoQLException($"no data for the {preset} unit of measure");
            string code = (built ?? Identity(reference)).Code;

            // the code is the unit's identity, which its equality and the conversions' lookups read
            return data.Code == code
                ? data
                : throw new NanoQLException($"the data for the {preset} unit of measure changes its code: {data.Code} for QuantLib's {code}");
        }
    }

    // A preset's QuantLib data for its identity check, built under QuantLib's conventions: its nested presets (a
    // triangulation unit or currency) read no provider.
    private static T Identity<T>(Func<T> reference)
    {
        using (Use(QuantLib))
        {
            return reference();
        }
    }

    private static (T Values, ConventionProvider Provider) Read<T>(Func<ConventionProvider, T?> resolve, string what)
        where T : class
    {
        ConventionProvider provider = Current;
        using (Use(provider))
        {
            return (resolve(provider) ?? throw new NanoQLException($"no conventions for {what}"), provider);
        }
    }

    // A market builder's defaults from its provider, read inside the provider's scope when the builder builds.
    internal static T ReadDefaults<T>(ConventionProvider provider, Func<ConventionProvider, T?> resolve, string what)
        where T : class
    {
        using (Use(provider))
        {
            return resolve(provider) ?? throw new NanoQLException($"no defaults for {what}");
        }
    }

    internal static BondConventions ReadBond(string preset, Func<BondConventions> reference)
    {
        ConventionProvider provider = Current;
        using (Use(provider))
        {
            return provider.ResolveBond(preset, reference) ?? throw new NanoQLException($"no conventions for the {preset} bond");
        }
    }

    // A registry default, built under QuantLib's conventions whatever is current when its type initializes.
    internal static T BuildUnderQuantLib<T>(Func<T> build)
    {
        using (Use(QuantLib))
        {
            return build();
        }
    }

    // QuantLib's coded conventions: the base class's references.
    private sealed class QuantLibConventions : ConventionProvider
    {
    }

    // A scope that was already the provider's: nothing to restore.
    private sealed class Unchanged : IDisposable
    {
        public static readonly Unchanged Instance = new();

        public void Dispose()
        {
        }
    }

    // Restores the scope active when it was opened, once.
    private sealed class Restore(ConventionProvider? previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Scope.Value = previous;
            }
        }
    }
}
