// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energyfuture.hpp ql/experimental/commodities/energyfuture.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>An energy future: a quantity bought or sold at a trade price, valued at its index's quote.</summary>
/// <remarks>
/// <para>
/// The value is (quote − trade price) × quantity × lots × buy-sell, in the base currency and unit, less the secondary
/// costs. The quote is the index's stored fixing when its last quote date is the evaluation date or the day before,
/// its forward price at the evaluation date otherwise (with QuantLib's warning). The future never expires, as QuantLib's.
/// </para>
/// <para>
/// Three of QuantLib's lines are fixed (the S-xrt plan's XRR5; <c>Divergences/EnergyFutureTests.cs</c>):
/// </para>
/// <list type="bullet">
/// <item><description>(vii) the lot multiplies the quantity once: QuantLib's conversion factor already carries it and
/// the value multiplies by it again, so lot 1,000 prices 5,000,000 there for 5,000 here;</description></item>
/// <item><description>(xxxix) the secondary costs are charged on the quantity the value uses, in the base unit with
/// its lots, where QuantLib's reads the raw amount (a fee of 0.1 a unit on 10 gallons reads 42 there, 1
/// here);</description></item>
/// <item><description>(xl) within QuantLib's window the quote is the stored history's entry for the evaluation date
/// where there is one (QuantLib's), else the day before's, read from the history itself: QuantLib reads the evaluation
/// date's fixing alone, so with the last fixing yesterday it refuses ("missing quote"), and through the index's fixing,
/// which refuses a Saturday evaluation on a weekday index. Neither entry: QuantLib's "missing
/// quote".</description></item>
/// </list>
/// </remarks>
[Experimental("NQL0001")]
public sealed class EnergyFuture : EnergyCommodity
{
    private readonly Quantity quantity;

    /// <summary>Initializes a new instance of the <see cref="EnergyFuture"/> class.</summary>
    /// <param name="buySell">1 for a bought future, −1 for a sold one (QuantLib's integer, any value taken).</param>
    /// <param name="quantity">The quantity per lot, in its unit.</param>
    /// <param name="tradePrice">The trade price, per its unit.</param>
    /// <param name="index">The index.</param>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="secondaryCosts">The secondary costs by name; <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">An argument but the costs is null.</exception>
    public EnergyFuture(
        int buySell,
        Quantity quantity,
        CommodityUnitCost tradePrice,
        CommodityIndex index,
        CommodityType commodityType,
        IReadOnlyDictionary<string, object>? secondaryCosts)
        : base(commodityType, secondaryCosts)
    {
        ArgumentNullException.ThrowIfNull(tradePrice);
        ArgumentNullException.ThrowIfNull(index);
        BuySell = buySell;
        this.quantity = quantity;
        TradePrice = tradePrice;
        Index = index;
    }

    /// <summary>Gets 1 for a bought future, −1 for a sold one.</summary>
    public int BuySell { get; }

    /// <inheritdoc/>
    public override Quantity Quantity => quantity;

    /// <summary>Gets the trade price, per its unit.</summary>
    public CommodityUnitCost TradePrice { get; }

    /// <summary>Gets the index.</summary>
    public CommodityIndex Index { get; }

    /// <inheritdoc/>
    /// <remarks>Never, as QuantLib's.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return false;
    }

    /// <summary>Prices the future: QuantLib's <c>performCalculations</c>, in the context's base currency and unit.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The NPV, the secondary cost amounts and the pricing errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The quote is missing ("missing quote for …"), or a price, quantity or cost can't
    /// be converted.</exception>
    public EnergyCommodityResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ImmutableArray<PricingError>.Builder errors = ImmutableArray.CreateBuilder<PricingError>();
        Date evaluationDate = context.EvaluationDate;
        CommoditySettings settings = context.CommoditySettings;
        Currency baseCurrency = settings.Currency;
        UnitOfMeasure baseUnitOfMeasure = settings.UnitOfMeasure;
        CommodityType quantityType = quantity.CommodityType
            ?? throw new NanoQLException("no commodity type data provided");

        double quantityUomConversionFactor = CalculateUomConversionFactor(
            quantityType,
            quantity.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided"),
            baseUnitOfMeasure,
            context) * Index.LotQuantity;
        double indexUomConversionFactor = CalculateUomConversionFactor(
            Index.CommodityType, baseUnitOfMeasure, Index.UnitOfMeasure, context);
        double tradePriceUomConversionFactor = CalculateUomConversionFactor(
            quantityType,
            baseUnitOfMeasure,
            TradePrice.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided"),
            context);

        double tradePriceFxConversionFactor = CalculateFxConversionFactor(
            TradePrice.Amount.Currency ?? throw new NanoQLException("no currency data provided"),
            baseCurrency,
            evaluationDate,
            context);
        double indexPriceFxConversionFactor = CalculateFxConversionFactor(
            Index.Currency, baseCurrency, evaluationDate, context);

        double? quoteValue;
        Date? lastQuoteDate = Index.LastQuoteDate(context);
        if (lastQuoteDate is Date last && last >= evaluationDate - 1)
        {
            TimeSeries<double> history = Index.TimeSeries(context);
            quoteValue = history.TryGetValue(evaluationDate, out double today) ? today
                : history.TryGetValue(evaluationDate - 1, out double yesterday) ? yesterday
                : null;
        }
        else
        {
            quoteValue = Index.ForwardPrice(evaluationDate, context);
            string lastText = lastQuoteDate is Date d ? $"has last quote date of {EnergySwap.Iso(d)}" : "has no quotes";
            errors.Add(new PricingError(
                PricingErrorLevel.Warning,
                $"curve [{Index.Name}] {lastText} using forward price from [{EnergyVanillaSwap.ForwardCurveName(Index)}]"));
        }

        double quote = quoteValue ?? throw new NanoQLException($"missing quote for [{Index.Name}]");
        double tradePriceValue =
            TradePrice.Amount.Value * tradePriceUomConversionFactor * tradePriceFxConversionFactor;
        double quotePriceValue = quote * indexUomConversionFactor * indexPriceFxConversionFactor;
        double quantityAmount = quantity.Amount * quantityUomConversionFactor;
        double delta = (quotePriceValue - tradePriceValue) * quantityAmount * BuySell;

        double npv = delta;
        ImmutableSortedDictionary<string, Money> secondaryCostAmounts =
            CalculateSecondaryCostAmounts(quantityType, quantityAmount, evaluationDate, context);
        foreach (Money amount in secondaryCostAmounts.Values)
        {
            npv -= amount.Value;
        }

        return new EnergyCommodityResults
        {
            NPV = npv,
            PricingErrors = errors.ToImmutable(),
            SecondaryCostAmounts = secondaryCostAmounts,
        };
    }
}
