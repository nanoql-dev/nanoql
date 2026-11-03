// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commodityindex.hpp ql/experimental/commodities/commodityindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A commodity index: its fixings, read from the context, and a forward curve priced in its own unit.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's base class for commodity indexes, which nothing in QuantLib derives from; an extension point here too.
/// Its stored fixings are <see cref="EvaluationContext.Fixings"/>' under its name.
/// </para>
/// <para>
/// <see cref="ForwardPrice"/> converts the curve's price per curve unit into a price per index unit by the factor from
/// the index's unit to the curve's, a price's conversion; QuantLib multiplies by the factor from the curve's unit to the
/// index's, which inverts it, and computes the factor when the index is built, where it is read from the context here.
/// </para>
/// <para>
/// <see cref="LastQuoteDate"/> is <see langword="null"/> for an index with no fixings, where QuantLib's reads the empty
/// history's last date and throws "empty timeseries", so its swaps and future refuse an index with no fixings that their
/// own branch prices from the forward curve (<c>Divergences/CommodityIndexNoFixingsTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class CommodityIndex : FinancialIndex
{
    private readonly string name;
    private readonly Calendar calendar;

    /// <summary>Initializes a new instance of the <see cref="CommodityIndex"/> class.</summary>
    /// <param name="indexName">The index's name, which keys its fixings.</param>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="currency">The currency its prices are in.</param>
    /// <param name="unitOfMeasure">The unit its prices are per.</param>
    /// <param name="calendar">The fixing calendar.</param>
    /// <param name="lotQuantity">The quantity of a lot, which an energy future multiplies its quantity by.</param>
    /// <param name="forwardCurve">The forward curve; <see langword="null"/> for none.</param>
    /// <param name="exchangeContracts">The exchange contracts a nearby curve rolls on, by date; <see langword="null"/>
    /// for none.</param>
    /// <param name="nearbyOffset">The nearby contract the forward price reads, from 1; 0 for the date's own price.</param>
    /// <exception cref="ArgumentNullException">A string, the type, the currency, the unit or the calendar is null.</exception>
    public CommodityIndex(
        string indexName,
        CommodityType commodityType,
        Currency currency,
        UnitOfMeasure unitOfMeasure,
        Calendar calendar,
        double lotQuantity,
        CommodityCurve? forwardCurve,
        ImmutableSortedDictionary<Date, ExchangeContract>? exchangeContracts,
        int nearbyOffset)
    {
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(commodityType);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(unitOfMeasure);
        ArgumentNullException.ThrowIfNull(calendar);
        name = indexName;
        CommodityType = commodityType;
        Currency = currency;
        UnitOfMeasure = unitOfMeasure;
        this.calendar = calendar;
        LotQuantity = lotQuantity;
        ForwardCurve = forwardCurve;
        ExchangeContracts = exchangeContracts;
        NearbyOffset = nearbyOffset;
    }

    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override Calendar FixingCalendar => calendar;

    /// <summary>Gets the commodity type.</summary>
    public CommodityType CommodityType { get; }

    /// <summary>Gets the currency its prices are in.</summary>
    public Currency Currency { get; }

    /// <summary>Gets the unit its prices are per.</summary>
    public UnitOfMeasure UnitOfMeasure { get; }

    /// <summary>Gets the forward curve; <see langword="null"/> for none.</summary>
    public CommodityCurve? ForwardCurve { get; }

    /// <summary>Gets the quantity of a lot.</summary>
    public double LotQuantity { get; }

    /// <summary>Gets the exchange contracts a nearby curve rolls on; <see langword="null"/> for none.</summary>
    public ImmutableSortedDictionary<Date, ExchangeContract>? ExchangeContracts { get; }

    /// <summary>Gets the nearby contract the forward price reads, from 1; 0 for the date's own price.</summary>
    public int NearbyOffset { get; }

    /// <summary>Gets a value indicating whether the forward curve has no prices; <see langword="false"/> without a
    /// curve, as QuantLib's.</summary>
    public bool ForwardCurveEmpty => ForwardCurve?.Empty ?? false;

    /// <inheritdoc/>
    public override bool IsValidFixingDate(Date fixingDate) => FixingCalendar.IsBusinessDay(fixingDate);

    /// <summary>The stored fixing on a date: QuantLib's <c>fixing</c>, its past fixing alone, never forecast.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <param name="forecastTodaysFixing">Ignored, as QuantLib ignores it.</param>
    /// <returns>The stored fixing.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date.</exception>
    /// <exception cref="NanoQLException">No fixing is stored on the date, where QuantLib returns its null
    /// value.</exception>
    public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false) =>
        PastFixing(fixingDate, context) ?? throw new NanoQLException($"missing {Name} fixing for {fixingDate}");

    /// <summary>
    /// The forward price at a date, per index unit in the index's currency: the curve's price, through the nearby
    /// contract when the offset is positive, converted from the curve's unit.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="context">The evaluation context, whose unit conversions are read.</param>
    /// <returns>The forward price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">There is no forward curve, or the curve can't price the date ("error fetching
    /// forward price for index …").</exception>
    public double ForwardPrice(Date date, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CommodityCurve curve = ForwardCurve ?? throw new NanoQLException($"index [{Name}] has no forward curve");
        try
        {
            double forwardPrice = curve.Price(date, ExchangeContracts, NearbyOffset);
            double factor = CommodityPricingHelperFunctions.CalculateUomConversionFactor(
                CommodityType, UnitOfMeasure, curve.UnitOfMeasure, context);
            return forwardPrice * factor;
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            throw new NanoQLException($"error fetching forward price for index {Name}: {e.Message}", e);
        }
    }

    /// <summary>The last date a fixing is stored on: QuantLib's <c>lastQuoteDate</c>.</summary>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns>The date; <see langword="null"/> for an index with no fixings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public Date? LastQuoteDate(EvaluationContext context)
    {
        TimeSeries<double> history = TimeSeries(context);
        return history.Empty ? null : history.LastDate();
    }

    /// <summary>Whether no fixing is stored: QuantLib's <c>empty</c>.</summary>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns><see langword="true"/> when the index has no fixings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public bool Empty(EvaluationContext context) => TimeSeries(context).Empty;

    /// <summary>
    /// The index as QuantLib's <c>operator&lt;&lt;</c> writes it: "[name] (USD/BBL)", and "; forward (" with the
    /// curve's text and ")" where there is a curve.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        string text = $"[{Name}] ({Currency.Code}/{UnitOfMeasure.Code})";
        return ForwardCurve is null ? text : $"{text}; forward ({ForwardCurve})";
    }
}
