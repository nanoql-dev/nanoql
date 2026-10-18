// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/interestrateindex.hpp ql/indexes/interestrateindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class of interest-rate indexes.</summary>
public abstract class InterestRateIndex : FinancialIndex
{
    private readonly string name;
    private readonly Calendar fixingCalendar;

    /// <summary>Initializes a new instance of the <see cref="InterestRateIndex"/> class.</summary>
    /// <param name="familyName">The family name, such as "Euribor".</param>
    /// <param name="tenor">The tenor; a whole number of years given in months becomes years.</param>
    /// <param name="settlementDays">The fixing days: the business days from a fixing to its value date.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="fixingCalendar">The calendar defining valid fixing dates.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    protected InterestRateIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(fixingCalendar);
        ArgumentNullException.ThrowIfNull(dayCounter);
        FamilyName = familyName;
        FixingDays = settlementDays;
        Currency = currency;
        DayCounter = dayCounter;
        this.fixingCalendar = fixingCalendar;

        // tenor.Normalized() does too much; we want to leave days alone
        Tenor = tenor.Units == TimeUnit.Months && tenor.Length % 12 == 0
            ? new Period(tenor.Length / 12, TimeUnit.Years)
            : tenor;

        string period = Tenor == new Period(1, TimeUnit.Days)
            ? FixingDays switch
            {
                0 => "ON",
                1 => "TN",
                2 => "SN",
                _ => PeriodFunctions.ShortPeriod(Tenor),
            }
            : PeriodFunctions.ShortPeriod(Tenor);
        name = $"{familyName}{period} {dayCounter.Name}";
    }

    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override Calendar FixingCalendar => fixingCalendar;

    /// <summary>Gets the family name.</summary>
    public string FamilyName { get; }

    /// <summary>Gets the tenor.</summary>
    public Period Tenor { get; }

    /// <summary>Gets the fixing days: the business days from a fixing to its value date.</summary>
    public int FixingDays { get; }

    /// <summary>Gets the currency, or <see langword="null"/> for QuantLib's empty currency.</summary>
    public Currency? Currency { get; }

    /// <summary>Gets the day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <inheritdoc/>
    public override bool IsValidFixingDate(Date fixingDate)
    {
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        return FixingCalendar.IsBusinessDay(fixingDate);
    }

    /// <summary>
    /// The fixing on a date. A date after the evaluation date, or on it with <paramref name="forecastTodaysFixing"/>,
    /// is forecast; a date before it, or on it when the context enforces today's historic fixings, must have a stored
    /// fixing; otherwise today's stored fixing is used if there is one, and forecast if not.
    /// </summary>
    /// <param name="fixingDate">The actual calendar date of the fixing, with no settlement days applied.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="forecastTodaysFixing">Whether a fixing on the evaluation date is forecast even when one is stored.</param>
    /// <returns>The fixing.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date.</exception>
    /// <exception cref="NanoQLException">A fixing that must be stored is missing.</exception>
    public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        if (!IsValidFixingDate(fixingDate))
        {
            throw new ArgumentException($"Fixing date {fixingDate} is not valid", nameof(fixingDate));
        }

        Date today = context.EvaluationDate;
        if (fixingDate > today || (fixingDate == today && forecastTodaysFixing))
        {
            return ForecastFixing(fixingDate, context);
        }

        if (fixingDate < today || context.EnforcesTodaysHistoricFixings)
        {
            // must have been fixed
            // do not catch exceptions
            return PastFixing(fixingDate, context) ?? throw new NanoQLException($"Missing {Name} fixing for {fixingDate}");
        }

        try
        {
            // might have been fixed
            if (PastFixing(fixingDate, context) is double result)
            {
                return result;
            }

            // fall through and forecast
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // fall through and forecast
        }

        return ForecastFixing(fixingDate, context);
    }

    /// <summary>The fixing date of a value date: the fixing days before it, on the fixing calendar.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The fixing date.</returns>
    public virtual Date FixingDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        return FixingCalendar.Advance(valueDate, -FixingDays, TimeUnit.Days);
    }

    /// <summary>The value date of a fixing: the fixing days after it, on the fixing calendar.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <returns>The value date.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date.</exception>
    public virtual Date ValueDate(Date fixingDate)
    {
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        if (!IsValidFixingDate(fixingDate))
        {
            throw new ArgumentException($"{fixingDate} is not a valid fixing date", nameof(fixingDate));
        }

        return FixingCalendar.Advance(fixingDate, FixingDays, TimeUnit.Days);
    }

    /// <summary>The maturity date of a value date.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The maturity date.</returns>
    public abstract Date MaturityDate(Date valueDate);

    /// <summary>The forecast fixing on a date.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, which an index forecasting from other fixings reads.</param>
    /// <returns>The forecast.</returns>
    public abstract double ForecastFixing(Date fixingDate, EvaluationContext context);
}
