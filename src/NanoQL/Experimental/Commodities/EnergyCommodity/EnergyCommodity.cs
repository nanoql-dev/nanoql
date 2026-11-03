// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energycommodity.hpp ql/experimental/commodities/energycommodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: EnergyCommodity::arguments and EnergyCommodity::engine, never ported: no engine derives from the engine class, and setupArguments fills no argument

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The base of energy commodity instruments: the swaps and the future, priced without an engine.</summary>
/// <remarks>
/// <para>
/// An extension point. Each instrument values in the context's base currency and unit,
/// <see cref="EvaluationContext.CommoditySettings"/>, QuantLib's <c>CommoditySettings</c>, converting its prices and
/// quantities with the context's <see cref="EvaluationContext.UnitOfMeasureConversions"/> and
/// <see cref="EvaluationContext.ExchangeRates"/>.
/// </para>
/// <para>
/// A unit conversion factor here is the value of one unit of the first unit in the second, the conversion's own
/// <see cref="UnitOfMeasureConversion.Convert"/> of a unit quantity, whichever way the conversion is stored; a quantity
/// converts to the base unit by the factor from its unit to the base, and a price, money per unit, by the factor from
/// the base to its unit. QuantLib's factor is the stored conversion's whatever its orientation, and its call sites
/// multiply a quantity by the factor from the base to its unit and a price by the factor from its unit to the base, so
/// one of the two is inverted: a payer vanilla swap on 1,000 gallons a month at a 0.5 spread prices 882,000 there, 42²
/// times its value, 500, here; on the base unit every factor is 1 and the prices are QuantLib's
/// (<c>Divergences/EnergyUnitConversionTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class EnergyCommodity : Commodity
{
    /// <summary>Initializes a new instance of the <see cref="EnergyCommodity"/> class.</summary>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="secondaryCosts">The secondary costs by name; <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="commodityType"/> is null.</exception>
    protected EnergyCommodity(CommodityType commodityType, IReadOnlyDictionary<string, object>? secondaryCosts)
        : base(secondaryCosts)
    {
        ArgumentNullException.ThrowIfNull(commodityType);
        ConstructedCommodityType = commodityType;
    }

    /// <summary>Gets the commodity type: the one the instrument was built with, or an energy swap's first period's.</summary>
    public virtual CommodityType CommodityType => ConstructedCommodityType;

    /// <summary>Gets the quantity the instrument delivers.</summary>
    public abstract Quantity Quantity { get; }

    // The commodity type given to the constructor, QuantLib's commodityType_.
    private protected CommodityType ConstructedCommodityType { get; }

    /// <summary>
    /// The factor converting an amount in one currency into another at a date, through a derived lookup in the context's
    /// exchange rates: QuantLib's <c>EnergyCommodity::calculateFxConversionFactor</c>.
    /// </summary>
    /// <param name="fromCurrency">The amount's currency.</param>
    /// <param name="toCurrency">The currency it is converted into.</param>
    /// <param name="evaluationDate">The date of the rates.</param>
    /// <param name="context">The evaluation context, whose exchange rates are read.</param>
    /// <returns>1 between a currency and itself; otherwise the rate, or its reciprocal where the rate found runs the other
    /// way.</returns>
    /// <exception cref="ArgumentNullException">A currency or the context is null.</exception>
    /// <exception cref="NanoQLException">No rate is held.</exception>
    protected static double CalculateFxConversionFactor(
        Currency fromCurrency,
        Currency toCurrency,
        Date evaluationDate,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(fromCurrency);
        ArgumentNullException.ThrowIfNull(toCurrency);
        ArgumentNullException.ThrowIfNull(context);
        if (fromCurrency != toCurrency)
        {
            ExchangeRate rate = context.ExchangeRates.Lookup(fromCurrency, toCurrency, context, evaluationDate);
            return fromCurrency == rate.Target ? 1.0 / rate.Rate : rate.Rate;
        }

        return 1.0;
    }

    /// <summary>
    /// The value of one unit of a unit of measure in another, for a commodity type: QuantLib's
    /// <c>EnergyCommodity::calculateUomConversionFactor</c>, direction-aware.
    /// </summary>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="fromUnitOfMeasure">The unit converted from.</param>
    /// <param name="toUnitOfMeasure">The unit converted into.</param>
    /// <param name="context">The evaluation context, whose unit conversions are read.</param>
    /// <returns>1 between a unit and itself; otherwise how many of the second unit one of the first is worth.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">No conversion is held.</exception>
    protected static double CalculateUomConversionFactor(
        CommodityType commodityType,
        UnitOfMeasure fromUnitOfMeasure,
        UnitOfMeasure toUnitOfMeasure,
        EvaluationContext context) =>
        CommodityPricingHelperFunctions.CalculateUomConversionFactor(
            commodityType, fromUnitOfMeasure, toUnitOfMeasure, context);

    /// <summary>
    /// A unit cost as money per base unit in the base currency: QuantLib's <c>EnergyCommodity::calculateUnitCost</c>,
    /// the cost converted by the factor from the base unit to its unit (a price) and from its currency to the base
    /// currency.
    /// </summary>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="unitCost">The unit cost.</param>
    /// <param name="evaluationDate">The date of the exchange rates.</param>
    /// <param name="context">The evaluation context, whose commodity settings, unit conversions and exchange rates are
    /// read.</param>
    /// <returns>The cost per base unit; 0 for a zero cost.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The cost's currency or unit is QuantLib's empty one, or a conversion or rate
    /// can't be found.</exception>
    protected static double CalculateUnitCost(
        CommodityType commodityType,
        CommodityUnitCost unitCost,
        Date evaluationDate,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(unitCost);
        ArgumentNullException.ThrowIfNull(context);
        if (unitCost.Amount.Value != 0)
        {
            CommoditySettings settings = context.CommoditySettings;
            double uomFactor = CalculateUomConversionFactor(
                commodityType, settings.UnitOfMeasure, UnitOfCost(unitCost), context);
            double fxFactor = CalculateFxConversionFactor(
                CurrencyOfCost(unitCost), settings.Currency, evaluationDate, context);
            return unitCost.Amount.Value * uomFactor * fxFactor;
        }

        return 0.0;
    }

    /// <summary>
    /// The secondary costs' amounts in the base currency: QuantLib's <c>calculateSecondaryCostAmounts</c>, a unit cost
    /// per base unit times the quantity, a fixed amount converted.
    /// </summary>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="totalQuantityValue">The quantity a unit cost applies to, in the base unit.</param>
    /// <param name="evaluationDate">The date of the exchange rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amounts by name; empty without secondary costs.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">A cost can't be converted ("error calculating secondary costs: …").</exception>
    protected ImmutableSortedDictionary<string, Money> CalculateSecondaryCostAmounts(
        CommodityType commodityType,
        double totalQuantityValue,
        Date evaluationDate,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ImmutableSortedDictionary<string, Money>.Builder amounts =
            ImmutableSortedDictionary.CreateBuilder<string, Money>(StringComparer.Ordinal);
        if (SecondaryCosts is not null)
        {
            Currency baseCurrency = context.CommoditySettings.Currency;
            try
            {
                foreach (KeyValuePair<string, object> cost in SecondaryCosts)
                {
                    if (cost.Value is CommodityUnitCost unitCost)
                    {
                        double value = CalculateUnitCost(commodityType, unitCost, evaluationDate, context)
                            * totalQuantityValue;
                        amounts[cost.Key] = new Money(baseCurrency, value);
                    }
                    else if (cost.Value is Money amount)
                    {
                        double fxFactor = CalculateFxConversionFactor(
                            amount.Currency ?? throw new NanoQLException("no currency data provided"),
                            baseCurrency,
                            evaluationDate,
                            context);
                        amounts[cost.Key] = new Money(baseCurrency, amount.Value * fxFactor);
                    }
                }
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                throw new NanoQLException($"error calculating secondary costs: {e.Message}", e);
            }
        }

        return amounts.ToImmutable();
    }

    private static UnitOfMeasure UnitOfCost(CommodityUnitCost unitCost) =>
        unitCost.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided");

    private static Currency CurrencyOfCost(CommodityUnitCost unitCost) =>
        unitCost.Amount.Currency ?? throw new NanoQLException("no currency data provided");
}
