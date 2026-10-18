// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2008, 2010, 2011, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009, 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/schedule.hpp ql/time/schedule.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>A more comfortable interface to <see cref="Schedule"/>'s rule-based constructor.</summary>
/// <remarks>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToSchedule"/> builds the schedule, in place of QuantLib's conversion operator.
/// </remarks>
public sealed record MakeSchedule
{
    private Calendar? Calendar { get; init; }

    private Date? EffectiveDate { get; init; }

    private Date? TerminationDate { get; init; }

    private Period? Tenor { get; init; }

    private BusinessDayConvention? Convention { get; init; }

    private BusinessDayConvention? TerminationDateConvention { get; init; }

    private DateGenerationRule Rule { get; init; } = DateGenerationRule.Backward;

    private bool EndOfMonthFlag { get; init; }

    private Date? FirstDate { get; init; }

    private Date? NextToLastDate { get; init; }

    /// <summary>Sets the effective date.</summary>
    /// <param name="effectiveDate">The effective date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeSchedule From(Date effectiveDate)
    {
        Date.ThrowIfDefault(effectiveDate, nameof(effectiveDate));
        return this with { EffectiveDate = effectiveDate };
    }

    /// <summary>Sets the termination date.</summary>
    /// <param name="terminationDate">The termination date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeSchedule To(Date terminationDate)
    {
        Date.ThrowIfDefault(terminationDate, nameof(terminationDate));
        return this with { TerminationDate = terminationDate };
    }

    /// <summary>Sets the tenor.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public MakeSchedule WithTenor(Period tenor) => this with { Tenor = tenor };

    /// <summary>Sets the tenor from a frequency.</summary>
    /// <param name="frequency">The frequency.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The frequency is <see cref="Frequency.OtherFrequency"/> or not a member of the enum.
    /// </exception>
    public MakeSchedule WithFrequency(Frequency frequency) =>
        this with { Tenor = Period.FromFrequency(frequency, nameof(frequency)) };

    /// <summary>Sets the calendar.</summary>
    /// <param name="calendar">
    /// The calendar, or <see langword="null"/>, QuantLib's empty calendar, for none: the schedule is then built on a
    /// <see cref="NullCalendar"/>, with <see cref="BusinessDayConvention.Unadjusted"/> as the default convention.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeSchedule WithCalendar(Calendar? calendar) => this with { Calendar = calendar };

    /// <summary>Sets the business-day convention.</summary>
    /// <param name="conv">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeSchedule WithConvention(BusinessDayConvention conv)
    {
        EnumArguments.Check(conv, nameof(conv));
        return this with { Convention = conv };
    }

    /// <summary>Sets the termination date's business-day convention.</summary>
    /// <param name="conv">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeSchedule WithTerminationDateConvention(BusinessDayConvention conv)
    {
        EnumArguments.Check(conv, nameof(conv));
        return this with { TerminationDateConvention = conv };
    }

    /// <summary>Sets the date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public MakeSchedule WithRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { Rule = r };
    }

    /// <summary>Generates the dates forward from the effective date.</summary>
    /// <returns>The modified builder.</returns>
    public MakeSchedule Forwards() => this with { Rule = DateGenerationRule.Forward };

    /// <summary>Generates the dates backward from the termination date.</summary>
    /// <returns>The modified builder.</returns>
    public MakeSchedule Backwards() => this with { Rule = DateGenerationRule.Backward };

    /// <summary>Sets the end-of-month flag.</summary>
    /// <param name="flag">Whether dates stay at month end.</param>
    /// <returns>The modified builder.</returns>
    public MakeSchedule EndOfMonth(bool flag = true) => this with { EndOfMonthFlag = flag };

    /// <summary>Sets the first date after the effective date, for an irregular first period.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeSchedule WithFirstDate(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { FirstDate = d };
    }

    /// <summary>Sets the date before the termination date, for an irregular last period.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeSchedule WithNextToLastDate(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { NextToLastDate = d };
    }

    /// <summary>Builds the schedule.</summary>
    /// <returns>The schedule.</returns>
    /// <exception cref="NanoQLException">The effective date, the termination date or the tenor isn't set.</exception>
    public Schedule ToSchedule()
    {
        // check for mandatory arguments
        Date effectiveDate = EffectiveDate ?? throw new NanoQLException("effective date not provided");
        Date terminationDate = TerminationDate ?? throw new NanoQLException("termination date not provided");
        Period tenor = Tenor ?? throw new NanoQLException("tenor/frequency not provided");

        // set dynamic defaults: if a convention was set, we use it; if we set a calendar, we probably want it to be
        // used; if not, we don't care.
        BusinessDayConvention convention = Convention
            ?? (Calendar is not null ? BusinessDayConvention.Following : BusinessDayConvention.Unadjusted);

        // if set explicitly, we use it; otherwise the convention, as per ISDA specification
        BusinessDayConvention terminationDateConvention = TerminationDateConvention ?? convention;

        // if no calendar was set, we use a null one
        Calendar calendar = Calendar ?? new NullCalendar();

        return new Schedule(
            effectiveDate,
            terminationDate,
            tenor,
            calendar,
            convention,
            terminationDateConvention,
            Rule,
            EndOfMonthFlag,
            FirstDate,
            NextToLastDate);
    }
}
