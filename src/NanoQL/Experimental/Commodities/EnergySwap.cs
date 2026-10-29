// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energyswap.hpp ql/experimental/commodities/energyswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The base of energy swaps: a calendar, a pay and a receive currency, and pricing periods.</summary>
/// <remarks>
/// <para>
/// An extension point (U13): <see cref="EnergyVanillaSwap"/> and <see cref="EnergyBasisSwap"/> derive from it. A swap
/// prices each pricing day of each period on its calendar, from the adjusted start, reading its index's stored fixing up
/// to the index's last quote date and its forward price after it. The fixing is read as the index reads it, so a day the
/// index doesn't fix is refused ("… is not a valid fixing date"): a swap's calendar must be its index's on its fixed
/// days, as QuantLib's.
/// </para>
/// <para>
/// Each payment is made in the currency of the leg the net amount goes through, the receive leg's when the discounted
/// amount is positive, at that currency's exchange rate, and discounted on that leg's curve. QuantLib pairs the
/// payment's currency with the other leg's rate in the basis swap and a payer vanilla swap, and a receiver vanilla
/// swap's payment never changes currency, its pay-receive flag 0 where −1 would switch it (the S-xrt plan's XRR5
/// (viii); <c>Divergences/EnergySwapPaymentTests.cs</c>), so only a receiver's payments to the holder are QuantLib's;
/// the NPV, in the base currency, is the same either way.
/// </para>
/// <para>
/// A swap whose index has no fixings prices every day from its forward curve, with QuantLib's warning, where QuantLib's
/// throws "empty timeseries" (XRR5 (vi)). <see cref="Quantity"/> sums the periods' amounts under the first period's unit,
/// as QuantLib's, right for periods in one unit.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class EnergySwap : EnergyCommodity
{
    /// <summary>Initializes a new instance of the <see cref="EnergySwap"/> class.</summary>
    /// <param name="calendar">The pricing calendar.</param>
    /// <param name="payCurrency">The pay currency: the basis swap's pay leg's, the vanilla swap's fixed leg's.</param>
    /// <param name="receiveCurrency">The receive currency: the basis swap's receive leg's, the vanilla swap's floating
    /// leg's.</param>
    /// <param name="pricingPeriods">The pricing periods.</param>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="secondaryCosts">The secondary costs by name; <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">An argument but the costs is null, or a period is.</exception>
    protected EnergySwap(
        Calendar calendar,
        Currency payCurrency,
        Currency receiveCurrency,
        IReadOnlyList<PricingPeriod> pricingPeriods,
        CommodityType commodityType,
        IReadOnlyDictionary<string, object>? secondaryCosts)
        : base(commodityType, secondaryCosts)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(payCurrency);
        ArgumentNullException.ThrowIfNull(receiveCurrency);
        ArgumentNullException.ThrowIfNull(pricingPeriods);
        foreach (PricingPeriod p in pricingPeriods)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(pricingPeriods));
        }

        Calendar = calendar;
        PayCurrency = payCurrency;
        ReceiveCurrency = receiveCurrency;
        PricingPeriods = [.. pricingPeriods];
    }

    /// <summary>Gets the pricing calendar.</summary>
    public Calendar Calendar { get; }

    /// <summary>Gets the pay currency: the basis swap's pay leg's, the vanilla swap's fixed leg's.</summary>
    public Currency PayCurrency { get; }

    /// <summary>Gets the receive currency: the basis swap's receive leg's, the vanilla swap's floating leg's.</summary>
    public Currency ReceiveCurrency { get; }

    /// <summary>Gets the pricing periods.</summary>
    public ImmutableArray<PricingPeriod> PricingPeriods { get; }

    /// <summary>Gets the commodity type: the first period's quantity's, which QuantLib's swap returns in place of the
    /// one it was built with.</summary>
    /// <exception cref="NanoQLException">The swap has no pricing periods, or the first's quantity has no commodity
    /// type.</exception>
    public override CommodityType CommodityType => FirstPeriodQuantity().CommodityType
        ?? throw new NanoQLException("no commodity type data provided");

    /// <summary>Gets the quantity: the periods' amounts summed, in the first period's unit and commodity type.</summary>
    /// <exception cref="NanoQLException">The swap has no pricing periods.</exception>
    public override Quantity Quantity
    {
        get
        {
            Quantity first = FirstPeriodQuantity();
            double total = 0.0;
            foreach (PricingPeriod p in PricingPeriods)
            {
                total += p.Quantity.Amount;
            }

            return new Quantity(first.CommodityType, first.UnitOfMeasure, total);
        }
    }

    /// <inheritdoc/>
    /// <remarks>A swap expires with its last payment date, or has none without periods.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return PricingPeriods.IsEmpty || new SimpleEvent(PricingPeriods[^1].PaymentDate).HasOccurred(context);
    }

    /// <summary>
    /// Prices the swap: QuantLib's <c>Instrument::calculate</c> with the swap's own calculation, in the context's base
    /// currency and unit.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The NPV and what the calculation records; an expired swap's NPV is zero, with nothing else.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The calculation can't price a day or a cost.</exception>
    public EnergyCommodityResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsExpired(context))
        {
            InstrumentResults expired = SetupExpired();
            return new EnergyCommodityResults
            {
                NPV = expired.NPV,
                ErrorEstimate = expired.ErrorEstimate,
                ValuationDate = expired.ValuationDate,
                AdditionalResults = expired.AdditionalResults,
            };
        }

        return PerformCalculations(context);
    }

    /// <summary>The swap's own calculation, QuantLib's <c>performCalculations</c>, for a swap that isn't expired.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results.</returns>
    protected abstract EnergyCommodityResults PerformCalculations(EvaluationContext context);

    // A date as QuantLib's io::iso_date writes it into a message.
    internal static string Iso(Date d) => d.ToDateOnly().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // The pricing days of a period with their two prices: from the calendar-adjusted start to the end, each the next
    // business day; QuantLib's first loop over a period.
    private protected IEnumerable<Date> PricingDays(PricingPeriod period)
    {
        for (Date d = Calendar.Adjust(period.StartDate); d <= period.EndDate; d = Calendar.Advance(d, 1, TimeUnit.Days))
        {
            yield return d;
        }
    }

    // A period's priced days as daily positions, the period's quantity spread evenly over them, and the two legs'
    // values: QuantLib's second loop over a period.
    private protected static (double PayLegValue, double ReceiveLegValue) SpreadOverDays(
        List<(Date Date, double Pay, double Receive, bool Unrealized)> days,
        double periodQuantityAmount,
        ImmutableSortedDictionary<Date, EnergyDailyPosition>.Builder positions)
    {
        double avgDailyQuantityAmount = days.Count == 0 ? 0.0 : periodQuantityAmount / days.Count;
        double payLegValue = 0.0;
        double receiveLegValue = 0.0;
        foreach ((Date date, double pay, double receive, bool unrealized) in days)
        {
            positions[date] = new EnergyDailyPosition(
                date, pay, receive, avgDailyQuantityAmount, (-pay + receive) * avgDailyQuantityAmount, unrealized);
            payLegValue += -pay * avgDailyQuantityAmount;
            receiveLegValue += receive * avgDailyQuantityAmount;
        }

        return (payLegValue, receiveLegValue);
    }

    // A period's payment: its legs discounted on their curves from two days after the evaluation date, the payment in
    // the currency of the leg the net amount goes through at that currency's rate (XRR5 (viii)).
    private protected (double DDelta, CommodityCashFlow CashFlow) Payment(
        PricingPeriod period,
        double payLegValue,
        double receiveLegValue,
        YieldTermStructure discount,
        YieldTermStructure payLeg,
        YieldTermStructure receiveLeg,
        Currency payLegCurrency,
        double payLegFxConversionFactor,
        Currency receiveLegCurrency,
        double receiveLegFxConversionFactor,
        Currency baseCurrency,
        Date evaluationDate)
    {
        double discountFactor = 1.0;
        double payLegDiscountFactor = 1.0;
        double receiveLegDiscountFactor = 1.0;
        if (period.PaymentDate >= evaluationDate + 2)
        {
            discountFactor = discount.Discount(period.PaymentDate);
            payLegDiscountFactor = payLeg.Discount(period.PaymentDate);
            receiveLegDiscountFactor = receiveLeg.Discount(period.PaymentDate);
        }

        double uDelta = receiveLegValue + payLegValue;
        double dDelta = (receiveLegValue * receiveLegDiscountFactor) + (payLegValue * payLegDiscountFactor);
        bool received = dDelta > 0;
        double pmtFxConversionFactor = received ? receiveLegFxConversionFactor : payLegFxConversionFactor;
        Currency pmtCurrency = received ? receiveLegCurrency : payLegCurrency;
        double pmtDiscountFactor = received ? receiveLegDiscountFactor : payLegDiscountFactor;
        var cashFlow = new CommodityCashFlow(
            period.PaymentDate,
            new Money(baseCurrency, uDelta * discountFactor),
            new Money(baseCurrency, uDelta),
            new Money(pmtCurrency, dDelta * pmtFxConversionFactor),
            new Money(pmtCurrency, uDelta * pmtFxConversionFactor),
            discountFactor,
            pmtDiscountFactor,
            period.PaymentDate <= evaluationDate);
        return (dDelta, cashFlow);
    }

    // The results of a calculation: the NPV less the secondary costs, and the daily positions also as QuantLib's
    // additional result.
    private protected static EnergyCommodityResults Results(
        double npv,
        ImmutableSortedDictionary<string, Money> secondaryCostAmounts,
        ImmutableArray<PricingError> pricingErrors,
        ImmutableSortedDictionary<Date, EnergyDailyPosition> dailyPositions,
        ImmutableSortedDictionary<Date, CommodityCashFlow> paymentCashFlows)
    {
        foreach (Money amount in secondaryCostAmounts.Values)
        {
            npv -= amount.Value;
        }

        return new EnergyCommodityResults
        {
            NPV = npv,
            PricingErrors = pricingErrors,
            SecondaryCostAmounts = secondaryCostAmounts,
            DailyPositions = dailyPositions,
            PaymentCashFlows = paymentCashFlows,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("dailyPositions", dailyPositions),
        };
    }

    private Quantity FirstPeriodQuantity() =>
        !PricingPeriods.IsEmpty ? PricingPeriods[0].Quantity : throw new NanoQLException("no pricing periods");
}
