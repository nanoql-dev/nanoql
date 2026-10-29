// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commoditypricinghelpers.hpp ql/experimental/commodities/commoditypricinghelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// QuantLib's <c>CommodityPricingHelper</c>, a class of static members: the conversion factors, a unit cost in the base
/// currency and unit, and an energy swap's pricing periods.
/// </summary>
/// <remarks>QuantLib declares a constructor it defines nowhere, so the class is never built; here it is a static
/// class.</remarks>
[Experimental("NQL0001")]
public static class CommodityPricingHelperFunctions
{
    /// <summary>
    /// The factor converting an amount in one currency into another at a date, through a direct lookup in the context's
    /// exchange rates: QuantLib's <c>CommodityPricingHelper::calculateFxConversionFactor</c>, whose lookup, unlike
    /// <c>EnergyCommodity</c>'s, is direct.
    /// </summary>
    /// <param name="fromCurrency">The amount's currency.</param>
    /// <param name="toCurrency">The currency it is converted into.</param>
    /// <param name="evaluationDate">The date of the rates.</param>
    /// <param name="context">The evaluation context, whose exchange rates are read.</param>
    /// <returns>1 between a currency and itself; otherwise the direct rate, or its reciprocal where it runs the other
    /// way.</returns>
    /// <exception cref="ArgumentNullException">A currency or the context is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="evaluationDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">No direct rate is held.</exception>
    public static double CalculateFxConversionFactor(
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
            ExchangeRate rate = context.ExchangeRates.Lookup(
                fromCurrency, toCurrency, context, evaluationDate, ExchangeRateType.Direct);
            return fromCurrency != rate.Source ? 1.0 / rate.Rate : rate.Rate;
        }

        return 1.0;
    }

    /// <summary>
    /// The value of one unit of a unit of measure in another, for a commodity type: QuantLib's
    /// <c>CommodityPricingHelper::calculateUomConversionFactor</c>, direction-aware.
    /// </summary>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="fromUnitOfMeasure">The unit converted from.</param>
    /// <param name="toUnitOfMeasure">The unit converted into.</param>
    /// <param name="context">The evaluation context, whose unit conversions are read.</param>
    /// <returns>1 between a unit and itself; otherwise how many of the second unit one of the first is worth, the
    /// conversion's own <see cref="UnitOfMeasureConversion.Convert"/> of a unit quantity.</returns>
    /// <remarks>QuantLib returns the looked-up conversion's stored factor whatever its orientation, so its factor is
    /// one of the first in the second for 25 of the 30 ordered pairs of its six petroleum units and the reciprocal for
    /// five (BBL→MB, BBL→kl, BBL→KL_tk, GAL→BBL, l→BBL; the S-xrt plan's XRR5 (i)).</remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">No conversion is held.</exception>
    public static double CalculateUomConversionFactor(
        CommodityType commodityType,
        UnitOfMeasure fromUnitOfMeasure,
        UnitOfMeasure toUnitOfMeasure,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(commodityType);
        ArgumentNullException.ThrowIfNull(fromUnitOfMeasure);
        ArgumentNullException.ThrowIfNull(toUnitOfMeasure);
        ArgumentNullException.ThrowIfNull(context);
        if (toUnitOfMeasure != fromUnitOfMeasure)
        {
            UnitOfMeasureConversion conversion =
                context.UnitOfMeasureConversions.Lookup(commodityType, fromUnitOfMeasure, toUnitOfMeasure);
            return conversion.ValueOfOne(commodityType, fromUnitOfMeasure);
        }

        return 1.0;
    }

    /// <summary>
    /// A unit cost as money per base unit in a base currency: QuantLib's <c>CommodityPricingHelper::calculateUnitCost</c>,
    /// the cost converted by the factor from the base unit to its unit (a price) and from its currency to the base
    /// currency, through a direct rate.
    /// </summary>
    /// <param name="commodityType">The commodity type.</param>
    /// <param name="unitCost">The unit cost.</param>
    /// <param name="baseCurrency">The base currency.</param>
    /// <param name="baseUnitOfMeasure">The base unit.</param>
    /// <param name="evaluationDate">The date of the exchange rates.</param>
    /// <param name="context">The evaluation context, whose unit conversions and exchange rates are read.</param>
    /// <returns>The cost per base unit; 0 for a zero cost.</returns>
    /// <remarks>QuantLib converts the cost by its lookup's stored factor between the cost's unit and the base unit,
    /// which inverts a price where that factor is stored from the cost's unit: a cost of 0.1 a thousand-barrels reads
    /// 100 a barrel there and 1e-4 here; a gallon's is stored from the barrel, so 0.1 a gallon reads 4.2 a barrel in
    /// both (the S-xrt plan's XRR5 (i)).</remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The cost's currency or unit is QuantLib's empty one, or a conversion or rate
    /// can't be found.</exception>
    public static double CalculateUnitCost(
        CommodityType commodityType,
        CommodityUnitCost unitCost,
        Currency baseCurrency,
        UnitOfMeasure baseUnitOfMeasure,
        Date evaluationDate,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(unitCost);
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(baseUnitOfMeasure);
        if (unitCost.Amount.Value != 0)
        {
            double uomFactor = CalculateUomConversionFactor(
                commodityType,
                baseUnitOfMeasure,
                unitCost.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided"),
                context);
            double fxFactor = CalculateFxConversionFactor(
                unitCost.Amount.Currency ?? throw new NanoQLException("no currency data provided"),
                baseCurrency,
                evaluationDate,
                context);
            return unitCost.Amount.Value * uomFactor * fxFactor;
        }

        return 0.0;
    }

    /// <summary>
    /// An energy swap's pricing periods between two dates: QuantLib's <c>createPricingPeriods</c>, a monthly period
    /// from each start, <c>[start, start + 1M − 1]</c>, paid on the payment term's date after its end.
    /// </summary>
    /// <param name="startDate">The first period's start.</param>
    /// <param name="endDate">The date the last period starts before.</param>
    /// <param name="quantity">The quantity per period of the periodicity.</param>
    /// <param name="deliverySchedule">The delivery schedule: <see cref="EnergyCommodityDeliverySchedule.Monthly"/>,
    /// each period the monthly quantity, or <see cref="EnergyCommodityDeliverySchedule.Daily"/>, each the daily
    /// quantity times its days.</param>
    /// <param name="qtyPeriodicity">The quantity's periodicity: per month for a monthly schedule, per day for a daily
    /// one.</param>
    /// <param name="paymentTerm">The payment term.</param>
    /// <returns>The periods, which QuantLib appends to a list it is given; none when the start isn't before the
    /// end.</returns>
    /// <remarks>
    /// <para>
    /// A daily period's quantity is the daily quantity times its days, both ends counted: QuantLib's counts
    /// <c>end − start</c>, one day short, 300 barrels for January at 10 a day (the S-xrt plan's XRR5 (iv),
    /// <c>Divergences/PricingPeriodTests.cs</c>; FpML's <c>PerCalendarDay</c>, "deemed to apply once per Calendar Day").
    /// </para>
    /// <para>
    /// The other schedules are refused, where QuantLib returns no period and its swaps then refuse "no pricing
    /// periods" or price as expired: its two branches read a schedule two ways, monthly as periods of its own tenor and
    /// daily as monthly periods summing its quantity, and no QuantLib case or caller says which a weekly, quarterly or
    /// yearly schedule means, while a constant, window or hourly one has no tenor (XRR5 (v)). A caller builds such
    /// periods directly. A period starting on a month's last day ends the day before the same day next month (31
    /// January to 27 February), and the last period may run past the end date, as QuantLib's.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="paymentTerm"/> is null.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The periodicity doesn't fit the schedule, or the schedule is neither monthly nor
    /// daily ("Invalid period quantity/pricing period combination.").</exception>
    public static ImmutableArray<PricingPeriod> CreatePricingPeriods(
        Date startDate,
        Date endDate,
        Quantity quantity,
        EnergyCommodityDeliverySchedule deliverySchedule,
        EnergyCommodityQuantityPeriodicity qtyPeriodicity,
        PaymentTerm paymentTerm)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        EnumArguments.Check(deliverySchedule, nameof(deliverySchedule));
        EnumArguments.Check(qtyPeriodicity, nameof(qtyPeriodicity));
        ArgumentNullException.ThrowIfNull(paymentTerm);
        ImmutableArray<PricingPeriod>.Builder periods = ImmutableArray.CreateBuilder<PricingPeriod>();
        var oneMonth = new Period(1, TimeUnit.Months);
        if (deliverySchedule == EnergyCommodityDeliverySchedule.Monthly)
        {
            if (qtyPeriodicity != EnergyCommodityQuantityPeriodicity.PerMonth)
            {
                throw new NanoQLException("Invalid period quantity/pricing period combination.");
            }

            for (Date periodStartDate = startDate; periodStartDate < endDate;)
            {
                Date periodEndDate = (periodStartDate + oneMonth) - 1;
                Date paymentDate = paymentTerm.GetPaymentDate(periodEndDate);
                periods.Add(new PricingPeriod(periodStartDate, periodEndDate, paymentDate, quantity));
                periodStartDate = periodEndDate + 1;
            }
        }
        else if (deliverySchedule == EnergyCommodityDeliverySchedule.Daily)
        {
            if (qtyPeriodicity != EnergyCommodityQuantityPeriodicity.PerDay)
            {
                throw new NanoQLException("Invalid period quantity/pricing period combination.");
            }

            for (Date periodStartDate = startDate; periodStartDate < endDate;)
            {
                Date periodEndDate = (periodStartDate + oneMonth) - 1;
                Quantity periodQuantity = quantity * ((periodEndDate - periodStartDate) + 1);
                Date paymentDate = paymentTerm.GetPaymentDate(periodEndDate);
                periods.Add(new PricingPeriod(periodStartDate, periodEndDate, paymentDate, periodQuantity));
                periodStartDate = periodEndDate + 1;
            }
        }
        else
        {
            throw new NanoQLException(
                $"Invalid period quantity/pricing period combination: a {deliverySchedule} delivery schedule yields no "
                + "pricing periods.");
        }

        return periods.ToImmutable();
    }
}
