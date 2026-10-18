// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/actualactual.hpp ql/time/daycounters/actualactual.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Actual/Actual day count.</summary>
/// <remarks>
/// <para>The day count can be calculated according to:</para>
/// <list type="bullet">
/// <item><description>
/// the ISDA convention, also known as "Actual/Actual (Historical)", "Actual/Actual", "Act/Act", and according to ISDA
/// also "Actual/365", "Act/365", and "A/365";
/// </description></item>
/// <item><description>the ISMA and US Treasury convention, also known as "Actual/Actual (Bond)";</description></item>
/// <item><description>the AFB convention, also known as "Actual/Actual (Euro)".</description></item>
/// </list>
/// <para>
/// For more details, refer to https://www.isda.org/a/pIJEE/The-Actual-Actual-Day-Count-Fraction-1999.pdf
/// </para>
/// </remarks>
public sealed class ActualActual : DayCounter
{
    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="ActualActual"/> class.</summary>
    /// <param name="c">The convention.</param>
    /// <param name="schedule">
    /// The schedule from which the ISMA and Bond conventions take their reference periods; ignored by the other
    /// conventions. When it is <see langword="null"/> (QuantLib's empty default) or empty, the reference periods are
    /// the ones passed to <see cref="YearFraction"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public ActualActual(ActualActualConvention c, Schedule? schedule = null)
    {
        impl = Implementation(c, schedule);
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Under the ISMA and Bond conventions with a schedule, the dates are out of the schedule's range; without a
    /// schedule, the reference period is invalid.
    /// </exception>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return impl.YearFraction(d1, d2, refPeriodStart, refPeriodEnd);
    }

    private static Impl Implementation(ActualActualConvention c, Schedule? schedule)
    {
        switch (c)
        {
            case ActualActualConvention.ISMA:
            case ActualActualConvention.Bond:
                if (schedule is not null && !schedule.Empty)
                {
                    return new ISMAImpl(schedule);
                }
                else
                {
                    return new OldISMAImpl();
                }

            case ActualActualConvention.ISDA:
            case ActualActualConvention.Historical:
            case ActualActualConvention.Actual365:
                return new ISDAImpl();
            case ActualActualConvention.AFB:
            case ActualActualConvention.Euro:
                return new AFBImpl();
            default:
                throw new ArgumentOutOfRangeException(nameof(c), c, "unknown act/act convention");
        }
    }

    // QuantLib's DayCounter::Impl.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public virtual int DayCount(Date d1, Date d2) => d2 - d1;

        public abstract double YearFraction(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd);
    }

    private sealed class ISMAImpl : Impl
    {
        private readonly Schedule schedule;

        public ISMAImpl(Schedule schedule)
        {
            this.schedule = schedule;
        }

        public override string Name => "Actual/Actual (ISMA)";

        public override double YearFraction(Date d1, Date d2, Date? d3, Date? d4)
        {
            if (d1 == d2)
            {
                return 0.0;
            }
            else if (d2 < d1)
            {
                return -YearFraction(d2, d1, d3, d4);
            }

            List<Date> couponDates = ActualActualHelpers.GetListOfPeriodDatesIncludingQuasiPayments(schedule);

            Date firstDate = couponDates.Min();
            Date lastDate = couponDates.Max();

            if (!(d1 >= firstDate && d2 <= lastDate))
            {
                throw new ArgumentException(
                    $"Dates out of range of schedule: date 1: {d1}, date 2: {d2}, first date: {firstDate}, "
                    + $"last date: {lastDate}",
                    d1 >= firstDate ? nameof(d2) : nameof(d1));
            }

            double yearFractionSum = 0.0;
            for (int i = 0; i < couponDates.Count - 1; i++)
            {
                Date startReferencePeriod = couponDates[i];
                Date endReferencePeriod = couponDates[i + 1];
                if (d1 < endReferencePeriod && d2 > startReferencePeriod)
                {
                    yearFractionSum +=
                        ActualActualHelpers.YearFractionWithReferenceDates(
                            DayCount,
                            d1 < startReferencePeriod ? startReferencePeriod : d1,
                            endReferencePeriod < d2 ? endReferencePeriod : d2,
                            startReferencePeriod,
                            endReferencePeriod);
                }
            }

            return yearFractionSum;
        }
    }

    // QuantLib's ActualActual::Old_ISMA_Impl, used by the ISMA and Bond conventions without a schedule.
    private sealed class OldISMAImpl : Impl
    {
        public override string Name => "Actual/Actual (ISMA)";

        public override double YearFraction(Date d1, Date d2, Date? d3, Date? d4)
        {
            if (d1 == d2)
            {
                return 0.0;
            }

            if (d1 > d2)
            {
                return -YearFraction(d2, d1, d3, d4);
            }

            // when the reference period is not specified, try taking
            // it equal to (d1,d2)
            Date refPeriodStart = d3 ?? d1;
            Date refPeriodEnd = d4 ?? d2;

            if (!(refPeriodEnd > refPeriodStart && refPeriodEnd > d1))
            {
                throw new ArgumentException(
                    $"invalid reference period: date 1: {d1}, date 2: {d2}, reference period start: {refPeriodStart}, "
                    + $"reference period end: {refPeriodEnd}",
                    nameof(refPeriodEnd));
            }

            // estimate roughly the length in months of a period
            int months = (int)Math.Round(
                12 * (double)(refPeriodEnd - refPeriodStart) / 365, MidpointRounding.AwayFromZero);

            // for short periods...
            if (months == 0)
            {
                // ...take the reference period as 1 year from d1
                refPeriodStart = d1;
                refPeriodEnd = d1 + new Period(1, TimeUnit.Years);
                months = 12;
            }

            double period = months / 12.0;

            if (d2 <= refPeriodEnd)
            {
                // here refPeriodEnd is a future (notional?) payment date
                if (d1 >= refPeriodStart)
                {
                    // here refPeriodStart is the last (maybe notional)
                    // payment date.
                    // refPeriodStart <= d1 <= d2 <= refPeriodEnd
                    // [maybe the equality should be enforced, since
                    // refPeriodStart < d1 <= d2 < refPeriodEnd
                    // could give wrong results] ???
                    return period * DateFunctions.DaysBetween(d1, d2)
                        / DateFunctions.DaysBetween(refPeriodStart, refPeriodEnd);
                }
                else
                {
                    // here refPeriodStart is the next (maybe notional)
                    // payment date and refPeriodEnd is the second next
                    // (maybe notional) payment date.
                    // d1 < refPeriodStart < refPeriodEnd
                    // AND d2 <= refPeriodEnd
                    // this case is long first coupon

                    // the last notional payment date
                    Date previousRef = refPeriodStart - new Period(months, TimeUnit.Months);

                    if (d2 > refPeriodStart)
                    {
                        return YearFraction(d1, refPeriodStart, previousRef, refPeriodStart)
                            + YearFraction(refPeriodStart, d2, refPeriodStart, refPeriodEnd);
                    }
                    else
                    {
                        return YearFraction(d1, d2, previousRef, refPeriodStart);
                    }
                }
            }
            else
            {
                // here refPeriodEnd is the last (notional?) payment date
                // d1 < refPeriodEnd < d2 AND refPeriodStart < refPeriodEnd
                if (!(refPeriodStart <= d1))
                {
                    throw new ArgumentException(
                        "invalid dates: d1 < refPeriodStart < refPeriodEnd < d2", nameof(refPeriodStart));
                }

                // now it is: refPeriodStart <= d1 < refPeriodEnd < d2

                // the part from d1 to refPeriodEnd
                double sum = YearFraction(d1, refPeriodEnd, refPeriodStart, refPeriodEnd);

                // the part from refPeriodEnd to d2
                // count how many regular periods are in [refPeriodEnd, d2],
                // then add the remaining time
                int i = 0;
                Date newRefStart, newRefEnd;
                for (; ;)
                {
                    newRefStart = refPeriodEnd + new Period(months * i, TimeUnit.Months);
                    newRefEnd = refPeriodEnd + new Period(months * (i + 1), TimeUnit.Months);
                    if (d2 < newRefEnd)
                    {
                        break;
                    }
                    else
                    {
                        sum += period;
                        i++;
                    }
                }

                sum += YearFraction(newRefStart, d2, newRefStart, newRefEnd);
                return sum;
            }
        }
    }

    private sealed class ISDAImpl : Impl
    {
        public override string Name => "Actual/Actual (ISDA)";

        public override double YearFraction(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd)
        {
            if (d1 == d2)
            {
                return 0.0;
            }

            if (d1 > d2)
            {
                return -YearFraction(d2, d1, null, null);
            }

            int y1 = d1.Year, y2 = d2.Year;
            double dib1 = Date.IsLeap(y1) ? 366.0 : 365.0,
                dib2 = Date.IsLeap(y2) ? 366.0 : 365.0;

            double sum = y2 - y1 - 1;
            sum += DateFunctions.DaysBetween(d1, new Date(1, Month.January, y1 + 1)) / dib1;
            sum += DateFunctions.DaysBetween(new Date(1, Month.January, y2), d2) / dib2;
            return sum;
        }
    }

    private sealed class AFBImpl : Impl
    {
        public override string Name => "Actual/Actual (AFB)";

        public override double YearFraction(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd)
        {
            if (d1 == d2)
            {
                return 0.0;
            }

            if (d1 > d2)
            {
                return -YearFraction(d2, d1, null, null);
            }

            Date newD2 = d2, temp = d2;
            double sum = 0.0;
            while (temp > d1)
            {
                temp = newD2 - new Period(1, TimeUnit.Years);
                if (temp.DayOfMonth == 28 && temp.Month == Month.February
                    && Date.IsLeap(temp.Year))
                {
                    temp += 1;
                }

                if (temp >= d1)
                {
                    sum += 1.0;
                    newD2 = temp;
                }
            }

            double den = 365.0;

            if (Date.IsLeap(newD2.Year))
            {
                temp = new Date(29, Month.February, newD2.Year);
                if (newD2 > temp && d1 <= temp)
                {
                    den += 1.0;
                }
            }
            else if (Date.IsLeap(d1.Year))
            {
                temp = new Date(29, Month.February, d1.Year);
                if (newD2 > temp && d1 <= temp)
                {
                    den += 1.0;
                }
            }

            return sum + (DateFunctions.DaysBetween(d1, newD2) / den);
        }
    }
}

// QuantLib's anonymous-namespace helpers in actualactual.cpp. QuantLib passes the first and the last one the
// implementation, whose day count they call; here they take that day count.
file static class ActualActualHelpers
{
    public static int FindCouponsPerYear(Func<Date, Date, int> dayCount, Date refStart, Date refEnd)
    {
        // This will only work for day counts longer than 15 days.
        int months = (int)Math.Round(12 * (double)dayCount(refStart, refEnd) / 365.0, MidpointRounding.AwayFromZero);
        return (int)Math.Round(12.0 / months, MidpointRounding.AwayFromZero);
    }

    public static List<Date> GetListOfPeriodDatesIncludingQuasiPayments(Schedule schedule)
    {
        // Process the schedule into an array of dates.
        Date issueDate = schedule.Date(0);
        List<Date> newDates = [.. schedule.Dates];

        // QuantLib's empty calendar, null in a schedule built on it, is used unchecked.
        Calendar calendar = schedule.Calendar ?? EmptyCalendar.Instance;

        if (!schedule.HasIsRegular || !schedule.IsRegular(1))
        {
            Date firstCoupon = schedule.Date(1);

            Date notionalFirstCoupon =
                calendar.Advance(
                    firstCoupon,
                    -schedule.Tenor(),
                    schedule.BusinessDayConvention,
                    schedule.EndOfMonth());

            newDates[0] = notionalFirstCoupon;

            // long first coupon
            if (notionalFirstCoupon > issueDate)
            {
                Date priorNotionalCoupon =
                    calendar.Advance(
                        notionalFirstCoupon,
                        -schedule.Tenor(),
                        schedule.BusinessDayConvention,
                        schedule.EndOfMonth());
                newDates.Insert(0, priorNotionalCoupon); // insert as the first element?
            }
        }

        if (!schedule.HasIsRegular || !schedule.IsRegular(schedule.Size - 1))
        {
            Date notionalLastCoupon =
                calendar.Advance(
                    schedule.Date(schedule.Size - 2),
                    schedule.Tenor(),
                    schedule.BusinessDayConvention,
                    schedule.EndOfMonth());

            // As in QuantLib, this index is the schedule's, also after an insertion above.
            newDates[schedule.Size - 1] = notionalLastCoupon;

            if (notionalLastCoupon < schedule.EndDate())
            {
                Date nextNotionalCoupon =
                    calendar.Advance(
                        notionalLastCoupon,
                        schedule.Tenor(),
                        schedule.BusinessDayConvention,
                        schedule.EndOfMonth());
                newDates.Add(nextNotionalCoupon);
            }
        }

        return newDates;
    }

    public static double YearFractionWithReferenceDates(
        Func<Date, Date, int> dayCount, Date d1, Date d2, Date d3, Date d4)
    {
        if (!(d1 <= d2))
        {
            throw new NanoQLException($"This function is only correct if d1 <= d2\nd1: {d1} d2: {d2}");
        }

        double referenceDayCount = dayCount(d3, d4);

        // guess how many coupon periods per year:
        int couponsPerYear;
        if (referenceDayCount < 16)
        {
            couponsPerYear = 1;
            referenceDayCount = dayCount(d1, d1 + new Period(1, TimeUnit.Years));
        }
        else
        {
            couponsPerYear = FindCouponsPerYear(dayCount, d3, d4);
        }

        return dayCount(d1, d2) / (referenceDayCount * couponsPerYear);
    }
}
