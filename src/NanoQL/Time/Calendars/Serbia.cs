// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/serbia.hpp ql/time/calendars/serbia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Serbian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Belgrade stock exchange (see https://www.belex.rs/eng/trzista_i_hartije/kalendar):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year, January 1st</description></item>
/// <item><description>New Year, January 2nd</description></item>
/// <item><description>Serbian Orthodox Christmas, January 7th</description></item>
/// <item><description>Serbian Statehood Day, February 15 &amp; 16th</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Armistice Day in World War I, Novemeber 11th</description></item>
/// <item><description>Trading system maintenance, statistics and data migration, December 31st</description></item>
/// </list>
/// </para>
/// <para>
/// Good Friday and Easter Monday follow Orthodox Easter, as the exchange's calendar has them; QuantLib 1.44 takes
/// Western Easter's: a documented divergence.
/// </para>
/// </remarks>
public sealed class Serbia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl BseImplInstance = new BseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Serbia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Serbia(SerbiaMarket market = SerbiaMarket.BSE)
    {
        switch (market)
        {
            case SerbiaMarket.BSE:
                impl = BseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => impl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsWeekend(Weekday w);

        public abstract bool IsBusinessDay(Date date);
    }

    // Western weekend, Orthodox Easter: the Belgrade Stock Exchange closes on Orthodox Good Friday and Easter Monday,
    // where QuantLib's implementation derives from WesternImpl and takes Western Easter's
    private sealed class BseImpl : Impl
    {
        public override string Name => "Belgrade stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = OrthodoxImpl.EasterMonday(y);
            // Serbian holidays
            if (WesternImpl.IsWeekend(w)
                // New Year
                || (d == 1 && m == Month.January)
                || (d == 2 && m == Month.January)
                // Serbian Orthodox Christmas
                || (d == 7 && m == Month.January)
                // Serbian Statehood Day
                || (d == 15 && m == Month.February)
                || (d == 16 && m == Month.February)
                // Serbian Statehood Day (observed)
                // Serbia's Statehood Day (Sretenje) is officially observed on February 15 and February 16.
                // However, since these dates fall on a Saturday and Sunday, the government designates Monday,
                // February 17, as an additional non-working day. This practice ensures that citizens receive a weekday
                // off when public holidays coincide with weekends.
                || ((d == 17 && m == Month.February)
                    && WesternImpl.IsWeekend(new Date(15, Month.February, y).Weekday)
                    && WesternImpl.IsWeekend(new Date(16, Month.February, y).Weekday))
                // Good Friday
                || (dd == em - 3 && y >= 2016)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Armistice Day in World War I
                || (d == 11 && m == Month.November)
                // Trading system maintenance, statistics and data migration
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
