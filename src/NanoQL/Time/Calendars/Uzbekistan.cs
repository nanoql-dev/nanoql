// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/uzbekistan.hpp ql/time/calendars/uzbekistan.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Uzbek calendar.</summary>
/// <remarks>
/// <para>
/// Holidays for the Uzbekistan exchanges (data from https://www.advantour.com/uzbekistan/holidays.htm):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>International Women's Day, March 8th</description></item>
/// <item><description>Navruz (Persian New Year), March 21st</description></item>
/// <item><description>Day of Remembrance and Honors, May 9th</description></item>
/// <item><description>Independence Day, September 1st</description></item>
/// <item><description>Teachers' Day, October 1st</description></item>
/// <item><description>Constitution Day, December 8th</description></item>
/// <item><description>Eid al-Fitr (Ramazon Hayit), variable date</description></item>
/// <item><description>Eid al-Adha (Qurbon Hayit), variable date</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Uzbekistan : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl UzbekistanImplInstance = new UzbekistanImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Uzbekistan"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Uzbekistan(UzbekistanMarket market = UzbekistanMarket.UZSE)
    {
        switch (market)
        {
            case UzbekistanMarket.UZSE:
                impl = UzbekistanImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Uzbekistan.{market}"));
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

    private sealed class UzbekistanImpl : Impl
    {
        public override string Name => "Uzbekistan Stock Exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;

            if (WesternImpl.IsWeekend(w)
                || IslamicHolidaysFunctions.IsEidAlFitrByMoonSighting(date)
                || IslamicHolidaysFunctions.IsEidAlAdhaByMoonSighting(date)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // International Womens Day
                || (d == 8 && m == Month.March)
                // Navruz(Persian New Year)
                || (d == 21 && m == Month.March)
                // Day of Remembrance and Honors
                || (d == 9 && m == Month.May)
                // Independence Day
                || (d == 1 && m == Month.September)
                // Teachers Day
                || (d == 1 && m == Month.October)
                // Constitution Day
                || (d == 8 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
