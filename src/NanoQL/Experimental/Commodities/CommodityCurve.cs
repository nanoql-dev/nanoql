// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commoditycurve.hpp ql/experimental/commodities/commoditycurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A commodity forward curve: prices at dates, forward-flat between them and flat before the first and after the last,
/// optionally on a basis curve whose prices it adds.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: QuantLib's <c>setPrices</c> and <c>setBasisOfCurve</c> change the curve, and here return a copy. Its
/// constructor from dates has a fixed reference date, the first date; its constructor without dates follows the
/// evaluation date (<c>TermStructure(0, …)</c>) and is the factory <see cref="Create"/>, whose curve gets its prices from
/// <see cref="SetPrices"/>.
/// </para>
/// <para>
/// The node times are measured from the reference date. QuantLib's <c>setPrices</c> measures them from the first price's
/// date, so on the moving-date curve with the first price ten days after today every price in the ten days before
/// each node is the next node's (<c>Divergences/CommodityCurveTests.cs</c>); on the curve from dates the two are one.
/// </para>
/// <para>
/// A basis curve is read at the date, on its own reference date and day counter, and its price converted by the factor
/// from this curve's unit to its unit, a price's conversion. QuantLib reads it at this curve's time, on this curve's
/// clock, and converts by the factor from the basis curve's unit to this one's, which inverts it; where the two curves
/// share their reference date, day counter and unit both give QuantLib's prices.
/// </para>
/// <para>
/// QuantLib's <c>operator==</c> compares the curves' names; compare <see cref="Name"/>, as for an index
/// (<see cref="FinancialIndex"/>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class CommodityCurve : TermStructure
{
    private readonly ImmutableArray<Date> dates;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> prices;
    private readonly ForwardFlatInterpolation? interpolation;
    private readonly double basisOfCurveUomConversionFactor;

    /// <summary>Initializes a new instance of the <see cref="CommodityCurve"/> class, its reference date its first
    /// date.</summary>
    /// <param name="name">The curve's name.</param>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="currency">The currency its prices are in.</param>
    /// <param name="unitOfMeasure">The unit its prices are per.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="dates">The price dates, increasing; at least two.</param>
    /// <param name="prices">The prices, one per date.</param>
    /// <param name="dayCounter">The day counter; QuantLib's default, Actual/365 (Fixed), when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A string, the type, the currency, the unit, the calendar or a list is
    /// null.</exception>
    /// <exception cref="NanoQLException">Fewer than two dates ("too few dates"), the counts differ, or a date isn't after
    /// the one before.</exception>
    public CommodityCurve(
        string name,
        CommodityType commodityType,
        Currency currency,
        UnitOfMeasure unitOfMeasure,
        Calendar calendar,
        IReadOnlyList<Date> dates,
        IReadOnlyList<double> prices,
        DayCounter? dayCounter = null)
        : this(name, commodityType, currency, unitOfMeasure, calendar, FirstDate(dates), dayCounter ?? new Actual365Fixed(), null)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (dates.Count <= 1)
        {
            throw new NanoQLException("too few dates");
        }

        if (prices.Count != dates.Count)
        {
            throw new NanoQLException("dates/prices count mismatch");
        }

        for (int i = 1; i < dates.Count; i++)
        {
            if (!(dates[i] > dates[i - 1]))
            {
                throw new NanoQLException($"invalid date ({dates[i]}, vs {dates[i - 1]})");
            }
        }

        this.dates = [.. dates];
        this.prices = [.. prices];
        times = NodeTimes(this.dates);
        interpolation = new ForwardFlatInterpolation(times.AsSpan(), this.prices.AsSpan());
    }

    private CommodityCurve(
        string name,
        CommodityType commodityType,
        Currency currency,
        UnitOfMeasure unitOfMeasure,
        Calendar calendar,
        Date referenceDate,
        DayCounter dayCounter,
        int? settlementDays)
        : base(referenceDate, calendar, dayCounter, settlementDays)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(commodityType);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(unitOfMeasure);
        Name = name;
        CommodityType = commodityType;
        Currency = currency;
        UnitOfMeasure = unitOfMeasure;
        dates = [];
        times = [];
        prices = [];
        basisOfCurveUomConversionFactor = 1.0;
    }

    private CommodityCurve(
        CommodityCurve from,
        ImmutableArray<Date> dates,
        ImmutableArray<double> prices,
        CommodityCurve? basisOfCurve,
        double basisOfCurveUomConversionFactor)
        : base(from.ReferenceDate, from.Calendar, from.DayCounter, from.SettlementDays)
    {
        Name = from.Name;
        CommodityType = from.CommodityType;
        Currency = from.Currency;
        UnitOfMeasure = from.UnitOfMeasure;
        this.dates = dates;
        this.prices = prices;
        times = NodeTimes(dates);
        interpolation = dates.IsEmpty ? null : new ForwardFlatInterpolation(times.AsSpan(), prices.AsSpan());
        BasisOfCurve = basisOfCurve;
        this.basisOfCurveUomConversionFactor = basisOfCurveUomConversionFactor;
    }

    /// <summary>Gets the curve's name.</summary>
    public string Name { get; }

    /// <summary>Gets the commodity type.</summary>
    public CommodityType CommodityType { get; }

    /// <summary>Gets the unit its prices are per.</summary>
    public UnitOfMeasure UnitOfMeasure { get; }

    /// <summary>Gets the currency its prices are in.</summary>
    public Currency Currency { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The curve has no prices.</exception>
    public override Date MaxDate => !dates.IsEmpty ? dates[^1] : throw new NanoQLException("empty commodity curve");

    /// <summary>Gets the node times, from the reference date.</summary>
    public ImmutableArray<double> Times => times;

    /// <summary>Gets the price dates.</summary>
    public ImmutableArray<Date> Dates => dates;

    /// <summary>Gets the prices.</summary>
    public ImmutableArray<double> Prices => prices;

    /// <summary>Gets the dates with their prices.</summary>
    public ImmutableArray<(Date Date, double Price)> Nodes => [.. dates.Zip(prices)];

    /// <summary>Gets a value indicating whether the curve has no prices.</summary>
    public bool Empty => dates.IsEmpty;

    /// <summary>Gets the basis curve whose prices this curve adds; <see langword="null"/> for none.</summary>
    public CommodityCurve? BasisOfCurve { get; }

    /// <summary>
    /// A curve with no prices whose reference date follows the evaluation date: QuantLib's constructor without dates,
    /// its reference date the evaluation date adjusted on the calendar. <see cref="SetPrices"/> gives it its prices.
    /// </summary>
    /// <param name="name">The curve's name.</param>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="currency">The currency its prices are in.</param>
    /// <param name="unitOfMeasure">The unit its prices are per.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="context">The evaluation context, whose evaluation date is read.</param>
    /// <param name="dayCounter">The day counter; Actual/365 (Fixed) when <see langword="null"/>.</param>
    /// <returns>The curve, with a fixed reference date and no prices.</returns>
    /// <exception cref="ArgumentNullException">An argument but the day counter is null.</exception>
    public static CommodityCurve Create(
        string name,
        CommodityType commodityType,
        Currency currency,
        UnitOfMeasure unitOfMeasure,
        Calendar calendar,
        EvaluationContext context,
        DayCounter? dayCounter = null)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(context);
        Date referenceDate = calendar.Advance(context.EvaluationDate, 0, TimeUnit.Days);
        return new CommodityCurve(
            name, commodityType, currency, unitOfMeasure, calendar, referenceDate, dayCounter ?? new Actual365Fixed(), 0);
    }

    /// <summary>The curve with new prices: QuantLib's <c>setPrices</c>, the dates in order, timed from the reference
    /// date.</summary>
    /// <param name="prices">The prices by date; at least two.</param>
    /// <returns>A copy with the prices, keeping the basis curve; this curve is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prices"/> is null.</exception>
    /// <exception cref="NanoQLException">Fewer than two prices ("too few prices").</exception>
    public CommodityCurve SetPrices(IReadOnlyDictionary<Date, double> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count <= 1)
        {
            throw new NanoQLException("too few prices");
        }

        KeyValuePair<Date, double>[] sorted = [.. prices.OrderBy(p => p.Key)];
        return new CommodityCurve(
            this, [.. sorted.Select(p => p.Key)], [.. sorted.Select(p => p.Value)], BasisOfCurve, basisOfCurveUomConversionFactor);
    }

    /// <summary>The curve on a basis curve: QuantLib's <c>setBasisOfCurve</c>.</summary>
    /// <param name="basisOfCurve">The basis curve.</param>
    /// <param name="context">The evaluation context, whose unit conversions give the basis price's factor.</param>
    /// <returns>A copy adding the basis curve's prices; this curve is unchanged.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">No conversion between the curves' units is held.</exception>
    public CommodityCurve SetBasisOfCurve(CommodityCurve basisOfCurve, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(basisOfCurve);
        ArgumentNullException.ThrowIfNull(context);
        double factor = CommodityPricingHelperFunctions.CalculateUomConversionFactor(
            CommodityType, UnitOfMeasure, basisOfCurve.UnitOfMeasure, context);
        return new CommodityCurve(this, dates, prices, basisOfCurve, factor);
    }

    /// <summary>
    /// The price at a date, with every basis curve's: at the nearby contract's underlying start date when the offset is
    /// positive.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <param name="exchangeContracts">The exchange contracts by date; read only when the offset is positive.</param>
    /// <param name="nearbyOffset">The nearby contract, from 1; 0 or less for the date's own price.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The curve has no prices ("error retrieving price for curve …"), or not enough
    /// nearby contracts follow the date.</exception>
    public double Price(Date d, ImmutableSortedDictionary<Date, ExchangeContract>? exchangeContracts, int nearbyOffset)
    {
        Date date = nearbyOffset > 0 ? UnderlyingPriceDate(d, exchangeContracts, nearbyOffset) : d;
        double priceValue;
        try
        {
            priceValue = PriceAt(date);
        }
        catch (NanoQLException e)
        {
            throw new NanoQLException($"error retrieving price for curve [{Name}]: {e.Message}", e);
        }

        return priceValue + BasisOfPrice(date);
    }

    /// <summary>The basis curves' price at a date, converted into this curve's unit; 0 without a basis curve.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The basis price.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">A basis curve has no prices.</exception>
    public double BasisOfPrice(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        if (BasisOfCurve is not null)
        {
            double basisCurvePriceValue;
            try
            {
                basisCurvePriceValue = BasisOfCurve.PriceAt(d) * basisOfCurveUomConversionFactor;
            }
            catch (NanoQLException e)
            {
                throw new NanoQLException($"error retrieving price for curve [{Name}]: {e.Message}", e);
            }

            return basisCurvePriceValue + BasisOfCurve.BasisOfPrice(d);
        }

        return 0.0;
    }

    /// <summary>
    /// The date a nearby curve prices a date at: the underlying start date of the contract the offset counts to among
    /// those on or after the date; the date itself when none is on or after it.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="exchangeContracts">The exchange contracts by date.</param>
    /// <param name="nearbyOffset">The nearby contract, from 1.</param>
    /// <returns>The date to price at.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exchangeContracts"/> is null.</exception>
    /// <exception cref="NanoQLException">The offset isn't positive, or not enough contracts follow the date.</exception>
    public Date UnderlyingPriceDate(
        Date date,
        ImmutableSortedDictionary<Date, ExchangeContract>? exchangeContracts,
        int nearbyOffset)
    {
        if (!(nearbyOffset > 0))
        {
            throw new NanoQLException("nearby offset must be > 0");
        }

        ArgumentNullException.ThrowIfNull(exchangeContracts);
        Date.ThrowIfDefault(date, nameof(date));
        ExchangeContract[] onOrAfter = [.. exchangeContracts.Where(c => c.Key >= date).Select(c => c.Value)];
        if (onOrAfter.Length > 0)
        {
            if (nearbyOffset > onOrAfter.Length)
            {
                throw new NanoQLException(
                    $"not enough nearby contracts available for curve [{Name}] for date [{date}].");
            }

            return onOrAfter[nearbyOffset - 1].UnderlyingStartDate;
        }

        return date;
    }

    /// <summary>
    /// The curve as QuantLib's <c>operator&lt;&lt;</c> writes it: "[name] (USD/BBL)", and "; basis to (" with the basis
    /// curve's text and ")" where there is one.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        string text = $"[{Name}] ({Currency.Code}/{UnitOfMeasure.Code})";
        return BasisOfCurve is null ? text : $"{text}; basis to ({BasisOfCurve})";
    }

    private static Date FirstDate(IReadOnlyList<Date> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        return dates.Count > 0 ? dates[0] : throw new NanoQLException("too few dates");
    }

    // QuantLib's priceImpl, interpolation_(t, true), at the date's time on this curve's clock.
    private double PriceAt(Date d)
    {
        ForwardFlatInterpolation curve = interpolation ?? throw new NanoQLException("empty interpolation");
        return curve.Evaluate(TimeFromReference(d), true);
    }

    // The node times from the reference date.
    private ImmutableArray<double> NodeTimes(ImmutableArray<Date> nodeDates) =>
        [.. nodeDates.Select(d => DayCounter.YearFraction(ReferenceDate, d))];
}
