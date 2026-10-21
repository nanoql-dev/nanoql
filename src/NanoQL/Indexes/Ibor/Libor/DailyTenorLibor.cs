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

/// <summary>The base class for one-day deposit LIBOR indexes, fixed by ICE
/// (https://www.theice.com/marketdata/reports/170).</summary>
/// <remarks>
/// Fixed on the joint calendar of London and the currency's financial center. An extension point: QuantLib's
/// currency daily-tenor LIBORs derive from it.
/// </remarks>
public class DailyTenorLibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="DailyTenorLibor"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="currency">The currency, not EUR.</param>
    /// <param name="financialCenterCalendar">The financial center's calendar.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The currency is EUR.</exception>
    /// <exception cref="ArgumentNullException">The calendar or the day counter is <see langword="null"/>.</exception>
    public DailyTenorLibor(
        string familyName,
        int settlementDays,
        Currency currency,
        Calendar financialCenterCalendar,
        DayCounter dayCounter,
        YieldTermStructure? h = null)
        : base(
            familyName,
            new Period(1, TimeUnit.Days),
            settlementDays,
            currency,
            // http://www.bba.org.uk/bba/jsp/polopoly.jsp?d=225&a=1412 :
            // no o/n or s/n fixings (as the case may be) will take place
            // when the principal centre of the currency concerned is
            // closed but London is open on the fixing day.
            new JointCalendar(
                new UnitedKingdom(UnitedKingdomMarket.Exchange),
                financialCenterCalendar,
                JointCalendarRule.JoinHolidays),
            LiborConventions.Convention(new Period(1, TimeUnit.Days)),
            LiborConventions.EndOfMonth(new Period(1, TimeUnit.Days)),
            dayCounter,
            h)
    {
        if (!(currency != new EURCurrency()))
        {
            throw new ArgumentException("for EUR Libor dedicated EurLibor constructor must be used", nameof(currency));
        }
    }
}
