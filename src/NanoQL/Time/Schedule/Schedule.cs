// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2008, 2010, 2011, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009, 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/schedule.hpp ql/time/schedule.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Time;

/// <summary>A payment schedule: a list of dates, with the rule and conventions that generated it.</summary>
/// <remarks>
/// <see cref="Date(int)"/> is a method, so inside this class the <c>Date</c> type is written
/// <c>NanoQL.Time.Date</c> wherever an expression needs it.
/// </remarks>
public sealed class Schedule
{
    private readonly Period? tenor;
    private readonly BusinessDayConvention? terminationDateConvention;
    private readonly DateGenerationRule? rule;
    private readonly bool? endOfMonth;
    private readonly Date? firstDate;
    private readonly Date? nextToLastDate;
    private readonly ImmutableArray<Date> dates;
    private readonly ImmutableArray<bool> isRegular;

    /// <summary>
    /// Initializes a new instance of the <see cref="Schedule"/> class from any list of dates and, optionally, meta
    /// information that client classes can use. Neither the dates nor the meta information is checked for
    /// plausibility in any sense.
    /// </summary>
    /// <param name="dates">The dates.</param>
    /// <param name="calendar">The calendar; a <see cref="NullCalendar"/> when <see langword="null"/>.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="terminationDateConvention">The termination date's business-day convention, if known.</param>
    /// <param name="tenor">The tenor, if known.</param>
    /// <param name="rule">The date-generation rule, if known.</param>
    /// <param name="endOfMonth">The end-of-month flag, if known.</param>
    /// <param name="isRegular">Whether each period is regular: empty, or one flag per period.</param>
    /// <exception cref="ArgumentException"><paramref name="isRegular"/> has the wrong size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A convention or the rule isn't a member of its enum.</exception>
    public Schedule(
        IEnumerable<Date> dates,
        Calendar? calendar = null,
        BusinessDayConvention convention = BusinessDayConvention.Unadjusted,
        BusinessDayConvention? terminationDateConvention = null,
        Period? tenor = null,
        DateGenerationRule? rule = null,
        bool? endOfMonth = null,
        IEnumerable<bool>? isRegular = null)
    {
        ArgumentNullException.ThrowIfNull(dates);
        EnumArguments.Check(convention, nameof(convention));
        if (terminationDateConvention is BusinessDayConvention tdc)
        {
            EnumArguments.Check(tdc, nameof(terminationDateConvention));
        }

        if (rule is DateGenerationRule r)
        {
            EnumArguments.Check(r, nameof(rule));
        }

        this.tenor = tenor;
        Calendar = calendar ?? new NullCalendar();
        BusinessDayConvention = convention;
        this.terminationDateConvention = terminationDateConvention;
        this.rule = rule;
        this.dates = [.. dates];
        foreach (Date d in this.dates)
        {
            NanoQL.Time.Date.ThrowIfDefault(d, nameof(dates));
        }

        this.isRegular = isRegular is null ? [] : [.. isRegular];
        this.endOfMonth = tenor is Period t && !ScheduleFunctions.AllowsEndOfMonth(t) ? false : endOfMonth;
        if (!(this.isRegular.IsEmpty || this.isRegular.Length == this.dates.Length - 1))
        {
            throw new ArgumentException(
                $"isRegular size ({this.isRegular.Length}) must be zero or equal to the number of dates minus 1 "
                + $"({this.dates.Length - 1})",
                nameof(isRegular));
        }
    }

    /// <summary>Initializes a new instance of the <see cref="Schedule"/> class by its generation rule.</summary>
    /// <param name="effectiveDate">
    /// The effective date. When <see langword="null"/>, with no first date and the backward rule, a placeholder is
    /// placed a whole number of years before the termination (or next-to-last) date, from the evaluation date.
    /// </param>
    /// <param name="terminationDate">The termination date.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="calendar">
    /// The calendar, or <see langword="null"/> for QuantLib's empty calendar, which adjusts only by
    /// <see cref="BusinessDayConvention.Unadjusted"/>: a schedule that needs another adjustment, or the end-of-month
    /// one, is refused, and the schedule built answers <see langword="null"/> for <see cref="Calendar"/>.
    /// </param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="terminationDateConvention">The termination date's business-day convention.</param>
    /// <param name="rule">The date-generation rule.</param>
    /// <param name="endOfMonth">Whether dates stay at month end.</param>
    /// <param name="context">The evaluation context, read when the effective date is null.</param>
    /// <param name="firstDate">The first date after the effective date, for an irregular first period.</param>
    /// <param name="nextToLastDate">The date before the termination date, for an irregular last period.</param>
    /// <exception cref="ArgumentException">A date or the tenor is inconsistent with the others.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A convention or the rule isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">
    /// The rule can't generate the schedule, or the calendar is <see langword="null"/> and a date is adjusted.
    /// </exception>
    public Schedule(
        Date? effectiveDate,
        Date terminationDate,
        Period tenor,
        Calendar? calendar,
        BusinessDayConvention convention,
        BusinessDayConvention terminationDateConvention,
        DateGenerationRule rule,
        bool endOfMonth,
        EvaluationContext context,
        Date? firstDate = null,
        Date? nextToLastDate = null)
        : this(
            PlaceEffectiveDate(effectiveDate, terminationDate, rule, firstDate, nextToLastDate, context),
            terminationDate,
            tenor,
            calendar,
            convention,
            terminationDateConvention,
            rule,
            endOfMonth,
            firstDate,
            nextToLastDate)
    {
    }

    // The rule-based constructor with the effective date known: MakeSchedule's conversion, which requires the date,
    // calls it, and so reads no evaluation date.
    internal Schedule(
        Date effectiveDate,
        Date terminationDate,
        Period tenor,
        Calendar? calendar,
        BusinessDayConvention convention,
        BusinessDayConvention terminationDateConvention,
        DateGenerationRule rule,
        bool endOfMonth,
        Date? firstDate,
        Date? nextToLastDate)
    {
        EnumArguments.Check(convention, nameof(convention));
        EnumArguments.Check(terminationDateConvention, nameof(terminationDateConvention));
        EnumArguments.Check(rule, nameof(rule));
        RequireDate(terminationDate, "null termination date", nameof(terminationDate));
        RequireDate(effectiveDate, "null effective date", nameof(effectiveDate));
        NanoQL.Time.Date.ThrowIfDefault(firstDate, nameof(firstDate));
        NanoQL.Time.Date.ThrowIfDefault(nextToLastDate, nameof(nextToLastDate));
        this.tenor = tenor;

        // QuantLib's empty calendar is kept as null, and the dates are generated on it, as QuantLib generates them.
        Calendar = calendar;
        calendar ??= EmptyCalendar.Instance;
        BusinessDayConvention = convention;
        this.terminationDateConvention = terminationDateConvention;
        DateGenerationRule generation = rule;
        this.endOfMonth = ScheduleFunctions.AllowsEndOfMonth(tenor) && endOfMonth;
        this.firstDate = firstDate == effectiveDate ? null : firstDate;
        this.nextToLastDate = nextToLastDate == terminationDate ? null : nextToLastDate;
        bool eom = this.endOfMonth.Value;

        if (!(effectiveDate < terminationDate))
        {
            throw new ArgumentException(
                $"effective date ({effectiveDate}) later than or equal to termination date ({terminationDate})",
                nameof(effectiveDate));
        }

        if (tenor.Length == 0)
        {
            generation = DateGenerationRule.Zero;
        }
        else if (!(tenor.Length > 0))
        {
            throw new ArgumentException($"non positive tenor ({tenor}) not allowed", nameof(tenor));
        }

        if (this.firstDate is NanoQL.Time.Date first)
        {
            switch (generation)
            {
                case DateGenerationRule.Backward:
                case DateGenerationRule.Forward:
                    if (!(first > effectiveDate && first <= terminationDate))
                    {
                        throw new ArgumentException(
                            $"first date ({first}) out of effective-termination date range ({effectiveDate}, {terminationDate}]",
                            nameof(firstDate));
                    }

                    // we should ensure that the above condition is still verified after adjustment
                    break;
                case DateGenerationRule.ThirdWednesday:
                    if (!IMM.IsIMMdate(first, false))
                    {
                        throw new ArgumentException($"first date ({first}) is not an IMM date", nameof(firstDate));
                    }

                    break;
                case DateGenerationRule.Zero:
                case DateGenerationRule.Twentieth:
                case DateGenerationRule.TwentiethIMM:
                case DateGenerationRule.OldCDS:
                case DateGenerationRule.CDS:
                case DateGenerationRule.CDS2015:
                    throw new NanoQLException($"first date incompatible with {generation} date generation rule");
                default:
                    throw new NanoQLException($"unknown rule ({(int)generation})");
            }
        }

        if (this.nextToLastDate is NanoQL.Time.Date nextToLast)
        {
            switch (generation)
            {
                case DateGenerationRule.Backward:
                case DateGenerationRule.Forward:
                    if (!(nextToLast >= effectiveDate && nextToLast < terminationDate))
                    {
                        throw new ArgumentException(
                            $"next to last date ({nextToLast}) out of effective-termination date range [{effectiveDate}, {terminationDate})",
                            nameof(nextToLastDate));
                    }

                    // we should ensure that the above condition is still verified after adjustment
                    break;
                case DateGenerationRule.ThirdWednesday:
                    if (!IMM.IsIMMdate(nextToLast, false))
                    {
                        throw new ArgumentException($"next-to-last date ({nextToLast}) is not an IMM date", nameof(nextToLastDate));
                    }

                    break;
                case DateGenerationRule.Zero:
                case DateGenerationRule.Twentieth:
                case DateGenerationRule.TwentiethIMM:
                case DateGenerationRule.OldCDS:
                case DateGenerationRule.CDS:
                case DateGenerationRule.CDS2015:
                    throw new NanoQLException($"next to last date incompatible with {generation} date generation rule");
                default:
                    throw new NanoQLException($"unknown rule ({(int)generation})");
            }
        }

        // calendar needed for endOfMonth adjustment
        Calendar nullCalendar = new NullCalendar();
        int periods = 1;
        Date? seed = null;
        Date? exitDate = null;
        var dateList = new List<Date>();
        var regular = new List<bool>();
        Period step = tenor;
        switch (generation)
        {
            case DateGenerationRule.Zero:
                step = new Period(0, TimeUnit.Years);
                this.tenor = step;
                dateList.Add(effectiveDate);
                dateList.Add(terminationDate);
                regular.Add(true);
                break;

            case DateGenerationRule.Backward:
            {
                dateList.Add(terminationDate);
                Date current = terminationDate;
                if (this.nextToLastDate is NanoQL.Time.Date ntl)
                {
                    dateList.Add(ntl);
                    Date temp = nullCalendar.Advance(current, -periods * step, convention, eom);
                    regular.Add(temp == ntl);
                    current = ntl;
                }

                Date exit = this.firstDate ?? effectiveDate;
                exitDate = exit;
                for (; ;)
                {
                    Date temp = nullCalendar.Advance(current, -periods * step, convention, eom);
                    if (temp < exit)
                    {
                        if (this.firstDate is NanoQL.Time.Date fd
                            && calendar.Adjust(dateList[^1], convention) != calendar.Adjust(fd, convention))
                        {
                            dateList.Add(fd);
                            regular.Add(nullCalendar.Advance(dateList[^2], -1 * step, convention, eom) == fd);
                        }

                        break;
                    }

                    // skip dates that would result in duplicates after adjustment
                    if (calendar.Adjust(dateList[^1], convention) != calendar.Adjust(temp, convention))
                    {
                        dateList.Add(temp);
                        regular.Add(true);
                    }

                    ++periods;
                }

                if (calendar.Adjust(dateList[^1], convention) != calendar.Adjust(effectiveDate, convention))
                {
                    dateList.Add(effectiveDate);
                    regular.Add(nullCalendar.Advance(dateList[^2], -1 * step, convention, eom) == effectiveDate);
                }

                dateList.Reverse();
                regular.Reverse();
                seed = current;
                break;
            }

            case DateGenerationRule.Twentieth:
            case DateGenerationRule.TwentiethIMM:
            case DateGenerationRule.ThirdWednesday:
            case DateGenerationRule.ThirdWednesdayInclusive:
            case DateGenerationRule.OldCDS:
            case DateGenerationRule.CDS:
            case DateGenerationRule.CDS2015:
            case DateGenerationRule.Forward:
            {
                if (generation != DateGenerationRule.Forward && eom)
                {
                    throw new ArgumentException(
                        $"endOfMonth convention incompatible with {generation} date generation rule",
                        nameof(endOfMonth));
                }

                if (generation is DateGenerationRule.CDS or DateGenerationRule.CDS2015)
                {
                    Date prev20th = ScheduleFunctions.PreviousTwentieth(effectiveDate, generation);
                    if (calendar.Adjust(prev20th, convention) > effectiveDate)
                    {
                        dateList.Add(prev20th - new Period(3, TimeUnit.Months));
                        regular.Add(true);
                    }

                    dateList.Add(prev20th);
                }
                else
                {
                    dateList.Add(effectiveDate);
                }

                Date current = dateList[^1];
                if (this.firstDate is NanoQL.Time.Date fd)
                {
                    dateList.Add(fd);
                    Date temp = nullCalendar.Advance(current, periods * step, convention, eom);
                    regular.Add(temp == fd);
                    current = fd;
                }
                else if (generation is DateGenerationRule.Twentieth or DateGenerationRule.TwentiethIMM
                         or DateGenerationRule.OldCDS or DateGenerationRule.CDS or DateGenerationRule.CDS2015)
                {
                    Date next20th = TwentiethDates.Next(effectiveDate, generation);
                    if (generation == DateGenerationRule.OldCDS)
                    {
                        // distance rule inforced in natural days
                        const int stubDays = 30;
                        if (next20th - effectiveDate < stubDays)
                        {
                            // +1 will skip this one and get the next
                            next20th = TwentiethDates.Next(next20th + 1, generation);
                        }
                    }

                    if (next20th != effectiveDate)
                    {
                        dateList.Add(next20th);
                        regular.Add(generation is DateGenerationRule.CDS or DateGenerationRule.CDS2015);
                        current = next20th;
                    }
                }

                Date exit = this.nextToLastDate ?? terminationDate;
                exitDate = exit;
                for (; ;)
                {
                    Date temp = nullCalendar.Advance(current, periods * step, convention, eom);
                    if (temp > exit)
                    {
                        if (this.nextToLastDate is NanoQL.Time.Date ntl
                            && calendar.Adjust(dateList[^1], convention) != calendar.Adjust(ntl, convention))
                        {
                            dateList.Add(ntl);
                            regular.Add(nullCalendar.Advance(dateList[^2], 1 * step, convention, eom) == ntl);
                        }

                        break;
                    }

                    // skip dates that would result in duplicates after adjustment
                    if (calendar.Adjust(dateList[^1], convention) != calendar.Adjust(temp, convention))
                    {
                        dateList.Add(temp);
                        regular.Add(true);
                    }

                    ++periods;
                }

                if (calendar.Adjust(dateList[^1], terminationDateConvention)
                    != calendar.Adjust(terminationDate, terminationDateConvention))
                {
                    if (generation is DateGenerationRule.Twentieth or DateGenerationRule.TwentiethIMM
                        or DateGenerationRule.OldCDS or DateGenerationRule.CDS or DateGenerationRule.CDS2015)
                    {
                        dateList.Add(TwentiethDates.Next(terminationDate, generation));
                        regular.Add(true);
                    }
                    else
                    {
                        dateList.Add(terminationDate);
                        regular.Add(false);
                    }
                }

                seed = current;
                break;
            }

            default:
                throw new NanoQLException($"unknown rule ({(int)generation})");
        }

        // adjustments
        if (generation == DateGenerationRule.ThirdWednesday)
        {
            for (int i = 1; i < dateList.Count - 1; ++i)
            {
                dateList[i] = NanoQL.Time.Date.NthWeekday(3, Weekday.Wednesday, dateList[i].Month, dateList[i].Year);
            }
        }
        else if (generation == DateGenerationRule.ThirdWednesdayInclusive)
        {
            for (int i = 0; i < dateList.Count; ++i)
            {
                dateList[i] = NanoQL.Time.Date.NthWeekday(3, Weekday.Wednesday, dateList[i].Month, dateList[i].Year);
            }
        }

        // first date not adjusted for old CDS schedules
        if (convention != BusinessDayConvention.Unadjusted && generation != DateGenerationRule.OldCDS)
        {
            dateList[0] = calendar.Adjust(dateList[0], convention);
        }

        // termination date is NOT adjusted as per ISDA specifications, unless otherwise specified in the
        // confirmation of the deal or unless we're creating a CDS schedule
        if (terminationDateConvention != BusinessDayConvention.Unadjusted
            && generation != DateGenerationRule.CDS
            && generation != DateGenerationRule.CDS2015)
        {
            dateList[^1] = calendar.Adjust(dateList[^1], terminationDateConvention);
        }

        // For the zero rule QuantLib's seed is the null date, and asking whether it is at month end reads the year 1899,
        // which Date::isLeap refuses; so a zero-rule schedule with the end-of-month flag throws, with that message.
        if (eom && seed is null)
        {
            throw new NanoQLException("year outside valid range");
        }

        if (eom && seed is NanoQL.Time.Date s && calendar.IsEndOfMonth(s))
        {
            // adjust to end of month
            for (int i = 1; i < dateList.Count - 1; ++i)
            {
                dateList[i] = calendar.Adjust(NanoQL.Time.Date.EndOfMonth(dateList[i]), convention);
            }
        }
        else
        {
            for (int i = 1; i < dateList.Count - 1; ++i)
            {
                dateList[i] = calendar.Adjust(dateList[i], convention);
            }
        }

        // Final safety checks to remove extra next-to-last date, if necessary. It can happen to be equal or later
        // than the end date due to EOM adjustments (see the Schedule test suite for an example).
        if (dateList.Count >= 2 && dateList[^2] >= dateList[^1])
        {
            // there might be two dates only, then isRegular has size one
            if (regular.Count >= 2)
            {
                regular[^2] = dateList[^2] == dateList[^1];
            }

            dateList[^2] = dateList[^1];
            dateList.RemoveAt(dateList.Count - 1);
            regular.RemoveAt(regular.Count - 1);
        }

        if (dateList.Count >= 2 && dateList[1] <= dateList[0])
        {
            regular[1] = dateList[1] == dateList[0];
            dateList[1] = dateList[0];
            dateList.RemoveAt(0);
            regular.RemoveAt(0);
        }

        if (!(dateList.Count > 1))
        {
            throw new NanoQLException(
                $"degenerate single date ({dateList[0]}) schedule"
                + $"\n seed date: {DateFunctions.LongDate(seed)}"
                + $"\n exit date: {DateFunctions.LongDate(exitDate)}"
                + $"\n effective date: {effectiveDate}"
                + $"\n first date: {DateFunctions.LongDate(firstDate)}"
                + $"\n next to last date: {DateFunctions.LongDate(nextToLastDate)}"
                + $"\n termination date: {terminationDate}"
                + $"\n generation rule: {generation}"
                + $"\n end of month: {(eom ? 1 : 0)}");
        }

        this.rule = generation;
        dates = [.. dateList];
        isRegular = [.. regular];
    }

    private Schedule(
        Schedule source,
        ImmutableArray<Date> dates,
        ImmutableArray<bool> isRegular,
        BusinessDayConvention? terminationDateConvention,
        Date? firstDate,
        Date? nextToLastDate)
    {
        tenor = source.tenor;
        Calendar = source.Calendar;
        BusinessDayConvention = source.BusinessDayConvention;
        rule = source.rule;
        endOfMonth = source.endOfMonth;
        this.dates = dates;
        this.isRegular = isRegular;
        this.terminationDateConvention = terminationDateConvention;
        this.firstDate = firstDate;
        this.nextToLastDate = nextToLastDate;
    }

    /// <summary>Gets the number of dates.</summary>
    public int Size => dates.Length;

    /// <summary>Gets the dates.</summary>
    public ImmutableArray<Date> Dates => dates;

    /// <summary>Gets a value indicating whether the schedule has no dates.</summary>
    public bool Empty => dates.IsEmpty;

    /// <summary>
    /// Gets the calendar: <see langword="null"/>, QuantLib's empty calendar, for a schedule generated on it; a schedule
    /// built from dates without one has a <see cref="NullCalendar"/>.
    /// </summary>
    public Calendar? Calendar { get; }

    /// <summary>Gets the business-day convention.</summary>
    public BusinessDayConvention BusinessDayConvention { get; }

    /// <summary>Gets a value indicating whether the schedule knows its tenor.</summary>
    public bool HasTenor => tenor.HasValue;

    /// <summary>Gets a value indicating whether the schedule knows its termination date's convention.</summary>
    public bool HasTerminationDateBusinessDayConvention => terminationDateConvention.HasValue;

    /// <summary>Gets a value indicating whether the schedule knows its date-generation rule.</summary>
    public bool HasRule => rule.HasValue;

    /// <summary>Gets a value indicating whether the schedule knows its end-of-month flag.</summary>
    public bool HasEndOfMonth => endOfMonth.HasValue;

    /// <summary>Gets a value indicating whether the schedule knows which periods are regular.</summary>
    public bool HasIsRegular => !isRegular.IsEmpty;

    /// <summary>Gets the date at the given index.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> isn't a date of the schedule, where QuantLib reads past its end.
    /// </exception>
    public Date this[int i]
    {
        get
        {
            if ((uint)i >= (uint)dates.Length)
            {
                ThrowIndex(i, dates.Length, nameof(i));
            }

            return dates[i];
        }
    }

    /// <summary>The date at the given index, checked.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is out of range.</exception>
    public Date At(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, dates.Length);
        return dates[i];
    }

    /// <summary>The date at the given index, checked.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is out of range.</exception>
    public Date Date(int i) => At(i);

    /// <summary>The first date.</summary>
    /// <returns>The first date.</returns>
    /// <exception cref="NanoQLException">The schedule is empty.</exception>
    public Date Front() => !dates.IsEmpty ? dates[0] : throw new NanoQLException("no front date for empty schedule");

    /// <summary>The last date.</summary>
    /// <returns>The last date.</returns>
    /// <exception cref="NanoQLException">The schedule is empty.</exception>
    public Date Back() => !dates.IsEmpty ? dates[^1] : throw new NanoQLException("no back date for empty schedule");

    /// <summary>The start date.</summary>
    /// <returns>The first date.</returns>
    /// <exception cref="NanoQLException">The schedule is empty.</exception>
    public Date StartDate() => !dates.IsEmpty ? dates[0] : throw new NanoQLException("empty Schedule: no start date");

    /// <summary>The end date.</summary>
    /// <returns>The last date.</returns>
    /// <exception cref="NanoQLException">The schedule is empty.</exception>
    public Date EndDate() => !dates.IsEmpty ? dates[^1] : throw new NanoQLException("empty Schedule: no end date");

    /// <summary>The tenor.</summary>
    /// <returns>The tenor.</returns>
    /// <exception cref="NanoQLException">The schedule doesn't know it.</exception>
    public Period Tenor() => tenor ?? throw new NanoQLException("full interface (tenor) not available");

    /// <summary>The termination date's business-day convention.</summary>
    /// <returns>The convention.</returns>
    /// <exception cref="NanoQLException">The schedule doesn't know it.</exception>
    public BusinessDayConvention TerminationDateBusinessDayConvention() =>
        terminationDateConvention ?? throw new NanoQLException("full interface (termination date bdc) not available");

    /// <summary>The date-generation rule.</summary>
    /// <returns>The rule.</returns>
    /// <exception cref="NanoQLException">The schedule doesn't know it.</exception>
    public DateGenerationRule Rule() => rule ?? throw new NanoQLException("full interface (rule) not available");

    /// <summary>The end-of-month flag.</summary>
    /// <returns>The flag.</returns>
    /// <exception cref="NanoQLException">The schedule doesn't know it.</exception>
    public bool EndOfMonth() => endOfMonth ?? throw new NanoQLException("full interface (end of month) not available");

    /// <summary>Whether each period is regular.</summary>
    /// <returns>One flag per period.</returns>
    /// <exception cref="NanoQLException">The schedule doesn't know it.</exception>
    public ImmutableArray<bool> IsRegular() =>
        !isRegular.IsEmpty ? isRegular : throw new NanoQLException("full interface (isRegular) not available");

    /// <summary>Whether the given period is regular.</summary>
    /// <param name="i">The period, one-based: period i runs from date i - 1 to date i.</param>
    /// <returns><see langword="true"/> for a regular period.</returns>
    /// <exception cref="NanoQLException">The schedule doesn't know it.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The period is out of range.</exception>
    public bool IsRegular(int i)
    {
        if (!HasIsRegular)
        {
            throw new NanoQLException("full interface (isRegular) not available");
        }

        if (!(i <= isRegular.Length && i > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(i), $"index ({i}) must be in [1, {isRegular.Length}]");
        }

        return isRegular[i - 1];
    }

    /// <summary>The index of the first date on or after the reference date.</summary>
    /// <param name="context">The evaluation context, whose date is the default reference date.</param>
    /// <param name="refDate">The reference date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The index, <see cref="Size"/> when every date is earlier.</returns>
    public int LowerBound(EvaluationContext context, Date? refDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        NanoQL.Time.Date.ThrowIfDefault(refDate, nameof(refDate));
        Date d = refDate ?? context.EvaluationDate;
        int lo = 0, hi = dates.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (dates[mid] < d)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>The first date on or after the reference date.</summary>
    /// <param name="refDate">The reference date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The date, or <see langword="null"/> when every date is earlier.</returns>
    public Date? NextDate(Date? refDate, EvaluationContext context)
    {
        int i = LowerBound(context, refDate);
        return i != dates.Length ? dates[i] : null;
    }

    /// <summary>The last date before the reference date.</summary>
    /// <param name="refDate">The reference date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The date, or <see langword="null"/> when no date is earlier.</returns>
    public Date? PreviousDate(Date? refDate, EvaluationContext context)
    {
        int i = LowerBound(context, refDate);
        return i != 0 ? dates[i - 1] : null;
    }

    /// <summary>The schedule truncated to the dates on or after the given date, which joins it if missing.</summary>
    /// <param name="truncationDate">The truncation date.</param>
    /// <returns>The truncated schedule.</returns>
    /// <exception cref="ArgumentException">The truncation date isn't before the last date.</exception>
    public Schedule After(Date truncationDate)
    {
        NanoQL.Time.Date.ThrowIfDefault(truncationDate, nameof(truncationDate));
        if (!(truncationDate < dates[^1]))
        {
            throw new ArgumentException(
                $"truncation date {truncationDate} must be before the last schedule date {dates[^1]}",
                nameof(truncationDate));
        }

        var result = dates.ToList();
        var regular = isRegular.ToList();
        BusinessDayConvention? tdc = terminationDateConvention;
        Date? first = firstDate, nextToLast = nextToLastDate;
        if (truncationDate > result[0])
        {
            // remove earlier dates
            while (result[0] < truncationDate)
            {
                result.RemoveAt(0);
                if (regular.Count > 0)
                {
                    regular.RemoveAt(0);
                }
            }

            // add truncationDate if missing
            if (truncationDate != result[0])
            {
                result.Insert(0, truncationDate);
                regular.Insert(0, false);
                tdc = BusinessDayConvention.Unadjusted;
            }
            else
            {
                tdc = BusinessDayConvention;
            }

            if (nextToLast <= truncationDate)
            {
                nextToLast = null;
            }

            if (first <= truncationDate)
            {
                first = null;
            }
        }

        return new Schedule(this, [.. result], [.. regular], tdc, first, nextToLast);
    }

    /// <summary>The schedule truncated to the dates on or before the given date, which joins it if missing.</summary>
    /// <param name="truncationDate">The truncation date.</param>
    /// <returns>The truncated schedule.</returns>
    /// <exception cref="ArgumentException">The truncation date isn't after the first date.</exception>
    public Schedule Until(Date truncationDate)
    {
        NanoQL.Time.Date.ThrowIfDefault(truncationDate, nameof(truncationDate));
        if (!(truncationDate > dates[0]))
        {
            throw new ArgumentException(
                $"truncation date {truncationDate} must be later than schedule first date {dates[0]}",
                nameof(truncationDate));
        }

        var result = dates.ToList();
        var regular = isRegular.ToList();
        BusinessDayConvention? tdc = terminationDateConvention;
        Date? first = firstDate, nextToLast = nextToLastDate;
        if (truncationDate < result[^1])
        {
            // remove later dates
            while (result[^1] > truncationDate)
            {
                result.RemoveAt(result.Count - 1);
                if (regular.Count > 0)
                {
                    regular.RemoveAt(regular.Count - 1);
                }
            }

            // add truncationDate if missing
            if (truncationDate != result[^1])
            {
                result.Add(truncationDate);
                regular.Add(false);
                tdc = BusinessDayConvention.Unadjusted;
            }
            else
            {
                tdc = BusinessDayConvention;
            }

            if (nextToLast >= truncationDate)
            {
                nextToLast = null;
            }

            if (first >= truncationDate)
            {
                first = null;
            }
        }

        return new Schedule(this, [.. result], [.. regular], tdc, first, nextToLast);
    }

    /// <summary>Returns an enumerator over the dates, so that <c>foreach</c> works.</summary>
    /// <returns>The enumerator.</returns>
    public ImmutableArray<Date>.Enumerator GetEnumerator() => dates.GetEnumerator();

    private static void RequireDate(Date d, string message, string paramName)
    {
        if (d.SerialNumber == 0)
        {
            throw new ArgumentException(message, paramName);
        }
    }

    // In many cases (e.g. non-expired bonds) the effective date is not really necessary. In these cases a decent
    // placeholder is enough.
    private static Date PlaceEffectiveDate(
        Date? effectiveDate,
        Date terminationDate,
        DateGenerationRule rule,
        Date? firstDate,
        Date? nextToLastDate,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnumArguments.Check(rule, nameof(rule));
        RequireDate(terminationDate, "null termination date", nameof(terminationDate));
        NanoQL.Time.Date.ThrowIfDefault(firstDate, nameof(firstDate));
        NanoQL.Time.Date.ThrowIfDefault(nextToLastDate, nameof(nextToLastDate));
        if (effectiveDate is NanoQL.Time.Date given)
        {
            return given;
        }

        if (firstDate is null && rule == DateGenerationRule.Backward)
        {
            Date evalDate = context.EvaluationDate;
            if (!(evalDate < terminationDate))
            {
                throw new ArgumentException("null effective date", nameof(effectiveDate));
            }

            int y;
            if (nextToLastDate is NanoQL.Time.Date ntl)
            {
                y = ((ntl - evalDate) / 366) + 1;
                return ntl - new Period(y, TimeUnit.Years);
            }

            y = ((terminationDate - evalDate) / 366) + 1;
            return terminationDate - new Period(y, TimeUnit.Years);
        }

        throw new ArgumentException("null effective date", nameof(effectiveDate));
    }

    // An index past the array an accessor reads: QuantLib's Array::at text, in a helper, so the accessors
    // stay small enough to inline.
    [DoesNotReturn]
    private static void ThrowIndex(int index, int length, string paramName) =>
        throw new ArgumentOutOfRangeException(
            paramName, $"index ({index}) must be less than {length}: array access out of range");
}

// QuantLib's anonymous-namespace helper in schedule.cpp.
file static class TwentiethDates
{
    public static Date Next(Date d, DateGenerationRule rule)
    {
        var result = new Date(20, d.Month, d.Year);
        if (result < d)
        {
            result += new Period(1, TimeUnit.Months);
        }

        if (rule is DateGenerationRule.TwentiethIMM or DateGenerationRule.OldCDS or DateGenerationRule.CDS
            or DateGenerationRule.CDS2015)
        {
            int m = (int)result.Month;
            if (m % 3 != 0)
            {
                // not a main IMM month
                int skip = 3 - (m % 3);
                result += new Period(skip, TimeUnit.Months);
            }
        }

        return result;
    }
}
