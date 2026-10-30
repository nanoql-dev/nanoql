// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/equityindex.hpp ql/indexes/equityindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>Base class for equity indexes.</summary>
/// <remarks>
/// <para>
/// The equity index object allows to retrieve past fixings, as well as project future fixings using either both the
/// risk free interest rate term structure and the dividend term structure, or just the interest rate term structure
/// in which case one can provide a term structure of equity forwards implied from, e.g. option prices.
/// </para>
/// <para>
/// In case of the first method, the forward is calculated as I(t, T) = I(t, t) P_D(t, T) / P_R(t, T), where I(t, t)
/// is today's value of the index, P_D(t, T) is a discount factor of the dividend curve at future time T, and P_R(t, T)
/// is a discount factor of the risk free curve at future time T. In case of the latter method, the forward is
/// calculated as I(t, T) = I(t, t) / P_F(t, T), where P_F(t, T) is a discount factor of the equity forward term
/// structure.
/// </para>
/// <para>
/// To forecast future fixings, the user can provide the current index spot. If the spot is
/// <see langword="null"/>, today's fixing will be used instead.
/// </para>
/// <para>
/// Each discount factor is read from the evaluation date t: P(t, T) is the curve's discount at T over its discount at
/// t, nothing accruing before the curve's own reference date. QuantLib reads each curve's discount at T from its own
/// reference date (<c>equityindex.cpp:84-88</c>), which is the formula's only where the curve is dated on or after
/// the evaluation date; on such a curve the forecast is QuantLib's bit for bit, and on a curve dated before it a
/// documented divergence.
/// </para>
/// </remarks>
public class EquityIndex : FinancialIndex
{
    private readonly string name;
    private readonly Calendar fixingCalendar;

    /// <summary>Initializes a new instance of the <see cref="EquityIndex"/> class.</summary>
    /// <param name="name">The index's name.</param>
    /// <param name="fixingCalendar">The calendar defining valid fixing dates.</param>
    /// <param name="currency">The index currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="interest">The interest rate curve used to forecast fixings, or <see langword="null"/> for
    /// none.</param>
    /// <param name="dividend">The dividend curve used to forecast fixings, or <see langword="null"/> for none.</param>
    /// <param name="spot">The index spot value, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="fixingCalendar"/> is <see langword="null"/>.
    /// </exception>
    public EquityIndex(
        string name,
        Calendar fixingCalendar,
        Currency? currency,
        YieldTermStructure? interest = null,
        YieldTermStructure? dividend = null,
        double? spot = null)
        : this(name, fixingCalendar, currency, interest, dividend, spot, ConventionProvider.Current)
    {
    }

    // An index keeping a given provider: a clone's source's.
    private EquityIndex(
        string name,
        Calendar fixingCalendar,
        Currency? currency,
        YieldTermStructure? interest,
        YieldTermStructure? dividend,
        double? spot,
        ConventionProvider provider)
        : base(provider)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fixingCalendar);
        this.name = name;
        this.fixingCalendar = fixingCalendar;
        Currency = currency;
        EquityInterestRateCurve = interest;
        EquityDividendCurve = dividend;
        Spot = spot;
    }

    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override Calendar FixingCalendar => fixingCalendar;

    /// <summary>Gets the index currency, or <see langword="null"/> for QuantLib's empty currency.</summary>
    public Currency? Currency { get; }

    /// <summary>Gets the rate curve used to forecast fixings, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? EquityInterestRateCurve { get; }

    /// <summary>Gets the dividend curve used to forecast fixings, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? EquityDividendCurve { get; }

    /// <summary>Gets the index spot value, or <see langword="null"/> for none.</summary>
    public double? Spot { get; }

    /// <inheritdoc/>
    public override bool IsValidFixingDate(Date fixingDate) => fixingCalendar.IsBusinessDay(fixingDate);

    /// <summary>
    /// The fixing on a date. A date after the evaluation date, or on it with <paramref name="forecastTodaysFixing"/>,
    /// is forecast; otherwise the stored fixing is used, or on the evaluation date the spot when no fixing is stored.
    /// </summary>
    /// <param name="fixingDate">The actual calendar date of the fixing.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="forecastTodaysFixing">Whether a fixing on the evaluation date is forecast even when one is
    /// stored.</param>
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

        // if historical fixing is present use it
        if (PastFixing(fixingDate, context) is double result)
        {
            return result;
        }

        // Today's fixing is missing, but spot is
        // provided, so use it as proxy
        if (fixingDate == today && Spot is double spot)
        {
            return spot;
        }

        throw new NanoQLException($"Missing {Name} fixing for {fixingDate}");
    }

    /// <summary>The forecast fixing on a date: the forward of today's value on the curves.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose evaluation date and fixings are read.</param>
    /// <returns>The forecast fixing.</returns>
    /// <exception cref="NanoQLException">
    /// No interest rate curve is set, or neither a spot nor a fixing on the last fixing date is available.
    /// </exception>
    /// <remarks>An extension point: it can be overridden to implement particular conventions.</remarks>
    public virtual double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        YieldTermStructure interest = EquityInterestRateCurve
            ?? throw new NanoQLException($"null interest rate term structure set to this instance of {Name}");

        Date today = context.EvaluationDate;
        Date lastFixingDate = fixingCalendar.Adjust(today, BusinessDayConvention.Preceding);

        double spot = ResolveSpot(Spot, PastFixing(lastFixingDate, context));

        double forward;
        if (EquityDividendCurve is YieldTermStructure dividend)
        {
            forward = spot * Discount(dividend, today, fixingDate) / Discount(interest, today, fixingDate);
        }
        else
        {
            forward = spot / Discount(interest, today, fixingDate);
        }

        return forward;
    }

    /// <summary>A copy of the index on other interest and dividend curves and spot.</summary>
    /// <param name="interest">The interest rate curve, or <see langword="null"/> for none.</param>
    /// <param name="dividend">The dividend curve, or <see langword="null"/> for none.</param>
    /// <param name="spot">The spot value, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public virtual EquityIndex Clone(YieldTermStructure? interest, YieldTermStructure? dividend, double? spot) =>
        new(Name, FixingCalendar, Currency, interest, dividend, spot, Provider);

    private static double ResolveSpot(double? spot, double? lastFixing)
    {
        if (!(spot is not null || lastFixing is not null))
        {
            throw new NanoQLException("Cannot forecast equity index, missing both spot and historical index");
        }

        return spot ?? lastFixing!.Value;
    }

    // P(t, T): the curve's discount at T over its discount at t, nothing accrued before its reference date. On a curve
    // dated on or after t nothing has accrued at t, and the read is QuantLib's, with no divisor: a curve's discount at
    // its own date needn't be 1 (QuantLib's unconstrained fitted bond curve reads 0 there).
    private static double Discount(YieldTermStructure curve, Date t, Date maturity) =>
        t <= curve.ReferenceDate
            ? curve.AccruedDiscount(maturity)
            : curve.AccruedDiscount(maturity) / curve.Discount(t);
}
