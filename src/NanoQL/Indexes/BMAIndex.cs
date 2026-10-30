// SPDX-FileCopyrightText: Copyright (C) 2007 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/bmaindex.hpp ql/indexes/bmaindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The Bond Market Association index: the short-term tax-exempt reference index of the Bond Market Association. It
/// has tenor one week, is fixed weekly on Wednesdays and is applied with a one-day fixing gap from Thursdays on for one
/// week. It is the tax-exempt correspondent of the 1M USD Libor.
/// </summary>
/// <remarks>
/// The index holds its forecasting curve, QuantLib's handle; <see langword="null"/> is QuantLib's empty handle, with
/// which stored fixings still work and a forecast throws. It has no clone, as in QuantLib: an index on another curve is
/// a new <see cref="BMAIndex"/>.
/// </remarks>
public sealed class BMAIndex : InterestRateIndex
{
    /// <summary>Initializes a new instance of the <see cref="BMAIndex"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public BMAIndex(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadIborIndex("BMAIndex", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps. The index has no convention or
    // end-of-month rule of its own, so it refuses conventions that set them.
    private BMAIndex((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "BMA",
            new Period(1, TimeUnit.Weeks),
            read.Values.FixingDays,
            read.Values.Currency,
            read.Values.FixingCalendar,
            read.Values.DayCounter,
            read.Provider)
    {
        if (read.Values.Convention != BusinessDayConvention.Following || read.Values.EndOfMonth)
        {
            throw new NanoQLException("the BMA index's conventions take no convention or end of month but QuantLib's");
        }

        ForwardingTermStructure = h;
    }

    // QuantLib's conventions, built in the provider's scope; its convention and end of month are none of the index's.
    private static IborIndexConventions Reference() => new(
        1,
        new USDCurrency(),
        new UnitedStates(UnitedStatesMarket.GovernmentBond),
        BusinessDayConvention.Following,
        false,
        new ActualActual(ActualActualConvention.ISDA));

    /// <summary>Gets the curve that forecasts fixings, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? ForwardingTermStructure { get; }

    /// <summary>
    /// Whether a date is a valid fixing date: a Wednesday that is a business day, or the first business day after a run
    /// of holidays starting on a Wednesday.
    /// </summary>
    /// <param name="fixingDate">The date.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is <c>default(Date)</c>.</exception>
    public override bool IsValidFixingDate(Date fixingDate)
    {
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        Calendar cal = FixingCalendar;

        // either the fixing date is last Wednesday, or all days between last Wednesday included and the fixing date are
        // holidays
        for (Date d = PreviousWednesday(fixingDate); d < fixingDate; ++d)
        {
            if (cal.IsBusinessDay(d))
            {
                return false;
            }
        }

        // also, the fixing date itself must be a business day
        return cal.IsBusinessDay(fixingDate);
    }

    /// <summary>
    /// The maturity date of a value date: the business day after the Wednesday following the value date's fixing
    /// date.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The maturity date.</returns>
    /// <exception cref="ArgumentException"><paramref name="valueDate"/> is <c>default(Date)</c>.</exception>
    public override Date MaturityDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        Calendar cal = FixingCalendar;
        Date fixingDate = cal.Advance(valueDate, -1, TimeUnit.Days);
        Date nextWednesday = PreviousWednesday(fixingDate + 7);
        return cal.Advance(nextWednesday, 1, TimeUnit.Days);
    }

    /// <summary>
    /// The schedule of fixing dates between two dates: weekly, from the Wednesday on or before the start to the first
    /// Wednesday after the end, adjusted to the following business day.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns>The schedule.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the schedule can't be built.</exception>
    public Schedule FixingSchedule(Date start, Date end)
    {
        Date.ThrowIfDefault(start, nameof(start));
        Date.ThrowIfDefault(end, nameof(end));
        return new MakeSchedule()
            .From(PreviousWednesday(start))
            .To(NextWednesday(end))
            .WithFrequency(Frequency.Weekly)
            .WithCalendar(FixingCalendar)
            .WithConvention(BusinessDayConvention.Following)
            .Forwards()
            .ToSchedule();
    }

    /// <summary>
    /// The fixing forecast from the forwarding curve: the simple forward rate, on the index's day counter, from the
    /// business day after the fixing date to its maturity date.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context; not read.</param>
    /// <returns>The forecast.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The index has no forwarding curve, or the curve can't give the rate.</exception>
    public override double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        YieldTermStructure termStructure = ForwardingTermStructure
            ?? throw new NanoQLException($"null term structure set to this instance of {Name}");
        Date start = FixingCalendar.Advance(fixingDate, 1, TimeUnit.Days);
        Date end = MaturityDate(start);
        return termStructure.ForwardRate(start, end, DayCounter, Compounding.Simple).Rate;
    }

    private static Date PreviousWednesday(Date date)
    {
        int w = (int)date.Weekday;

        // roll back w-4 days, or roll forward 4-w days and back one week
        return w >= 4 ? date - (w - 4) : date + (4 - w - 7);
    }

    private static Date NextWednesday(Date date) => PreviousWednesday(date + 7);
}
