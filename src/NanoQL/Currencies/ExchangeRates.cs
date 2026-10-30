// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/exchangeratemanager.hpp ql/currencies/exchangeratemanager.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Equality-tested: Conventions/ExchangeRatesValueTests.cs

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Currencies;

/// <summary>
/// The exchange-rate registry: QuantLib's <c>ExchangeRateManager</c>, as an immutable value that
/// <see cref="EvaluationContext.ExchangeRates"/> holds.
/// </summary>
/// <remarks>
/// <para>
/// Every change returns a new registry, as .NET's immutable collections do. <see cref="Default"/> holds QuantLib's
/// known rates, the euro's legacy currencies and the redenominated Turkish, Romanian and Peruvian currencies, which
/// QuantLib's manager holds at start and after <c>clear()</c>. <see cref="Add"/> puts a rate first among its pair's
/// entries, so the latest added takes precedence where date ranges overlap; <see cref="Lookup"/> finds a direct,
/// triangulated or chained rate in QuantLib's order.
/// </para>
/// <para>
/// QuantLib keys its rates by the pair's numeric codes, <c>min × 1000 + max</c> (<c>exchangeratemanager.cpp:89-98</c>),
/// which loses every rate of a currency coded 1,000 or more (QuantLib's crypto currencies, 10000 to 10007, or any
/// currency of one's own: a lookup from it throws, and no chain passes through it), and can't tell apart two currencies
/// sharing a code (CNY and CNH, 156: a USD/CNH rate answers for USD/CNY). Here the key is the pair of currencies,
/// ordered as QuantLib's key orders pairs, the codes and names breaking a tie; a chained lookup tests which pairs
/// involve a currency and which currencies it has passed by the same identity, where QuantLib uses the numeric code
/// (<c>:95-98</c>, <c>:159</c>, <c>:170</c>), and chains as QuantLib's <c>chain</c> does. So QuantLib's order and answers
/// stand wherever the numeric codes are distinct and below 1,000 and the currencies' names distinct, BGL and BGN apart:
/// three documented divergences (<c>Divergences/ExchangeRateKeyTests.cs</c>,
/// <c>Divergences/SharedNumericCodeTests.cs</c>, <c>Divergences/CurrencyNameAndCodeTests.cs</c>).
/// </para>
/// <para>
/// Two registries are equal when they hold the same pairs, each with equal entries (rate, start and end dates) in the
/// same order. The hash combines one hash per pair, computed when the registry is built.
/// </para>
/// </remarks>
public sealed class ExchangeRates : IEquatable<ExchangeRates>
{
    private static readonly ImmutableSortedDictionary<Pair, ImmutableArray<Entry>> Empty =
        ImmutableSortedDictionary.Create<Pair, ImmutableArray<Entry>>(PairComparer.Instance);

    private readonly ImmutableSortedDictionary<Pair, ImmutableArray<Entry>> data;
    private readonly int hash;

    private ExchangeRates(ImmutableSortedDictionary<Pair, ImmutableArray<Entry>> data)
    {
        this.data = data;
        var h = default(HashCode);
        h.Add(data.Count);
        foreach (KeyValuePair<Pair, ImmutableArray<Entry>> kv in data)
        {
            h.Add(kv.Key.Low);
            h.Add(kv.Key.High);
            foreach (Entry e in kv.Value)
            {
                h.Add(e);
            }
        }

        hash = h.ToHashCode();
    }

    /// <summary>Gets the registry holding QuantLib's known rates alone, its state at start and after
    /// <c>clear()</c>.</summary>
    public static ExchangeRates Default { get; } = ConventionProvider.BuildUnderQuantLib(KnownRates);

    /// <summary>A registry with one more exchange rate, valid between two dates.</summary>
    /// <param name="rate">The rate.</param>
    /// <param name="startDate">The first date it is valid on; <see langword="null"/> for the earliest date, QuantLib's
    /// <c>Date::minDate()</c>.</param>
    /// <param name="endDate">The last date it is valid on; <see langword="null"/> for the latest date, QuantLib's
    /// <c>Date::maxDate()</c>.</param>
    /// <returns>The new registry, the rate first among its pair's; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rate"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">A currency of the rate is QuantLib's empty one.</exception>
    public ExchangeRates Add(ExchangeRate rate, Date? startDate = null, Date? endDate = null)
    {
        ArgumentNullException.ThrowIfNull(rate);
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        Pair key = Pair.Of(rate.Source, rate.Target);
        var entry = new Entry(rate, startDate ?? Date.MinDate, endDate ?? Date.MaxDate);
        ImmutableArray<Entry> entries = data.TryGetValue(key, out ImmutableArray<Entry> list) ? list.Insert(0, entry) : [entry];
        return new ExchangeRates(data.SetItem(key, entries));
    }

    /// <summary>The registry without the added rates: <see cref="Default"/>, QuantLib's known rates alone.</summary>
    /// <returns><see cref="Default"/>; this registry is unchanged.</returns>
    public ExchangeRates Clear() => Default;

    /// <summary>
    /// The exchange rate between two currencies at a date. Direct rates are preferred; with
    /// <see cref="ExchangeRateType.Derived"/>, a rate is triangulated through a currency's triangulation currency, or
    /// chained through other currencies, where no direct one is held.
    /// </summary>
    /// <param name="source">The source currency.</param>
    /// <param name="target">The target currency.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the date when none is given.</param>
    /// <param name="date">The date; <see langword="null"/> for the context's evaluation date.</param>
    /// <param name="type">Whether only a direct rate is returned, or derived ones too.</param>
    /// <returns>The rate; 1.0 between a currency and itself.</returns>
    /// <remarks>Where two or more chains are possible, which one is returned is QuantLib's choice: the first by the
    /// pairs' order.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">
    /// No rate is held for the date ("no direct conversion available", "no conversion available"), or a currency is
    /// QuantLib's empty one.
    /// </exception>
    public ExchangeRate Lookup(
        Currency? source,
        Currency? target,
        EvaluationContext context,
        Date? date = null,
        ExchangeRateType type = ExchangeRateType.Derived)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(date, nameof(date));
        EnumArguments.Check(type, nameof(type));
        try
        {
            return LookupCore(source, target, date ?? context.EvaluationDate, type);
        }
        catch (NoConversionException e)
        {
            throw new NanoQLException(e.Message);
        }
    }

    /// <inheritdoc/>
    public bool Equals(ExchangeRates? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || hash != other.hash || data.Count != other.data.Count)
        {
            return false;
        }

        foreach (KeyValuePair<Pair, ImmutableArray<Entry>> kv in data)
        {
            if (!other.data.TryGetValue(kv.Key, out ImmutableArray<Entry> entries) || !kv.Value.SequenceEqual(entries))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ExchangeRates other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    /// <summary>
    /// QuantLib's <c>lookup</c> with the date given, for the readers that tell a missing rate from other refusals (the
    /// FX reset pricer's today's fixing): a missing rate throws <see cref="NoConversionException"/>, with QuantLib's
    /// text, and every other refusal as <see cref="Lookup"/> throws it.
    /// </summary>
    /// <param name="source">The source currency.</param>
    /// <param name="target">The target currency.</param>
    /// <param name="date">The date.</param>
    /// <param name="type">Whether only a direct rate is returned.</param>
    /// <returns>The rate.</returns>
    internal ExchangeRate LookupCore(Currency? source, Currency? target, Date date, ExchangeRateType type)
    {
        if (source == target)
        {
            return new ExchangeRate(source, target, 1.0);
        }

        // QuantLib reads the currencies' data next, refusing an empty one (currency.hpp's checkNonEmpty).
        Currency s = NonEmpty(source);
        Currency t = NonEmpty(target);

        if (type == ExchangeRateType.Direct)
        {
            return DirectLookup(s, t, date);
        }

        // QuantLib's chain(a, b) calls evaluate b first under MSVC, the wheel's compiler, so where both fail the
        // refusal is b's: the second lookup runs first here.
        if (s.TriangulationCurrency is Currency sourceLink)
        {
            if (sourceLink == t)
            {
                return DirectLookup(s, sourceLink, date);
            }

            ExchangeRate tail = LookupCore(sourceLink, t, date, ExchangeRateType.Derived);
            return ExchangeRate.ChainUnchecked(DirectLookup(s, sourceLink, date), tail);
        }

        if (t.TriangulationCurrency is Currency targetLink)
        {
            if (s == targetLink)
            {
                return DirectLookup(targetLink, t, date);
            }

            ExchangeRate last = DirectLookup(targetLink, t, date);
            return ExchangeRate.ChainUnchecked(LookupCore(s, targetLink, date, ExchangeRateType.Derived), last);
        }

        return SmartLookup(s, t, date, []);
    }

    // QuantLib's Currency data accessors on an empty currency: "no currency data provided".
    private static Currency NonEmpty(Currency? c) => c ?? throw new NanoQLException("no currency data provided");

    private static ExchangeRates KnownRates()
    {
        var r = new ExchangeRates(Empty);
        Date maxDate = Date.MaxDate;

        // currencies obsoleted by Euro
        r = r.Add(new ExchangeRate(new EURCurrency(), new ATSCurrency(), 13.7603), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new BEFCurrency(), 40.3399), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new DEMCurrency(), 1.95583), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new ESPCurrency(), 166.386), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new FIMCurrency(), 5.94573), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new FRFCurrency(), 6.55957), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new GRDCurrency(), 340.750), new Date(1, Month.January, 2001), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new IEPCurrency(), 0.787564), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new ITLCurrency(), 1936.27), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new LUFCurrency(), 40.3399), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new NLGCurrency(), 2.20371), new Date(1, Month.January, 1999), maxDate);
        r = r.Add(new ExchangeRate(new EURCurrency(), new PTECurrency(), 200.482), new Date(1, Month.January, 1999), maxDate);

        // other obsoleted currencies
        r = r.Add(new ExchangeRate(new TRYCurrency(), new TRLCurrency(), 1000000.0), new Date(1, Month.January, 2005), maxDate);
        r = r.Add(new ExchangeRate(new RONCurrency(), new ROLCurrency(), 10000.0), new Date(1, Month.July, 2005), maxDate);
        r = r.Add(new ExchangeRate(new PENCurrency(), new PEICurrency(), 1000000.0), new Date(1, Month.July, 1991), maxDate);
        r = r.Add(new ExchangeRate(new PEICurrency(), new PEHCurrency(), 1000.0), new Date(1, Month.February, 1985), maxDate);
        return r;
    }

    /// <summary>
    /// Whether two currencies are one in the registry's key: the same numeric code, code and name. The registry tests
    /// which pairs involve a currency and which currencies a chain has passed by it, where QuantLib tests them by the
    /// numeric code alone.
    /// </summary>
    /// <param name="a">A currency.</param>
    /// <param name="b">Another currency.</param>
    /// <returns><see langword="true"/> when the key can't tell them apart.</returns>
    internal static bool SameKey(Currency a, Currency b) => PairComparer.CompareCurrencies(a, b) == 0;

    // QuantLib's directLookup.
    private ExchangeRate DirectLookup(Currency source, Currency target, Date date) =>
        Fetch(source, target, date)
            ?? throw new NoConversionException($"no direct conversion available from {source.Code} to {target.Code} for {date}");

    // QuantLib's smartLookup: a direct rate first; otherwise a chain through the pairs that involve the source, in the
    // pairs' order, each first entry's other currency tried unless an earlier step of the chain started there.
    private ExchangeRate SmartLookup(Currency source, Currency target, Date date, ImmutableList<Currency> forbidden)
    {
        if (Fetch(source, target, date) is ExchangeRate direct)
        {
            return direct;
        }

        forbidden = forbidden.Add(source);
        foreach (KeyValuePair<Pair, ImmutableArray<Entry>> kv in data)
        {
            if (kv.Key.Involves(source) && !kv.Value.IsEmpty)
            {
                Entry e = kv.Value[0];
                Currency other = (source == e.Rate.Source ? e.Rate.Target : e.Rate.Source)!;
                if (!forbidden.Exists(f => SameKey(f, other)) && Fetch(source, other, date) is ExchangeRate head)
                {
                    try
                    {
                        ExchangeRate tail = SmartLookup(other, target, date, forbidden);
                        return ExchangeRate.ChainUnchecked(head, tail);
                    }
                    catch (Exception x) when (x is NoConversionException or NanoQLException or ArgumentException)
                    {
                        // QuantLib's catch (Error&) takes every refusal, the chain's among them: it discards this
                        // rate and goes on.
                    }
                }
            }
        }

        throw new NoConversionException($"no conversion available from {source.Code} to {target.Code} for {date}");
    }

    // QuantLib's fetch: the first entry of the pair valid at the date.
    private ExchangeRate? Fetch(Currency source, Currency target, Date date)
    {
        if (data.TryGetValue(Pair.Of(source, target), out ImmutableArray<Entry> entries))
        {
            foreach (Entry e in entries)
            {
                if (date >= e.StartDate && date <= e.EndDate)
                {
                    return e.Rate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A missing rate, which the direct and chained lookups raise with QuantLib's texts and <see cref="Lookup"/>
    /// rethrows as a <see cref="NanoQLException"/>; internal readers catch it apart from other refusals.
    /// </summary>
    internal sealed class NoConversionException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="NoConversionException"/> class.</summary>
        /// <param name="message">QuantLib's text.</param>
        internal NoConversionException(string message)
            : base(message)
        {
        }
    }

    // A stored rate with the dates it is valid between, both included.
    private sealed record Entry(ExchangeRate Rate, Date StartDate, Date EndDate);

    // An unordered pair of currencies, held with the lower by numeric code, code and name first.
    private readonly record struct Pair(Currency Low, Currency High)
    {
        internal static Pair Of(Currency? c1, Currency? c2)
        {
            Currency a = NonEmpty(c1);
            Currency b = NonEmpty(c2);
            return PairComparer.CompareCurrencies(a, b) <= 0 ? new Pair(a, b) : new Pair(b, a);
        }

        internal bool Involves(Currency c) => SameKey(Low, c) || SameKey(High, c);
    }

    // The pairs' order: QuantLib's key, min(numeric code) × 1000 + max(numeric code), then the currencies' codes and
    // names, which tell apart pairs the key confuses.
    private sealed class PairComparer : IComparer<Pair>
    {
        internal static readonly PairComparer Instance = new();

        public int Compare(Pair x, Pair y)
        {
            int c = QuantLibKey(x).CompareTo(QuantLibKey(y));
            if (c != 0)
            {
                return c;
            }

            c = CompareCurrencies(x.Low, y.Low);
            return c != 0 ? c : CompareCurrencies(x.High, y.High);
        }

        internal static int CompareCurrencies(Currency a, Currency b)
        {
            int c = a.NumericCode.CompareTo(b.NumericCode);
            if (c == 0)
            {
                c = string.CompareOrdinal(a.Code, b.Code);
            }

            return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
        }

        private static long QuantLibKey(Pair p) =>
            ((long)Math.Min(p.Low.NumericCode, p.High.NumericCode) * 1000) + Math.Max(p.Low.NumericCode, p.High.NumericCode);
    }
}
