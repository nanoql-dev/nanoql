// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energyvanillaswap.hpp ql/experimental/commodities/energyvanillaswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A vanilla energy swap: a fixed price against an index's price on each pricing day.</summary>
/// <remarks>
/// A payer pays the fixed price and receives the index's; a receiver the reverse. Each period's quantity is spread
/// evenly over its pricing days and valued in the base currency and unit, discounted on each leg's curve from two days
/// after the evaluation date, a payment on or before then at 1; the secondary costs are charged on the total quantity
/// in the base unit. The unit conversions are <see cref="EnergyCommodity"/>'s, direction-aware, the payments
/// <see cref="EnergySwap"/>'s. As QuantLib files the legs, the fixed leg is in the pay currency and the floating leg in
/// the receive currency whichever way the swap faces, so a receiver receives the fixed price in the pay currency.
/// </remarks>
[Experimental("NQL0001")]
public sealed class EnergyVanillaSwap : EnergySwap
{
    private readonly YieldTermStructure payLegTermStructure;
    private readonly YieldTermStructure receiveLegTermStructure;
    private readonly YieldTermStructure discountTermStructure;

    /// <summary>Initializes a new instance of the <see cref="EnergyVanillaSwap"/> class.</summary>
    /// <param name="payer">Whether the holder pays the fixed price.</param>
    /// <param name="calendar">The pricing calendar.</param>
    /// <param name="fixedPrice">The fixed price, per <paramref name="fixedPriceUnitOfMeasure"/>.</param>
    /// <param name="fixedPriceUnitOfMeasure">The unit the fixed price is per.</param>
    /// <param name="index">The floating leg's index.</param>
    /// <param name="payCurrency">The fixed leg's currency, which a payer pays in and a receiver receives in.</param>
    /// <param name="receiveCurrency">The floating leg's currency, which a payer receives in and a receiver pays
    /// in.</param>
    /// <param name="pricingPeriods">The pricing periods; at least one.</param>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="secondaryCosts">The secondary costs by name; <see langword="null"/> for none.</param>
    /// <param name="payLegTermStructure">The curve discounting the pay leg.</param>
    /// <param name="receiveLegTermStructure">The curve discounting the receive leg.</param>
    /// <param name="discountTermStructure">The curve discounting the base amounts.</param>
    /// <exception cref="ArgumentNullException">An argument but the costs is null.</exception>
    /// <exception cref="NanoQLException">There are no pricing periods.</exception>
    public EnergyVanillaSwap(
        bool payer,
        Calendar calendar,
        Money fixedPrice,
        UnitOfMeasure fixedPriceUnitOfMeasure,
        CommodityIndex index,
        Currency payCurrency,
        Currency receiveCurrency,
        IReadOnlyList<PricingPeriod> pricingPeriods,
        CommodityType commodityType,
        IReadOnlyDictionary<string, object>? secondaryCosts,
        YieldTermStructure payLegTermStructure,
        YieldTermStructure receiveLegTermStructure,
        YieldTermStructure discountTermStructure)
        : base(calendar, payCurrency, receiveCurrency, pricingPeriods, commodityType, secondaryCosts)
    {
        ArgumentNullException.ThrowIfNull(fixedPriceUnitOfMeasure);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(payLegTermStructure);
        ArgumentNullException.ThrowIfNull(receiveLegTermStructure);
        ArgumentNullException.ThrowIfNull(discountTermStructure);
        if (PricingPeriods.IsEmpty)
        {
            throw new NanoQLException("no pricing periods");
        }

        Payer = payer;
        FixedPrice = fixedPrice;
        FixedPriceUnitOfMeasure = fixedPriceUnitOfMeasure;
        Index = index;
        this.payLegTermStructure = payLegTermStructure;
        this.receiveLegTermStructure = receiveLegTermStructure;
        this.discountTermStructure = discountTermStructure;
    }

    /// <summary>Gets a value indicating whether the holder pays the fixed price.</summary>
    public bool Payer { get; }

    /// <summary>Gets QuantLib's pay-receive flag: 1 for a payer, 0 for a receiver.</summary>
    public int PayReceive => Payer ? 1 : 0;

    /// <summary>Gets the fixed price.</summary>
    public Money FixedPrice { get; }

    /// <summary>Gets the unit the fixed price is per.</summary>
    public UnitOfMeasure FixedPriceUnitOfMeasure { get; }

    /// <summary>Gets the floating leg's index.</summary>
    public CommodityIndex Index { get; }

    /// <inheritdoc/>
    /// <remarks>A vanilla swap expires with its last period's end date, where the base reads its payment date.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(PricingPeriods[^1].EndDate).HasOccurred(context);
    }

    /// <inheritdoc/>
    protected override EnergyCommodityResults PerformCalculations(EvaluationContext context)
    {
        ImmutableArray<PricingError>.Builder errors = ImmutableArray.CreateBuilder<PricingError>();
        if (Index.Empty(context))
        {
            if (Index.ForwardCurveEmpty)
            {
                throw new NanoQLException($"index [{Index.Name}] does not have any quotes");
            }

            errors.Add(new PricingError(
                PricingErrorLevel.Warning,
                $"index [{Index.Name}] does not have any quotes; using forward prices from [{ForwardCurveName(Index)}]"));
        }

        Date evaluationDate = context.EvaluationDate;
        CommoditySettings settings = context.CommoditySettings;
        Currency baseCurrency = settings.Currency;
        UnitOfMeasure baseUnitOfMeasure = settings.UnitOfMeasure;
        Quantity firstQuantity = PricingPeriods[0].Quantity;
        CommodityType quantityType = firstQuantity.CommodityType
            ?? throw new NanoQLException("no commodity type data provided");

        double quantityUomConversionFactor = CalculateUomConversionFactor(
            quantityType, UnitOf(firstQuantity), baseUnitOfMeasure, context);
        double fixedPriceUomConversionFactor = CalculateUomConversionFactor(
            quantityType, baseUnitOfMeasure, FixedPriceUnitOfMeasure, context);
        double indexUomConversionFactor = CalculateUomConversionFactor(
            Index.CommodityType, baseUnitOfMeasure, Index.UnitOfMeasure, context);

        double fixedPriceFxConversionFactor = CalculateFxConversionFactor(
            FixedPrice.Currency ?? throw new NanoQLException("no currency data provided"),
            baseCurrency,
            evaluationDate,
            context);
        double indexPriceFxConversionFactor = CalculateFxConversionFactor(
            Index.Currency, baseCurrency, evaluationDate, context);
        // QuantLib's legs: the fixed leg in the pay currency and the floating leg in the receive currency
        Currency payLegCurrency = Payer ? PayCurrency : ReceiveCurrency;
        Currency receiveLegCurrency = Payer ? ReceiveCurrency : PayCurrency;
        double payLegFxConversionFactor = CalculateFxConversionFactor(
            baseCurrency, payLegCurrency, evaluationDate, context);
        double receiveLegFxConversionFactor = CalculateFxConversionFactor(
            baseCurrency, receiveLegCurrency, evaluationDate, context);

        Date? lastQuoteDate = Index.LastQuoteDate(context);
        if (lastQuoteDate is Date last && last < evaluationDate - 1)
        {
            errors.Add(new PricingError(
                PricingErrorLevel.Warning, $"index [{Index.Name}] has last quote date of {Iso(last)}"));
        }

        ImmutableSortedDictionary<Date, EnergyDailyPosition>.Builder positions =
            ImmutableSortedDictionary.CreateBuilder<Date, EnergyDailyPosition>();
        ImmutableSortedDictionary<Date, CommodityCashFlow>.Builder cashFlows =
            ImmutableSortedDictionary.CreateBuilder<Date, CommodityCashFlow>();
        ImmutableSortedDictionary<string, Money> secondaryCostAmounts =
            ImmutableSortedDictionary.Create<string, Money>(StringComparer.Ordinal);
        double npv = 0.0;
        double totalQuantityAmount = 0.0;
        foreach (PricingPeriod period in PricingPeriods)
        {
            if (period.Quantity.Amount == 0)
            {
                throw new NanoQLException("quantity is zero");
            }

            var days = new List<(Date Date, double Pay, double Receive, bool Unrealized)>();
            foreach (Date stepDate in PricingDays(period))
            {
                bool unrealized = stepDate > evaluationDate;
                double? quoteValue = lastQuoteDate is Date lq && stepDate <= lq
                    ? Index.PastFixing(stepDate, context)
                    : Index.ForwardPrice(stepDate, context);
                if (quoteValue == 0)
                {
                    errors.Add(new PricingError(
                        PricingErrorLevel.Warning,
                        $"pay quote value for curve [{Index.Name}] is 0 for date {Iso(stepDate)}"));
                }

                double quote = quoteValue
                    ?? throw new NanoQLException($"curve [{Index.Name}] missing value for pricing date: {stepDate}");
                double fixedLegPriceValue =
                    FixedPrice.Value * fixedPriceUomConversionFactor * fixedPriceFxConversionFactor;
                double floatingLegPriceValue = quote * indexUomConversionFactor * indexPriceFxConversionFactor;
                double payLegPriceValue = Payer ? fixedLegPriceValue : floatingLegPriceValue;
                double receiveLegPriceValue = Payer ? floatingLegPriceValue : fixedLegPriceValue;
                days.Add((stepDate, payLegPriceValue, receiveLegPriceValue, unrealized));
            }

            double periodQuantityAmount = period.Quantity.Amount * quantityUomConversionFactor;
            totalQuantityAmount += periodQuantityAmount;
            (double payLegValue, double receiveLegValue) = SpreadOverDays(days, periodQuantityAmount, positions);
            (double dDelta, CommodityCashFlow cashFlow) = Payment(
                period,
                payLegValue,
                receiveLegValue,
                discountTermStructure,
                payLegTermStructure,
                receiveLegTermStructure,
                payLegCurrency,
                payLegFxConversionFactor,
                receiveLegCurrency,
                receiveLegFxConversionFactor,
                baseCurrency,
                evaluationDate);
            cashFlows[period.PaymentDate] = cashFlow;
            secondaryCostAmounts = CalculateSecondaryCostAmounts(quantityType, totalQuantityAmount, evaluationDate, context);
            npv += dDelta;
        }

        if (cashFlows.Count == 0)
        {
            throw new NanoQLException("no cashflows");
        }

        return Results(npv, secondaryCostAmounts, errors.ToImmutable(), positions.ToImmutable(), cashFlows.ToImmutable());
    }

    // The forward curve's name in a warning, QuantLib's forwardCurve()->name().
    internal static string ForwardCurveName(CommodityIndex index) =>
        index.ForwardCurve?.Name ?? throw new NanoQLException($"index [{index.Name}] has no forward curve");

    private static UnitOfMeasure UnitOf(Quantity q) =>
        q.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided");
}
