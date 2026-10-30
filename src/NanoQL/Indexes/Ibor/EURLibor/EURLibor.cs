// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/eurlibor.hpp ql/indexes/ibor/eurlibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class for all ICE EUR LIBOR indexes but the O/N: Euro LIBOR fixed by ICE.</summary>
/// <remarks>
/// <para>
/// See https://www.theice.com/marketdata/reports/170. This is the rate fixed in London by BBA; use
/// <see cref="Euribor"/> for the fixing by the ECB.
/// </para>
/// <para>
/// An extension point: QuantLib's tenors, <see cref="EURLibor1M"/> to <see cref="EURLibor1Y"/>, derive from it.
/// </para>
/// </remarks>
public class EURLibor : IborIndex
{
    // QuantLib's target_ member, built at construction: the fixing, value and maturity dates' calendar.
    private readonly Calendar target;

    /// <summary>Initializes a new instance of the <see cref="EURLibor"/> class.</summary>
    /// <param name="tenor">The tenor, in weeks, months or years.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tenor's unit is shorter than a day.</exception>
    public EURLibor(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("EURLibor", tenor, () => Reference(tenor)), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps: London's calendar and TARGET,
    // joined for the fixing dates, and TARGET for the value and maturity dates.
    private EURLibor(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "EURLibor",
            tenor,
            read.Values.FixingDays,
            read.Values.Currency,
            // http://www.bba.org.uk/bba/jsp/polopoly.jsp?d=225&a=1412 :
            // JoinHolidays is the fixing calendar for
            // all indexes but o/n
            new JointCalendar(
                read.Values.FixingCalendar,
                read.Values.FinancialCenterCalendar
                    ?? throw new NanoQLException("the EURLibor conventions name no financial-centre calendar"),
                JointCalendarRule.JoinHolidays),
            read.Values.Convention,
            read.Values.EndOfMonth,
            read.Values.DayCounter,
            h,
            read.Provider)
    {
        if (!(Tenor.Units != TimeUnit.Days))
        {
            throw new ArgumentException(
                $"for daily tenors ({Tenor}) dedicated DailyTenor constructor must be used", nameof(tenor));
        }

        target = read.Values.FinancialCenterCalendar;
    }

    // QuantLib's conventions at the tenor, built in the provider's scope: London's fixing calendar and TARGET.
    private static IborIndexConventions Reference(Period tenor) => new(
        2,
        new EURCurrency(),
        new UnitedKingdom(UnitedKingdomMarket.Exchange),
        EURLiborConventions.Convention(tenor),
        EURLiborConventions.EndOfMonth(tenor),
        new Actual360(),
        new TARGET());

    // A clone: the source's conventions, calendars and provider, on another forwarding curve.
    private EURLibor(EURLibor source, YieldTermStructure? forwarding)
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
        target = source.target;
    }

    /// <summary>
    /// The fixing date of a value date: the fixing days before it on TARGET, adjusted to the preceding fixing day.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The fixing date.</returns>
    public override Date FixingDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        return FixingCalendar.Adjust(
            target.Advance(valueDate, -FixingDays, TimeUnit.Days), BusinessDayConvention.Preceding);
    }

    /// <summary>The value date of a fixing: two TARGET business days after it, as ICE's rules have it.</summary>
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
        // In the case of EUR the Value Date shall be two TARGET
        // business days after the Fixing Date.
        return target.Advance(fixingDate, FixingDays, TimeUnit.Days);
    }

    /// <summary>The maturity date of a value date, advanced by the tenor on TARGET.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The maturity date.</returns>
    public override Date MaturityDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));

        // http://www.bba.org.uk/bba/jsp/polopoly.jsp?d=225&a=1412 :
        // In the case of EUR only, maturity dates will be based on days in
        // which the Target system is open.
        return target.Advance(valueDate, Tenor, BusinessDayConvention, EndOfMonth);
    }

    /// <summary>A copy of the index on another forwarding curve: a <see cref="EURLibor"/> of the same tenor, as
    /// QuantLib's.</summary>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public override IborIndex Clone(YieldTermStructure? forwarding) => new EURLibor(this, forwarding);
}
