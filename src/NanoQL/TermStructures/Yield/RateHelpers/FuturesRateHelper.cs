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
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over Ibor index futures prices: the price is 100 less the forward rate the curve
/// implies between the start and maturity dates, plus the convexity adjustment, in percent.
/// </summary>
/// <remarks>
/// <para>
/// The helper's dates don't follow the evaluation date, and it reads no other setting, so it takes no context. Its
/// start date must be an IMM or an ASX date for those types, on either cycle; a <see cref="FuturesType.Custom"/> one
/// takes any date. The pillar, latest relevant and latest dates are the maturity date.
/// </para>
/// <para>
/// A <see cref="FuturesType.Custom"/> helper built from dates with no end date keeps a null maturity, as QuantLib's
/// does, and so null pillar, latest relevant and latest dates, which the bootstraps read as expired and drop. QuantLib
/// computes a year fraction from that null date, which some day counters read out of bounds, and only its implied quote
/// reads it, after discounting to the null date; this helper computes none, and its <see cref="ImpliedQuote"/> refuses
/// the null maturity before it reads the curve.
/// </para>
/// </remarks>
public sealed class FuturesRateHelper : RateHelper
{
    // QuantLib's yearFraction_; NaN for a null maturity, where ImpliedQuote refuses before reading it.
    private readonly double yearFraction;

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesRateHelper"/> class for futures on a deposit of a given length
    /// from the start date.
    /// </summary>
    /// <param name="price">The futures price.</param>
    /// <param name="iborStartDate">The deposit's start date.</param>
    /// <param name="lengthInMonths">The deposit's length in months.</param>
    /// <param name="calendar">The calendar the maturity is adjusted on.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the maturity follows the end-of-month rule.</param>
    /// <param name="dayCounter">The day counter of the forward rate.</param>
    /// <param name="convexityAdjustment">The convexity adjustment.</param>
    /// <param name="type">The futures type, which the start date must be a date of.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="calendar"/> or <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="lengthInMonths"/> is negative, or <paramref name="convention"/> or <paramref name="type"/> isn't
    /// a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="iborStartDate"/> is <c>default(Date)</c>, or isn't a date of the futures type.
    /// </exception>
    public FuturesRateHelper(
        double price,
        Date iborStartDate,
        int lengthInMonths,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        double convexityAdjustment = 0.0,
        FuturesType type = FuturesType.IMM)
        : base(price)
    {
        Date.ThrowIfDefault(iborStartDate, nameof(iborStartDate));
        ArgumentOutOfRangeException.ThrowIfNegative(lengthInMonths);
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(convention, nameof(convention));
        ArgumentNullException.ThrowIfNull(dayCounter);
        EnumArguments.Check(type, nameof(type));
        ConvexityAdjustment = convexityAdjustment;
        FuturesRateHelperChecks.CheckDate(iborStartDate, type);

        earliestDate = iborStartDate;
        Date maturity = calendar.Advance(iborStartDate, lengthInMonths, TimeUnit.Months, convention, endOfMonth);
        maturityDate = maturity;
        yearFraction = FuturesRateHelperChecks.DetermineYearFraction(iborStartDate, maturity, dayCounter);
        pillarDate = latestDate = latestRelevantDate = maturityDate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesRateHelper"/> class for futures on a deposit between two
    /// dates.
    /// </summary>
    /// <param name="price">The futures price.</param>
    /// <param name="iborStartDate">The deposit's start date.</param>
    /// <param name="iborEndDate">
    /// The deposit's end date, after the start date; or <see langword="null"/>, QuantLib's <c>Date()</c>, for the third
    /// IMM or ASX date after the start date on either cycle, and for a <see cref="FuturesType.Custom"/> helper a null
    /// maturity, which the bootstraps drop.
    /// </param>
    /// <param name="dayCounter">The day counter of the forward rate.</param>
    /// <param name="convexityAdjustment">The convexity adjustment.</param>
    /// <param name="type">The futures type, which the start date must be a date of.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>; <paramref name="iborStartDate"/> isn't a date of the futures type; or, for an IMM
    /// or ASX helper, <paramref name="iborEndDate"/> is given and isn't after the start date.
    /// </exception>
    public FuturesRateHelper(
        double price,
        Date iborStartDate,
        Date? iborEndDate,
        DayCounter dayCounter,
        double convexityAdjustment = 0.0,
        FuturesType type = FuturesType.IMM)
        : base(price)
    {
        Date.ThrowIfDefault(iborStartDate, nameof(iborStartDate));
        Date.ThrowIfDefault(iborEndDate, nameof(iborEndDate));
        ArgumentNullException.ThrowIfNull(dayCounter);
        EnumArguments.Check(type, nameof(type));
        ConvexityAdjustment = convexityAdjustment;
        FuturesRateHelperChecks.CheckDate(iborStartDate, type);

        Date? maturity = type switch
        {
            FuturesType.IMM => DetermineMaturityDate(iborStartDate, iborEndDate, d => IMM.NextDate(d, false)),
            FuturesType.ASX => DetermineMaturityDate(iborStartDate, iborEndDate, d => ASX.NextDate(d, false)),
            _ => iborEndDate,
        };
        earliestDate = iborStartDate;
        maturityDate = maturity;
        yearFraction = maturity is Date m
            ? FuturesRateHelperChecks.DetermineYearFraction(iborStartDate, m, dayCounter)
            : double.NaN;
        pillarDate = latestDate = latestRelevantDate = maturityDate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesRateHelper"/> class for futures on an index's deposit from
    /// the start date.
    /// </summary>
    /// <param name="price">The futures price.</param>
    /// <param name="iborStartDate">The deposit's start date.</param>
    /// <param name="iborIndex">
    /// The index whose fixing calendar, tenor, convention and day counter the deposit follows; its end-of-month flag
    /// isn't used.
    /// </param>
    /// <param name="convexityAdjustment">The convexity adjustment.</param>
    /// <param name="type">The futures type, which the start date must be a date of.</param>
    /// <exception cref="ArgumentNullException"><paramref name="iborIndex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="iborStartDate"/> is <c>default(Date)</c>, or isn't a date of the futures type.
    /// </exception>
    public FuturesRateHelper(
        double price,
        Date iborStartDate,
        IborIndex iborIndex,
        double convexityAdjustment = 0.0,
        FuturesType type = FuturesType.IMM)
        : base(price)
    {
        Date.ThrowIfDefault(iborStartDate, nameof(iborStartDate));
        ArgumentNullException.ThrowIfNull(iborIndex);
        EnumArguments.Check(type, nameof(type));
        ConvexityAdjustment = convexityAdjustment;
        FuturesRateHelperChecks.CheckDate(iborStartDate, type);

        earliestDate = iborStartDate;
        Calendar cal = iborIndex.FixingCalendar;
        Date maturity = cal.Advance(iborStartDate, iborIndex.Tenor, iborIndex.BusinessDayConvention);
        maturityDate = maturity;
        yearFraction = FuturesRateHelperChecks.DetermineYearFraction(iborStartDate, maturity, iborIndex.DayCounter);
        pillarDate = latestDate = latestRelevantDate = maturityDate;
    }

    /// <summary>Gets the convexity adjustment: QuantLib's <c>convexityAdjustment()</c>.</summary>
    public double ConvexityAdjustment { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The maturity is null (a <see cref="FuturesType.Custom"/> helper built with no end date), or a date is outside the
    /// curve's range.
    /// </exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        YieldTermStructure termStructure = TermStructure!;
        if (maturityDate is not Date maturity)
        {
            // QuantLib discounts to its null maturity, which refuses its negative time or reads out of bounds; the
            // term structure's own text for a date before its reference date
            throw new NanoQLException($"date (null date) before reference date ({termStructure.ReferenceDate})");
        }

        double forwardRate = ((termStructure.Discount(earliestDate!.Value) / termStructure.Discount(maturity)) - 1.0)
            / yearFraction;

        // Convexity, as FRA/futures adjustment, has been used in the
        // past to take into account futures margining vs FRA.
        // Therefore, there's no requirement for it to be non-negative.
        double futureRate = forwardRate + ConvexityAdjustment;
        return 100.0 * (1.0 - futureRate);
    }

    // The lambda in QuantLib's dates constructor: three steps of the futures type's dates for a null end date.
    private static Date DetermineMaturityDate(Date iborStartDate, Date? iborEndDate, Func<Date, Date> nextDateCalculator)
    {
        Date maturity;
        if (iborEndDate is not Date end)
        {
            // advance 3 months
            maturity = nextDateCalculator(iborStartDate);
            maturity = nextDateCalculator(maturity);
            maturity = nextDateCalculator(maturity);
        }
        else
        {
            if (!(end > iborStartDate))
            {
                throw new ArgumentException(
                    $"end date ({end}) must be greater than start date ({iborStartDate})", nameof(iborEndDate));
            }

            maturity = end;
        }

        return maturity;
    }
}

// QuantLib's anonymous-namespace helpers in ratehelpers.cpp that the futures helper calls.
file static class FuturesRateHelperChecks
{
    public static void CheckDate(Date iborStartDate, FuturesType type)
    {
        // the type was checked on entry, so a value outside the members can't reach the switch
        switch (type)
        {
            case FuturesType.IMM:
                if (!IMM.IsIMMdate(iborStartDate, false))
                {
                    throw new ArgumentException($"{iborStartDate} is not a valid IMM date", nameof(iborStartDate));
                }

                break;
            case FuturesType.ASX:
                if (!ASX.IsASXdate(iborStartDate, false))
                {
                    throw new ArgumentException($"{iborStartDate} is not a valid ASX date", nameof(iborStartDate));
                }

                break;
            default:
                break;
        }
    }

    public static double DetermineYearFraction(Date earliestDate, Date maturityDate, DayCounter dayCounter) =>
        dayCounter.YearFraction(earliestDate, maturityDate, earliestDate, maturityDate);
}
