// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflationindex.hpp ql/indexes/inflationindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class of inflation indexes, whose fixings cover a period: a month, a quarter, a year.</summary>
/// <remarks>
/// <para>
/// A fixing stored through <see cref="IndexFixings.AddFixing"/> is written on every day of its date's inflation period,
/// as QuantLib's <c>addFixing</c> writes it, through <see cref="FixingDates"/>; the reads key on the period's first day.
/// The store's bulk writers store the dates they are given, as QuantLib's non-virtual <c>addFixings</c> does.
/// </para>
/// <para>
/// Inflation indexes aren't associated with a day but with a period, so they have no fixing calendar: QuantLib's
/// <see cref="NullCalendar"/> stands in, and every date is a valid fixing date. An extension point.
/// </para>
/// </remarks>
public abstract class InflationIndex : FinancialIndex
{
    private static readonly Calendar NullFixingCalendar = new NullCalendar();

    /// <summary>Initializes a new instance of the <see cref="InflationIndex"/> class.</summary>
    /// <param name="familyName">The index's family name, such as "RPI".</param>
    /// <param name="region">The region the index is published for.</param>
    /// <param name="revised">Whether the index's fixings are revised after publication.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="availabilityLag">The lag after which a period's fixing is available; the fixing stays the
    /// period's, whenever it is published.</param>
    /// <param name="currency">The index's currency.</param>
    /// <exception cref="ArgumentNullException"><paramref name="familyName"/>, <paramref name="region"/> or
    /// <paramref name="currency"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type.</exception>
    protected InflationIndex(
        string familyName, Region region, bool revised, Frequency frequency, Period availabilityLag, Currency currency)
        : this(familyName, region, revised, frequency, availabilityLag, currency, ConventionProvider.Current)
    {
    }

    // An index keeping a given provider: a preset's, read once, or a clone's source's.
    private protected InflationIndex(
        string familyName,
        Region region,
        bool revised,
        Frequency frequency,
        Period availabilityLag,
        Currency currency,
        ConventionProvider provider)
        : base(provider)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(currency);
        EnumArguments.Check(frequency, nameof(frequency));
        FamilyName = familyName;
        Region = region;
        Revised = revised;
        Frequency = frequency;
        AvailabilityLag = availabilityLag;
        Currency = currency;
        Name = region.Name + " " + familyName;
    }

    /// <summary>Gets the index's name: the region's name, a space and the family name.</summary>
    public override string Name { get; }

    /// <summary>Gets QuantLib's null calendar, since inflation indexes have no fixing calendar.</summary>
    public override Calendar FixingCalendar => NullFixingCalendar;

    /// <summary>Gets the index's family name.</summary>
    public string FamilyName { get; }

    /// <summary>Gets the region the index is published for.</summary>
    public Region Region { get; }

    /// <summary>Gets a value indicating whether the index's fixings are revised after publication.</summary>
    public bool Revised { get; }

    /// <summary>Gets the index's frequency.</summary>
    public Frequency Frequency { get; }

    /// <summary>
    /// Gets the lag after which a period's fixing is available: January's value may only be published in April, and
    /// it is still January's fixing.
    /// </summary>
    public Period AvailabilityLag { get; }

    /// <summary>Gets the index's currency.</summary>
    public Currency Currency { get; }

    /// <summary>Whether a date is a valid fixing date: every date is, since the index has no fixing calendar.</summary>
    /// <param name="fixingDate">The date.</param>
    /// <returns><see langword="true"/>.</returns>
    public override bool IsValidFixingDate(Date fixingDate) => true;

    /// <summary>
    /// The fixing on a date, stored or forecast: a fixing covers its inflation period, and forecasting needs a term
    /// structure whose base date is earlier than its reference date, since fixings are available only with a lag.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="forecastTodaysFixing">Ignored, as QuantLib ignores it.</param>
    /// <returns>The fixing.</returns>
    public abstract override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false);

    /// <summary>The stored fixing on a date, read on the first day of its inflation period.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns>The fixing, or <see langword="null"/> when none is stored.</returns>
    public abstract override double? PastFixing(Date fixingDate, EvaluationContext context);

    /// <summary>Every day of the date's inflation period, on which a fixing is stored.</summary>
    /// <param name="fixingDate">The fixing date given to the store.</param>
    /// <returns>The days, from the period's first to its last.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index's frequency isn't one
    /// <see cref="InflationTermStructureFunctions.InflationPeriod"/> handles.</exception>
    protected internal override IEnumerable<Date> FixingDates(Date fixingDate)
    {
        (Date start, Date end) = InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency);
        int n = (end - start) + 1;
        var dates = new Date[n];
        for (int i = 0; i < n; ++i)
        {
            dates[i] = start + i;
        }

        return dates;
    }
}
