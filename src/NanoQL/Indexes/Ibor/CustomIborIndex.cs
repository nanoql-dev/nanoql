// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/custom.hpp ql/indexes/ibor/custom.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// A LIBOR-like index that allows specifying custom calendars for value and maturity dates calculations.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="ValueDate"/> advances on the value calendar and adjusts on the maturity
/// calendar.</description></item>
/// <item><description><see cref="MaturityDate"/> advances on the maturity calendar.</description></item>
/// <item><description><see cref="FixingDate"/> goes back on the value calendar.</description></item>
/// </list>
/// <para>
/// Typical LIBOR indexes use fixing calendar = value calendar = UK, maturity calendar = JoinHolidays(UK, currency
/// calendar) for non-EUR currencies; fixing calendar = JoinHolidays(UK, TARGET), value calendar = maturity calendar =
/// TARGET for EUR.
/// </para>
/// </remarks>
public sealed class CustomIborIndex : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="CustomIborIndex"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="fixingCalendar">The calendar of the fixing dates.</param>
    /// <param name="valueCalendar">The calendar on which value dates advance.</param>
    /// <param name="maturityCalendar">The calendar on which value dates adjust and maturity dates advance.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the end-of-month rule applies.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">A calendar is <see langword="null"/>.</exception>
    public CustomIborIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        Calendar valueCalendar,
        Calendar maturityCalendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        YieldTermStructure? h = null)
        : base(familyName, tenor, settlementDays, currency, fixingCalendar, convention, endOfMonth, dayCounter, h)
    {
        ArgumentNullException.ThrowIfNull(valueCalendar);
        ArgumentNullException.ThrowIfNull(maturityCalendar);
        ValueCalendar = valueCalendar;
        MaturityCalendar = maturityCalendar;
    }

    /// <summary>Gets the calendar on which value dates advance.</summary>
    public Calendar ValueCalendar { get; }

    /// <summary>Gets the calendar on which value dates adjust and maturity dates advance.</summary>
    public Calendar MaturityCalendar { get; }

    /// <summary>
    /// The fixing date of a value date: the fixing days before it on the value calendar, adjusted to the preceding
    /// fixing day.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The fixing date.</returns>
    public override Date FixingDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        Date fixingDate = ValueCalendar.Advance(valueDate, -FixingDays, TimeUnit.Days);
        return FixingCalendar.Adjust(fixingDate, BusinessDayConvention.Preceding);
    }

    /// <summary>
    /// The value date of a fixing: the fixing days after it on the value calendar, adjusted on the maturity calendar.
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

        Date d = ValueCalendar.Advance(fixingDate, FixingDays, TimeUnit.Days);
        return MaturityCalendar.Adjust(d);
    }

    /// <summary>The maturity date of a value date, advanced by the tenor on the maturity calendar.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The maturity date.</returns>
    public override Date MaturityDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        return MaturityCalendar.Advance(valueDate, Tenor, BusinessDayConvention, EndOfMonth);
    }

    /// <summary>A copy of the index on another forwarding curve: a <see cref="CustomIborIndex"/> with the same
    /// conventions and calendars, as QuantLib's.</summary>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public override IborIndex Clone(YieldTermStructure? forwarding) =>
        new CustomIborIndex(
            FamilyName,
            Tenor,
            FixingDays,
            Currency,
            FixingCalendar,
            ValueCalendar,
            MaturityCalendar,
            BusinessDayConvention,
            EndOfMonth,
            DayCounter,
            forwarding);
}
