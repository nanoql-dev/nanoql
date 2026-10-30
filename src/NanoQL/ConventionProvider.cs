// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
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
/// builder takes the provider of its index, and builds inside it. An object that builds a builder inside passes its
/// own provider to it: a swap index's underlying swap takes the swap index's provider, even on an Ibor index built
/// under another, so its defaults are the swap index's provider's and its floating leg's conventions the Ibor index's.
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
/// values it configures. Each member takes the preset's name and its reference and returns the values to use, and the
/// base class returns the reference. Every member is called inside the provider's own scope.
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
            // QuantLib's data is built once, when the provider asks for it or the identity check needs it
            CurrencyData? built = null;
            CurrencyData QuantLibs() => built ??= reference();
            CurrencyData data = provider.ResolveCurrency(code, QuantLibs)
                ?? throw new NanoQLException($"no data for the {code} currency");
            CurrencyData quantLibs = QuantLibs();

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
