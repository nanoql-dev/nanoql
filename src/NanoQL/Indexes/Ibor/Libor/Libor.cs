// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/libor.hpp ql/indexes/ibor/libor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class for LIBOR indexes, fixed by ICE (https://www.theice.com/marketdata/reports/170).</summary>
/// <remarks>
/// Fixed on the London exchange calendar; the value and maturity dates are adjusted on the joint calendar of London
/// and the currency's financial center. An extension point: QuantLib's currency LIBORs derive from it.
/// </remarks>
public class Libor : IborIndex
{
    private readonly Calendar financialCenterCalendar;
    private readonly Calendar jointCalendar;

    /// <summary>Initializes a new instance of the <see cref="Libor"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="tenor">The tenor, not in days.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="currency">The currency, not EUR.</param>
    /// <param name="financialCenterCalendar">The financial center's calendar.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">
    /// The tenor is in days, for which <see cref="DailyTenorLibor"/> must be used, or in a unit LIBOR doesn't have; or
    /// the currency is EUR.
    /// </exception>
    /// <exception cref="ArgumentNullException">A calendar or the day counter is <see langword="null"/>.</exception>
    public Libor(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency currency,
        Calendar financialCenterCalendar,
        DayCounter dayCounter,
        YieldTermStructure? h = null)
        : base(
            familyName,
            tenor,
            settlementDays,
            currency,
            // http://www.bba.org.uk/bba/jsp/polopoly.jsp?d=225&a=1412 :
            // UnitedKingdom::Exchange is the fixing calendar for
            // a) all currencies but EUR
            // b) all indexes but o/n and s/n
            new UnitedKingdom(UnitedKingdomMarket.Exchange),
            LiborConventions.Convention(tenor),
            LiborConventions.EndOfMonth(tenor),
            dayCounter,
            h)
    {
        ArgumentNullException.ThrowIfNull(financialCenterCalendar);
        this.financialCenterCalendar = financialCenterCalendar;
        jointCalendar = new JointCalendar(
            new UnitedKingdom(UnitedKingdomMarket.Exchange), financialCenterCalendar, JointCalendarRule.JoinHolidays);
        if (!(Tenor.Units != TimeUnit.Days))
        {
            throw new ArgumentException(
                $"for daily tenors ({Tenor}) dedicated DailyTenor constructor must be used", nameof(tenor));
        }

        if (!(currency != new EURCurrency()))
        {
            throw new ArgumentException("for EUR Libor dedicated EurLibor constructor must be used", nameof(currency));
        }
    }

    /// <summary>Gets the joint calendar of London and the financial center, on which the value and maturity dates are
    /// adjusted.</summary>
    public Calendar JointCalendar => jointCalendar;

    // A clone: the source's conventions, calendars and provider, on another forwarding curve.
    private Libor(Libor source, YieldTermStructure? forwarding)
        : base(
            source.FamilyName,
            source.Tenor,
            source.FixingDays,
            source.Currency,
            source.FixingCalendar,
            source.BusinessDayConvention,
            source.EndOfMonth,
            source.DayCounter,
            forwarding,
            source.Provider)
    {
        financialCenterCalendar = source.financialCenterCalendar;
        jointCalendar = source.jointCalendar;
    }

    /// <summary>
    /// The value date of a fixing: the settlement days after it on the fixing calendar, then adjusted on the joint
    /// calendar, as ICE's rules have it.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <returns>The value date.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date.</exception>
    public override Date ValueDate(Date fixingDate)
    {
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        if (!IsValidFixingDate(fixingDate))
        {
            throw new ArgumentException($"Fixing date {fixingDate} is not valid", nameof(fixingDate));
        }

        // http://www.bba.org.uk/bba/jsp/polopoly.jsp?d=225&a=1412 :
        // For all currencies other than EUR and GBP the period between
        // Fixing Date and Value Date will be two London business days
        // after the Fixing Date, or if that day is not both a London
        // business day and a business day in the principal financial centre
        // of the currency concerned, the next following day which is a
        // business day in both centres shall be the Value Date.
        Date d = FixingCalendar.Advance(fixingDate, FixingDays, TimeUnit.Days);
        return jointCalendar.Adjust(d);
    }

    /// <summary>The maturity date of a value date, advanced by the tenor on the joint calendar.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The maturity date.</returns>
    public override Date MaturityDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));

        // Where a deposit is made on the final business day of a
        // particular calendar month, the maturity of the deposit shall
        // be on the final business day of the month in which it matures
        // (not the corresponding date in the month of maturity). Or in
        // other words, in line with market convention, BBA LIBOR rates
        // are dealt on an end-end basis. For instance a one month
        // deposit for value 28th February would mature on 31st March,
        // not the 28th of March.
        return jointCalendar.Advance(valueDate, Tenor, BusinessDayConvention, EndOfMonth);
    }

    /// <summary>A copy of the index on another forwarding curve: a plain <see cref="Libor"/>, as QuantLib's.</summary>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public override IborIndex Clone(YieldTermStructure? forwarding) => new Libor(this, forwarding);
}
