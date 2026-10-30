// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/ratehelpers.hpp ql/termstructures/yield/ratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over FRA rates: the index fixing the curve forecasts for the FRA's start, or, without
/// the indexed coupon, the simple forward rate between its start and maturity dates.
/// </summary>
/// <remarks>
/// <para>
/// The FRA starts a period, or a number of IMM dates on the main cycle, after the spot date, which is counted from the
/// evaluation date by the index's fixing days even when the evaluation date isn't a business day; or between explicit
/// start and end dates, which don't follow the evaluation date. QuantLib checks neither that the end IMM offset is after
/// the start one nor that the end date is after the start date, and nor does this helper.
/// </para>
/// <para>
/// <c>useIndexedCoupon</c> is the helper's own choice, read nowhere else: with it, the rate is the index fixing on the
/// curve, forecast even on the evaluation date, and the latest relevant date is the index's maturity from the start;
/// without it, the simple rate over the year fraction between the start and maturity dates, which is the latest
/// relevant date. Bound to a curve, its copy forecasts on a clone of the index on that curve.
/// </para>
/// </remarks>
public sealed class FraRateHelper : RelativeDateRateHelper
{
    private readonly Period? periodToStart;
    private readonly int? immOffsetStart;
    private readonly int? immOffsetEnd;
    private readonly PillarChoice pillarChoice;
    private readonly Date? customPillarDate;
    private readonly bool useIndexedCoupon;

    // Written by the constructor, and on a fresh copy only: the index cloned on the bound curve, the fixing date and
    // the spanning time when the helper is re-dated.
    private IborIndex iborIndex;
    private Date fixingDate;
    private double spanningTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="FraRateHelper"/> class for an FRA of the given conventions, from a
    /// number of months after the spot date to another.
    /// </summary>
    /// <param name="rate">The FRA rate.</param>
    /// <param name="monthsToStart">The months from the spot date to the start.</param>
    /// <param name="monthsToEnd">The months from the spot date to the end, more than <paramref name="monthsToStart"/>.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the dates follow the end-of-month rule.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="useIndexedCoupon">Whether the rate is the index fixing rather than the simple forward rate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="calendar"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A month count or <paramref name="fixingDays"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="monthsToEnd"/> isn't more than <paramref name="monthsToStart"/>, checked once the dates are
    /// computed, as QuantLib checks it; <paramref name="customPillarDate"/> is <c>default(Date)</c>; or, for a custom
    /// pillar, it is outside the FRA's earliest and latest relevant dates.
    /// </exception>
    public FraRateHelper(
        double rate,
        int monthsToStart,
        int monthsToEnd,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        EvaluationContext context,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool useIndexedCoupon = true)
        : this(
            rate,
            new Period(monthsToStart, TimeUnit.Months),
            monthsToEnd - monthsToStart,
            fixingDays,
            calendar,
            convention,
            endOfMonth,
            dayCounter,
            context,
            pillar,
            customPillarDate,
            useIndexedCoupon,
            (monthsToStart, monthsToEnd))
    {
        // QuantLib's check, after the delegated constructor built the dates on the length the months give
        if (!(monthsToEnd > monthsToStart))
        {
            throw new ArgumentException(
                $"monthsToEnd ({monthsToEnd}) must be grater than monthsToStart ({monthsToStart})",
                nameof(monthsToEnd));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FraRateHelper"/> class for an FRA on an index's conventions, from a
    /// number of months after the spot date for the index's tenor.
    /// </summary>
    /// <param name="rate">The FRA rate.</param>
    /// <param name="monthsToStart">The months from the spot date to the start.</param>
    /// <param name="iborIndex">The index whose conventions the FRA follows.</param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="useIndexedCoupon">Whether the rate is the index fixing rather than the simple forward rate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="iborIndex"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="monthsToStart"/> is negative, or <paramref name="pillar"/> isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="customPillarDate"/> is <c>default(Date)</c>, or, for a custom pillar, outside the FRA's earliest
    /// and latest relevant dates.
    /// </exception>
    public FraRateHelper(
        double rate,
        int monthsToStart,
        IborIndex iborIndex,
        EvaluationContext context,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool useIndexedCoupon = true)
        : this(rate, monthsToStart, null, null, null, iborIndex, context, pillar, customPillarDate, useIndexedCoupon)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FraRateHelper"/> class for an FRA of the given conventions, from a
    /// period after the spot date for a number of months.
    /// </summary>
    /// <param name="rate">The FRA rate.</param>
    /// <param name="periodToStart">The period from the spot date to the start.</param>
    /// <param name="lengthInMonths">The FRA's length in months.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the dates follow the end-of-month rule.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="useIndexedCoupon">Whether the rate is the index fixing rather than the simple forward rate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="calendar"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="lengthInMonths"/> or <paramref name="fixingDays"/> is negative, or an enum argument isn't a member
    /// of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="customPillarDate"/> is <c>default(Date)</c>, or, for a custom pillar, outside the FRA's earliest
    /// and latest relevant dates; or the period to start can't be added to the length (weeks and months).
    /// </exception>
    public FraRateHelper(
        double rate,
        Period periodToStart,
        int lengthInMonths,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        EvaluationContext context,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool useIndexedCoupon = true)
        : this(
            rate,
            periodToStart,
            lengthInMonths,
            fixingDays,
            calendar,
            convention,
            endOfMonth,
            dayCounter,
            context,
            pillar,
            customPillarDate,
            useIndexedCoupon,
            null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FraRateHelper"/> class for an FRA on an index's conventions, from a
    /// period after the spot date for the index's tenor.
    /// </summary>
    /// <param name="rate">The FRA rate.</param>
    /// <param name="periodToStart">The period from the spot date to the start.</param>
    /// <param name="iborIndex">The index whose conventions the FRA follows.</param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="useIndexedCoupon">Whether the rate is the index fixing rather than the simple forward rate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="iborIndex"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pillar"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="customPillarDate"/> is <c>default(Date)</c>, or, for a custom pillar, outside the FRA's earliest
    /// and latest relevant dates; or the period to start can't be added to the index's tenor (weeks and months).
    /// </exception>
    public FraRateHelper(
        double rate,
        Period periodToStart,
        IborIndex iborIndex,
        EvaluationContext context,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool useIndexedCoupon = true)
        : this(rate, null, periodToStart, null, null, iborIndex, context, pillar, customPillarDate, useIndexedCoupon)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FraRateHelper"/> class for an FRA on an index's conventions, from a
    /// number of IMM dates on the main cycle after the spot date to another, each adjusted on the index's fixing
    /// calendar; QuantLib doesn't check that the end is after the start.
    /// </summary>
    /// <param name="rate">The FRA rate.</param>
    /// <param name="immOffsetStart">The IMM dates from the spot date to the start; 0 for the spot date.</param>
    /// <param name="immOffsetEnd">The IMM dates from the spot date to the end.</param>
    /// <param name="iborIndex">The index whose conventions the FRA follows.</param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="useIndexedCoupon">Whether the rate is the index fixing rather than the simple forward rate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="iborIndex"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An IMM offset is negative, or <paramref name="pillar"/> isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="customPillarDate"/> is <c>default(Date)</c>, or, for a custom pillar, outside the FRA's earliest
    /// and latest relevant dates.
    /// </exception>
    public FraRateHelper(
        double rate,
        int immOffsetStart,
        int immOffsetEnd,
        IborIndex iborIndex,
        EvaluationContext context,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool useIndexedCoupon = true)
        : this(rate, null, null, immOffsetStart, immOffsetEnd, iborIndex, context, pillar, customPillarDate, useIndexedCoupon)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FraRateHelper"/> class for an FRA on an index's conventions between
    /// explicit start and end dates; its dates don't follow the evaluation date.
    /// </summary>
    /// <param name="rate">The FRA rate.</param>
    /// <param name="startDate">The FRA's start date.</param>
    /// <param name="endDate">The FRA's end date.</param>
    /// <param name="iborIndex">The index whose conventions the FRA follows.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="useIndexedCoupon">Whether the rate is the index fixing rather than the simple forward rate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="iborIndex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pillar"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>, or, for a custom pillar, the pillar date is outside the FRA's earliest and latest
    /// relevant dates.
    /// </exception>
    public FraRateHelper(
        double rate,
        Date startDate,
        Date endDate,
        IborIndex iborIndex,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool useIndexedCoupon = true)
        : base(rate, null, false)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        ArgumentNullException.ThrowIfNull(iborIndex);
        CheckPillar(pillar, customPillarDate);
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.useIndexedCoupon = useIndexedCoupon;

        // take fixing into account
        this.iborIndex = iborIndex.Clone(null);
        earliestDate = startDate;
        maturityDate = endDate;
        SetDates();
    }

    // QuantLib's constructor from a period and a length, which the months constructor delegates to with the length
    // its months give, negative where they are out of order (QuantLib's unsigned difference wraps to it): the months'
    // own counts are refused when negative, and their order is checked by the caller after this constructor.
    private FraRateHelper(
        double rate,
        Period periodToStart,
        int lengthInMonths,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        EvaluationContext context,
        PillarChoice pillar,
        Date? customPillarDate,
        bool useIndexedCoupon,
        (int Start, int End)? months)
        : base(rate, context)
    {
        if (months is (int monthsToStart, int monthsToEnd))
        {
            ArgumentOutOfRangeException.ThrowIfNegative(monthsToStart);
            ArgumentOutOfRangeException.ThrowIfNegative(monthsToEnd);
        }
        else
        {
            ArgumentOutOfRangeException.ThrowIfNegative(lengthInMonths);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(convention, nameof(convention));
        ArgumentNullException.ThrowIfNull(dayCounter);
        CheckPillar(pillar, customPillarDate);
        this.periodToStart = periodToStart;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.useIndexedCoupon = useIndexedCoupon;

        // no way to take fixing into account,
        // even if we would like to for FRA over today
        iborIndex = new IborIndex(
            "no-fix", // correct family name would be needed
            new Period(lengthInMonths, TimeUnit.Months),
            fixingDays,
            null,
            calendar,
            convention,
            endOfMonth,
            dayCounter);
        InitializeDates(context);
    }

    // QuantLib's constructors on an index, from months, a period or IMM offsets to the start.
    private FraRateHelper(
        double rate,
        int? monthsToStart,
        Period? periodToStart,
        int? immOffsetStart,
        int? immOffsetEnd,
        IborIndex iborIndex,
        EvaluationContext context,
        PillarChoice pillar,
        Date? customPillarDate,
        bool useIndexedCoupon)
        : base(rate, context)
    {
        if (monthsToStart is int months)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(months, nameof(monthsToStart));
            periodToStart = new Period(months, TimeUnit.Months);
        }

        if (immOffsetStart is int start)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start, nameof(immOffsetStart));
        }

        if (immOffsetEnd is int end)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(end, nameof(immOffsetEnd));
        }

        ArgumentNullException.ThrowIfNull(iborIndex);
        CheckPillar(pillar, customPillarDate);
        this.periodToStart = periodToStart;
        this.immOffsetStart = immOffsetStart;
        this.immOffsetEnd = immOffsetEnd;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.useIndexedCoupon = useIndexedCoupon;

        // take fixing into account
        this.iborIndex = iborIndex.Clone(null);
        InitializeDates(context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The fixing date isn't a valid fixing date of the index.</exception>
    /// <exception cref="NanoQLException">A date is outside the curve's range.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        if (useIndexedCoupon)
        {
            // The fixing can be historical on a non-business evaluation date.
            // Always derive the quote from the curve being bootstrapped
            // (QuantLib's fix after v1.44, #2899).
            return iborIndex.ForecastFixing(fixingDate, context);
        }

        YieldTermStructure termStructure = TermStructure!;
        return ((termStructure.Discount(earliestDate!.Value) / termStructure.Discount(maturityDate!.Value)) - 1.0)
            / spanningTime;
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (FraRateHelper)base.WithTermStructure(t);
        copy.iborIndex = iborIndex.Clone(t);
        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // fixing days are counted from the actual evaluation date,
        // even when it is not a business day (see issue #753)
        Calendar fixingCalendar = iborIndex.FixingCalendar;
        Date spotDate = fixingCalendar.Advance(context.EvaluationDate, iborIndex.FixingDays, TimeUnit.Days);
        if (periodToStart is Period start)
        {
            earliestDate = fixingCalendar.Advance(
                spotDate, start, iborIndex.BusinessDayConvention, iborIndex.EndOfMonth);

            // maturity date is calculated from spot date
            maturityDate = fixingCalendar.Advance(
                spotDate, start + iborIndex.Tenor, iborIndex.BusinessDayConvention, iborIndex.EndOfMonth);
        }
        else
        {
            // the IMM offsets, which every constructor without a period to start gives
            earliestDate = fixingCalendar.Adjust(FraRateHelperDates.NthImmDate(spotDate, immOffsetStart!.Value));
            maturityDate = fixingCalendar.Adjust(FraRateHelperDates.NthImmDate(spotDate, immOffsetEnd!.Value));
        }

        SetDates();
    }

    private static void CheckPillar(PillarChoice pillar, Date? customPillarDate)
    {
        // The helper stores the choice and switches on it when it is re-dated, so it checks it on entry.
        EnumArguments.Check(pillar, nameof(pillar));
        Date.ThrowIfDefault(customPillarDate, nameof(customPillarDate));
    }

    private static string Show(Date? d) => d is Date date ? date.ToString() : "null date";

    // initializeDates' dates from the earliest and maturity dates.
    private void SetDates()
    {
        Date earliest = earliestDate!.Value;
        Date maturity = maturityDate!.Value;
        Date lastRelevant;
        if (useIndexedCoupon)
        {
            // latest relevant date is calculated from earliestDate_
            lastRelevant = iborIndex.MaturityDate(earliest);
        }
        else
        {
            lastRelevant = maturity;
            spanningTime = iborIndex.DayCounter.YearFraction(earliest, maturity);
        }

        latestRelevantDate = lastRelevant;

        switch (pillarChoice)
        {
            case PillarChoice.MaturityDate:
                pillarDate = maturity;
                break;
            case PillarChoice.LastRelevantDate:
                pillarDate = lastRelevant;
                break;
            default:
                // pillarDate_ already assigned at construction time
                pillarDate = customPillarDate;
                if (!(customPillarDate is Date custom && custom >= earliest))
                {
                    throw new ArgumentException(
                        $"pillar date ({Show(customPillarDate)}) must be later than or equal to the instrument's "
                        + $"earliest date ({earliest})",
                        nameof(customPillarDate));
                }

                if (!(custom <= lastRelevant))
                {
                    throw new ArgumentException(
                        $"pillar date ({custom}) must be before or equal to the instrument's latest relevant date "
                        + $"({lastRelevant})",
                        nameof(customPillarDate));
                }

                break;
        }

        latestDate = pillarDate; // backward compatibility

        fixingDate = iborIndex.FixingDate(earliest);
    }
}

// QuantLib's anonymous-namespace helper in ratehelpers.cpp that the FRA helper calls.
file static class FraRateHelperDates
{
    public static Date NthImmDate(Date asof, int n)
    {
        Date imm = asof;
        for (int i = 0; i < n; ++i)
        {
            imm = IMM.NextDate(imm, true);
        }

        return imm;
    }
}
