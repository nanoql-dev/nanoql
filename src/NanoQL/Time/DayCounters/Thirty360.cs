// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2018 Alexey Indiryakov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/thirty360.hpp ql/time/daycounters/thirty360.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>
/// 30/360 day count convention: the 30/360 day count can be calculated according to a number of conventions.
/// </summary>
/// <remarks>
/// <para>
/// US convention: if the starting date is the 31st of a month or the last day of February, it becomes equal to the
/// 30th of the same month. If the ending date is the 31st of a month and the starting date is the 30th or 31st of a
/// month, the ending date becomes equal to the 30th. If the ending date is the last of February and the starting date
/// is also the last of February, the ending date becomes equal to the 30th. Also known as "30/360" or "360/360".
/// </para>
/// <para>
/// Bond Basis convention: if the starting date is the 31st of a month, it becomes equal to the 30th of the same month.
/// If the ending date is the 31st of a month and the starting date is the 30th or 31st of a month, the ending date
/// also becomes equal to the 30th of the month. Also known as "US (ISMA)".
/// </para>
/// <para>
/// European convention: starting dates or ending dates that occur on the 31st of a month become equal to the 30th of
/// the same month. Also known as "30E/360", or "Eurobond Basis".
/// </para>
/// <para>
/// Italian convention: starting dates or ending dates that occur on February and are greater than 27 become equal to
/// 30 for computational sake.
/// </para>
/// <para>
/// ISDA convention: starting or ending dates on the 31st of the month become equal to 30; starting dates or ending
/// dates that occur on the last day of February also become equal to 30, except for the termination date. Also known
/// as "30E/360 ISDA", "30/360 ISDA", or "30/360 German".
/// </para>
/// <para>
/// NASD convention: if the starting date is the 31st of a month, it becomes equal to the 30th of the same month. If the
/// ending date is the 31st of a month and the starting date is earlier than the 30th of a month, the ending date
/// becomes equal to the 1st of the next month, otherwise the ending date becomes equal to the 30th of the same month.
/// </para>
/// </remarks>
public sealed class Thirty360 : DayCounter
{
    private readonly Thirty360Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Thirty360"/> class.</summary>
    /// <param name="c">The convention.</param>
    /// <param name="terminationDate">
    /// The termination date, which the ISDA and German conventions don't move from the last day of February; ignored
    /// by the other conventions.
    /// </param>
    /// <exception cref="ArgumentException">The termination date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The convention is unknown.</exception>
    public Thirty360(Thirty360Convention c, Date? terminationDate = null)
    {
        if (terminationDate is Date t && t.SerialNumber == 0)
        {
            throw new ArgumentException(
                "default(Date) is not a valid date; a missing date is Date? null", nameof(terminationDate));
        }

        impl = Implementation(c, terminationDate);
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override int DayCount(Date d1, Date d2) => impl.DayCount(d1, d2);

    /// <inheritdoc/>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return impl.YearFraction(d1, d2);
    }

    private static Thirty360Impl Implementation(Thirty360Convention c, Date? terminationDate)
    {
        switch (c)
        {
            case Thirty360Convention.USA:
                return new USImpl();
            case Thirty360Convention.European:
            case Thirty360Convention.EurobondBasis:
                return new EUImpl();
            case Thirty360Convention.Italian:
                return new ITImpl();
            case Thirty360Convention.ISMA:
            case Thirty360Convention.BondBasis:
                return new ISMAImpl();
            case Thirty360Convention.ISDA:
            case Thirty360Convention.German:
                return new ISDAImpl(terminationDate);
            case Thirty360Convention.NASD:
                return new NASDImpl();
            default:
                throw new NanoQLException("unknown 30/360 convention");
        }
    }

    private abstract class Thirty360Impl
    {
        public abstract string Name { get; }

        public abstract int DayCount(Date d1, Date d2);

        public double YearFraction(Date d1, Date d2) => DayCount(d1, d2) / 360.0;
    }

    private sealed class USImpl : Thirty360Impl
    {
        public override string Name => "30/360 (US)";

        public override int DayCount(Date d1, Date d2)
        {
            int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
            Month mm1 = d1.Month, mm2 = d2.Month;
            int yy1 = d1.Year, yy2 = d2.Year;

            // See https://en.wikipedia.org/wiki/Day_count_convention#30/360_US
            // NOTE: the order of checks is important
            if (Thirty360Helpers.IsLastOfFebruary(dd1, mm1, yy1))
            {
                if (Thirty360Helpers.IsLastOfFebruary(dd2, mm2, yy2))
                {
                    dd2 = 30;
                }

                dd1 = 30;
            }

            if (dd2 == 31 && dd1 >= 30)
            {
                dd2 = 30;
            }

            if (dd1 == 31)
            {
                dd1 = 30;
            }

            return (360 * (yy2 - yy1)) + (30 * (mm2 - mm1)) + (dd2 - dd1);
        }
    }

    private sealed class ISMAImpl : Thirty360Impl
    {
        public override string Name => "30/360 (Bond Basis)";

        public override int DayCount(Date d1, Date d2)
        {
            int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
            Month mm1 = d1.Month, mm2 = d2.Month;
            int yy1 = d1.Year, yy2 = d2.Year;

            if (dd1 == 31)
            {
                dd1 = 30;
            }

            if (dd2 == 31 && dd1 == 30)
            {
                dd2 = 30;
            }

            return (360 * (yy2 - yy1)) + (30 * (mm2 - mm1)) + (dd2 - dd1);
        }
    }

    private sealed class EUImpl : Thirty360Impl
    {
        public override string Name => "30E/360 (Eurobond Basis)";

        public override int DayCount(Date d1, Date d2)
        {
            int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
            Month mm1 = d1.Month, mm2 = d2.Month;
            int yy1 = d1.Year, yy2 = d2.Year;

            if (dd1 == 31)
            {
                dd1 = 30;
            }

            if (dd2 == 31)
            {
                dd2 = 30;
            }

            return (360 * (yy2 - yy1)) + (30 * (mm2 - mm1)) + (dd2 - dd1);
        }
    }

    private sealed class ITImpl : Thirty360Impl
    {
        public override string Name => "30/360 (Italian)";

        public override int DayCount(Date d1, Date d2)
        {
            int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
            Month mm1 = d1.Month, mm2 = d2.Month;
            int yy1 = d1.Year, yy2 = d2.Year;

            if (dd1 == 31)
            {
                dd1 = 30;
            }

            if (dd2 == 31)
            {
                dd2 = 30;
            }

            if (mm1 == Month.February && dd1 > 27)
            {
                dd1 = 30;
            }

            if (mm2 == Month.February && dd2 > 27)
            {
                dd2 = 30;
            }

            return (360 * (yy2 - yy1)) + (30 * (mm2 - mm1)) + (dd2 - dd1);
        }
    }

    private sealed class ISDAImpl : Thirty360Impl
    {
        private readonly Date? terminationDate;

        public ISDAImpl(Date? terminationDate)
        {
            this.terminationDate = terminationDate;
        }

        public override string Name => "30E/360 (ISDA)";

        public override int DayCount(Date d1, Date d2)
        {
            int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
            Month mm1 = d1.Month, mm2 = d2.Month;
            int yy1 = d1.Year, yy2 = d2.Year;

            if (dd1 == 31)
            {
                dd1 = 30;
            }

            if (dd2 == 31)
            {
                dd2 = 30;
            }

            if (Thirty360Helpers.IsLastOfFebruary(dd1, mm1, yy1))
            {
                dd1 = 30;
            }

            if (d2 != terminationDate && Thirty360Helpers.IsLastOfFebruary(dd2, mm2, yy2))
            {
                dd2 = 30;
            }

            return (360 * (yy2 - yy1)) + (30 * (mm2 - mm1)) + (dd2 - dd1);
        }
    }

    private sealed class NASDImpl : Thirty360Impl
    {
        public override string Name => "30/360 (NASD)";

        public override int DayCount(Date d1, Date d2)
        {
            int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
            int mm1 = (int)d1.Month, mm2 = (int)d2.Month;
            int yy1 = d1.Year, yy2 = d2.Year;

            if (dd1 == 31)
            {
                dd1 = 30;
            }

            if (dd2 == 31 && dd1 >= 30)
            {
                dd2 = 30;
            }

            if (dd2 == 31 && dd1 < 30)
            {
                dd2 = 1;
                mm2++;
            }

            return (360 * (yy2 - yy1)) + (30 * (mm2 - mm1)) + (dd2 - dd1);
        }
    }
}

// QuantLib's anonymous-namespace helper in thirty360.cpp.
file static class Thirty360Helpers
{
    public static bool IsLastOfFebruary(int d, Month m, int y) =>
        m == Month.February && d == 28 + (Date.IsLeap(y) ? 1 : 0);
}
