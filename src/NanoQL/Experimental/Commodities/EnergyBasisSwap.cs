// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energybasisswap.hpp ql/experimental/commodities/energybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>An energy basis swap: one index's price against another's on each pricing day, plus a basis on one leg.</summary>
/// <remarks>
/// <para>
/// The holder pays the pay index's price and receives the receive index's, the basis added to the pay leg or the
/// receive leg. The basis, a price per its unit in its currency, converts into the base currency from its own currency,
/// as every other price here; QuantLib converts it from the base currency into the basis currency, the reverse
/// (<c>energybasisswap.cpp:126-129</c>; <c>Divergences/EnergyBasisSwapTests.cs</c>); a basis in the base currency is
/// QuantLib's.
/// </para>
/// <para>
/// The last quote date is the earlier of the two indexes'; an index with no fixings has none, and every day is priced
/// from the forward curves, the other index's fixings unread. The spread index is held and read by nothing, as QuantLib's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class EnergyBasisSwap : EnergySwap
{
    private readonly YieldTermStructure payLegTermStructure;
    private readonly YieldTermStructure receiveLegTermStructure;
    private readonly YieldTermStructure discountTermStructure;

    /// <summary>Initializes a new instance of the <see cref="EnergyBasisSwap"/> class.</summary>
    /// <param name="calendar">The pricing calendar.</param>
    /// <param name="spreadIndex">The spread index, which nothing reads; <see langword="null"/> for none.</param>
    /// <param name="payIndex">The pay leg's index.</param>
    /// <param name="receiveIndex">The receive leg's index.</param>
    /// <param name="spreadToPayLeg">Whether the basis is added to the pay leg; otherwise to the receive leg.</param>
    /// <param name="payCurrency">The pay leg's currency.</param>
    /// <param name="receiveCurrency">The receive leg's currency.</param>
    /// <param name="pricingPeriods">The pricing periods; at least one.</param>
    /// <param name="basis">The basis, a price per its unit.</param>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="secondaryCosts">The secondary costs by name; <see langword="null"/> for none.</param>
    /// <param name="payLegTermStructure">The curve discounting the pay leg.</param>
    /// <param name="receiveLegTermStructure">The curve discounting the receive leg.</param>
    /// <param name="discountTermStructure">The curve discounting the base amounts.</param>
    /// <exception cref="ArgumentNullException">An argument but the spread index and the costs is null.</exception>
    /// <exception cref="NanoQLException">There are no pricing periods ("no payment dates").</exception>
    public EnergyBasisSwap(
        Calendar calendar,
        CommodityIndex? spreadIndex,
        CommodityIndex payIndex,
        CommodityIndex receiveIndex,
        bool spreadToPayLeg,
        Currency payCurrency,
        Currency receiveCurrency,
        IReadOnlyList<PricingPeriod> pricingPeriods,
        CommodityUnitCost basis,
        CommodityType commodityType,
        IReadOnlyDictionary<string, object>? secondaryCosts,
        YieldTermStructure payLegTermStructure,
        YieldTermStructure receiveLegTermStructure,
        YieldTermStructure discountTermStructure)
        : base(calendar, payCurrency, receiveCurrency, pricingPeriods, commodityType, secondaryCosts)
    {
        ArgumentNullException.ThrowIfNull(payIndex);
        ArgumentNullException.ThrowIfNull(receiveIndex);
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(payLegTermStructure);
        ArgumentNullException.ThrowIfNull(receiveLegTermStructure);
        ArgumentNullException.ThrowIfNull(discountTermStructure);
        if (PricingPeriods.IsEmpty)
        {
            throw new NanoQLException("no payment dates");
        }

        SpreadIndex = spreadIndex;
        PayIndex = payIndex;
        ReceiveIndex = receiveIndex;
        SpreadToPayLeg = spreadToPayLeg;
        Basis = basis;
        this.payLegTermStructure = payLegTermStructure;
        this.receiveLegTermStructure = receiveLegTermStructure;
        this.discountTermStructure = discountTermStructure;
    }

    /// <summary>Gets the spread index, which nothing reads; <see langword="null"/> for none.</summary>
    public CommodityIndex? SpreadIndex { get; }

    /// <summary>Gets the pay leg's index.</summary>
    public CommodityIndex PayIndex { get; }

    /// <summary>Gets the receive leg's index.</summary>
    public CommodityIndex ReceiveIndex { get; }

    /// <summary>Gets a value indicating whether the basis is added to the pay leg.</summary>
    public bool SpreadToPayLeg { get; }

    /// <summary>Gets the basis, a price per its unit.</summary>
    public CommodityUnitCost Basis { get; }

    /// <inheritdoc/>
    protected override EnergyCommodityResults PerformCalculations(EvaluationContext context)
    {
        ImmutableArray<PricingError>.Builder errors = ImmutableArray.CreateBuilder<PricingError>();
        foreach (CommodityIndex index in (ReadOnlySpan<CommodityIndex>)[PayIndex, ReceiveIndex])
        {
            if (index.Empty(context))
            {
                if (index.ForwardCurveEmpty)
                {
                    throw new NanoQLException($"index [{index.Name}] does not have any quotes or forward prices");
                }

                errors.Add(new PricingError(
                    PricingErrorLevel.Warning,
                    $"index [{index.Name}] does not have any quotes; using forward prices from "
                    + $"[{EnergyVanillaSwap.ForwardCurveName(index)}]"));
            }
        }

        Date evaluationDate = context.EvaluationDate;
        CommoditySettings settings = context.CommoditySettings;
        Currency baseCurrency = settings.Currency;
        UnitOfMeasure baseUnitOfMeasure = settings.UnitOfMeasure;
        Quantity firstQuantity = PricingPeriods[0].Quantity;
        CommodityType quantityType = firstQuantity.CommodityType
            ?? throw new NanoQLException("no commodity type data provided");

        double quantityUomConversionFactor = CalculateUomConversionFactor(
            quantityType,
            firstQuantity.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided"),
            baseUnitOfMeasure,
            context);
        double payIndexUomConversionFactor = CalculateUomConversionFactor(
            PayIndex.CommodityType, baseUnitOfMeasure, PayIndex.UnitOfMeasure, context);
        double receiveIndexUomConversionFactor = CalculateUomConversionFactor(
            ReceiveIndex.CommodityType, baseUnitOfMeasure, ReceiveIndex.UnitOfMeasure, context);

        double payIndexFxConversionFactor = CalculateFxConversionFactor(
            PayIndex.Currency, baseCurrency, evaluationDate, context);
        double receiveIndexFxConversionFactor = CalculateFxConversionFactor(
            ReceiveIndex.Currency, baseCurrency, evaluationDate, context);
        double payLegFxConversionFactor = CalculateFxConversionFactor(baseCurrency, PayCurrency, evaluationDate, context);
        double receiveLegFxConversionFactor = CalculateFxConversionFactor(
            baseCurrency, ReceiveCurrency, evaluationDate, context);

        double basisUomConversionFactor = CalculateUomConversionFactor(
            quantityType,
            baseUnitOfMeasure,
            Basis.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided"),
            context);
        double basisFxConversionFactor = CalculateFxConversionFactor(
            Basis.Amount.Currency ?? throw new NanoQLException("no currency data provided"),
            baseCurrency,
            evaluationDate,
            context);
        double basisValue = Basis.Amount.Value * basisUomConversionFactor * basisFxConversionFactor;

        Date? lastPayIndexQuoteDate = PayIndex.LastQuoteDate(context);
        Date? lastReceiveIndexQuoteDate = ReceiveIndex.LastQuoteDate(context);
        if (lastPayIndexQuoteDate is Date lastPay && lastPay < evaluationDate - 1)
        {
            errors.Add(new PricingError(
                PricingErrorLevel.Warning, $"index [{PayIndex.Name}] has last quote date of {Iso(lastPay)}"));
        }

        if (lastReceiveIndexQuoteDate is Date lastReceive && lastReceive < evaluationDate - 1)
        {
            errors.Add(new PricingError(
                PricingErrorLevel.Warning, $"index [{ReceiveIndex.Name}] has last quote date of {Iso(lastReceive)}"));
        }

        Date? lastQuoteDate = lastPayIndexQuoteDate is Date p && lastReceiveIndexQuoteDate is Date r
            ? (p < r ? p : r)
            : null;

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
            var days = new List<(Date Date, double Pay, double Receive, bool Unrealized)>();
            foreach (Date stepDate in PricingDays(period))
            {
                bool unrealized = stepDate > evaluationDate;
                bool fixedDay = lastQuoteDate is Date lq && stepDate <= lq;
                double? payQuoteValue = fixedDay
                    ? PayIndex.PastFixing(stepDate, context)
                    : PayIndex.ForwardPrice(stepDate, context);
                double? receiveQuoteValue = fixedDay
                    ? ReceiveIndex.PastFixing(stepDate, context)
                    : ReceiveIndex.ForwardPrice(stepDate, context);
                if (payQuoteValue == 0)
                {
                    errors.Add(new PricingError(
                        PricingErrorLevel.Warning,
                        $"pay quote value for curve [{PayIndex.Name}] is 0 for date {Iso(stepDate)}"));
                }

                if (receiveQuoteValue == 0)
                {
                    errors.Add(new PricingError(
                        PricingErrorLevel.Warning,
                        $"receive quote value for curve [{ReceiveIndex.Name}] is 0 for date {Iso(stepDate)}"));
                }

                double payQuote = payQuoteValue
                    ?? throw new NanoQLException($"curve [{PayIndex.Name}] missing value for pricing date: {stepDate}");
                double receiveQuote = receiveQuoteValue
                    ?? throw new NanoQLException(
                        $"curve [{ReceiveIndex.Name}] missing value for pricing date: {stepDate}");
                double payLegPriceValue = payQuote * payIndexUomConversionFactor * payIndexFxConversionFactor;
                double receiveLegPriceValue =
                    receiveQuote * receiveIndexUomConversionFactor * receiveIndexFxConversionFactor;
                if (SpreadToPayLeg)
                {
                    payLegPriceValue += basisValue;
                }
                else
                {
                    receiveLegPriceValue += basisValue;
                }

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
                PayCurrency,
                payLegFxConversionFactor,
                ReceiveCurrency,
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
}
