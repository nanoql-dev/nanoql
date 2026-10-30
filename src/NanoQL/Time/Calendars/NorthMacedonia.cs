// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/northmacedonia.hpp ql/time/calendars/northmacedonia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The North Macedonian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the North Macedonian (see https://www.timeanddate.com/holidays/macedonia/2026?hol=1):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year, January 1st</description></item>
/// <item><description>Orthodox Christmas, January 7th</description></item>
/// <item><description>Orthodox Easter Monday, variable date</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Saints Cyril and Methodius Day, May 24th</description></item>
/// <item><description>Republic Day, August 2nd</description></item>
/// <item><description>Independence Day, September 8th</description></item>
/// <item><description>Revolution Day, October 11th</description></item>
/// <item><description>Day of the Macedonian Uprising, October 23rd</description></item>
/// <item><description>Saint Clement of Ohrid Day, December 8th</description></item>
/// <item><description>Eid al-Fitr (Ramazan Bajram), variable date</description></item>
/// <item><description>Eid al-Adha (Kurban Bayram), variable date</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class NorthMacedonia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl MseImplInstance = new MseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="NorthMacedonia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public NorthMacedonia(NorthMacedoniaMarket market = NorthMacedoniaMarket.MSE)
    {
        switch (market)
        {
            case NorthMacedoniaMarket.MSE:
                impl = MseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"NorthMacedonia.{market}"));
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

    private sealed class MseImpl : Impl
    {
        public override string Name => "Macedonian Stock Exchange";

        public override bool IsWeekend(Weekday w) => OrthodoxImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = OrthodoxImpl.EasterMonday(y);

            if (OrthodoxImpl.IsWeekend(w)
                || IslamicHolidaysFunctions.IsEidAlFitrByMoonSighting(date)
                || IslamicHolidaysFunctions.IsEidAlAdhaByMoonSighting(date)
                // New Year
                || (d == 1 && m == Month.January)
                // Orthodox Christmas
                || (d == 7 && m == Month.January)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Saints Cyril and Methodius Day
                || (d == 24 && m == Month.May)
                // Republic Day
                || (d == 2 && m == Month.August)
                // Independence Day
                || (d == 8 && m == Month.September)
                // Day of People's Uprising
                || (d == 11 && m == Month.October)
                // Day of the Macedonian Revolutionary Struggle
                || (d == 23 && m == Month.October)
                // Saint Clement of Ohrid Day,
                || (d == 8 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
