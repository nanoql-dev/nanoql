// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/discountingmtmcrosscurrencybasisswapengine.hpp ql/experimental/fx/discountingmtmcrosscurrencybasisswapengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>The discounting engine of mark-to-market cross-currency basis swaps.</summary>
/// <remarks>
/// <para>
/// Prices a <see cref="MtMCrossCurrencyBasisSwap"/> by discounting each leg on its own currency curve and converting to
/// the domestic currency through the FX spot. For the resettable leg, the engine prices copies of its FX-linked cash
/// flows holding a <see cref="DiscountingFxResetPricer"/> built from the engine's market data, so pricing leaves the
/// swap's own cash flows unchanged. A future reset is projected to its FX value date from the spot quote and the two
/// discount curves; an already-fixed reset is read from the context's <see cref="EvaluationContext.ExchangeRates"/> on
/// its fixing date.
/// </para>
/// <para>
/// Static (D14): QuantLib's engine object becomes the arguments of <see cref="Calculate"/>.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class DiscountingMtMCrossCurrencyBasisSwapEngine
{
    /// <summary>Prices a mark-to-market cross-currency basis swap.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="domesticCcy">The domestic currency, the NPV's.</param>
    /// <param name="domesticCcyDiscountCurve">The discount curve of domestic-currency flows.</param>
    /// <param name="foreignCcy">The foreign currency.</param>
    /// <param name="foreignCcyDiscountCurve">The discount curve of foreign-currency flows.</param>
    /// <param name="spotFx">Units of the domestic currency per unit of the foreign currency, quoted for settlement on
    /// the FX settlement date.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.</param>
    /// <param name="settlementDate">The settlement date; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="spotFxSettleDate">The settlement date of the FX quote; when <see langword="null"/>, the value date
    /// of a fixing on the evaluation date by the swap's FX-reset convention, or the curves' reference date if the swap
    /// has no fixing calendar.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the currencies is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The curves' reference dates differ; or a date is <c>default(Date)</c> or
    /// before the curves' reference date.</exception>
    /// <exception cref="NanoQLException">A discount factor the FX conversion divides by is zero; a leg fails to price,
    /// or its currency is neither of the two, the message naming the leg; or a fair spread is derived from a zero leg
    /// BPS.</exception>
    public static MtMCrossCurrencyBasisSwapResults Calculate(
        MtMCrossCurrencyBasisSwap swap,
        EvaluationContext context,
        Currency? domesticCcy,
        YieldTermStructure domesticCcyDiscountCurve,
        Currency? foreignCcy,
        YieldTermStructure foreignCcyDiscountCurve,
        double spotFx,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        Date? spotFxSettleDate = null)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        if (domesticCcyDiscountCurve is null)
        {
            throw new ArgumentNullException(
                nameof(domesticCcyDiscountCurve), "Discounting term structure handle is empty.");
        }

        if (foreignCcyDiscountCurve is null)
        {
            throw new ArgumentNullException(
                nameof(foreignCcyDiscountCurve), "Discounting term structure handle is empty.");
        }

        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        Date.ThrowIfDefault(spotFxSettleDate, nameof(spotFxSettleDate));
        MtMCrossCurrencyBasisSwapResults Priced(MtMCrossCurrencyBasisSwap s)
        {
            if (s.IsExpired(context))
            {
                return s.SetupExpired();
            }

            s.Validate();
            MtMCrossCurrencyBasisSwapResults body = Body(
                s,
                context,
                domesticCcy,
                domesticCcyDiscountCurve,
                foreignCcy,
                foreignCcyDiscountCurve,
                spotFx,
                includeSettlementDateFlows,
                settlementDate,
                npvDate,
                spotFxSettleDate);
            return s.FetchResults(body);
        }

        // a leg compounding its spread daily gets its fair spread solved on the swap's pricing (the S-rx plan's C3
        // review, B1-2)
        MtMCrossCurrencyBasisSwapResults results = Priced(swap);
        double Npv(CrossCurrencySwap copy) => Priced((MtMCrossCurrencyBasisSwap)copy).NPV!.Value;
        return results with
        {
            FairFxBaseSpread = DiscountingConstNotionalCrossCurrencySwapEngine.FairSpread(
                swap, 0, swap.FxBaseSpread, results.FairFxBaseSpread, results.NPV, Npv),
            FairFxQuoteSpread = DiscountingConstNotionalCrossCurrencySwapEngine.FairSpread(
                swap, 1, swap.FxQuoteSpread, results.FairFxQuoteSpread, results.NPV, Npv),
        };
    }

    private static MtMCrossCurrencyBasisSwapResults Body(
        MtMCrossCurrencyBasisSwap swap,
        EvaluationContext context,
        Currency? domesticCcy,
        YieldTermStructure domesticCcyDiscountCurve,
        Currency? foreignCcy,
        YieldTermStructure foreignCcyDiscountCurve,
        double spotFx,
        bool? includeSettlementDateFlows,
        Date? settlementDateInput,
        Date? npvDateInput,
        Date? spotFxSettleDateInput)
    {
        if (domesticCcyDiscountCurve.ReferenceDate != foreignCcyDiscountCurve.ReferenceDate)
        {
            throw new ArgumentException(
                "Term structures should have the same reference date.", nameof(foreignCcyDiscountCurve));
        }

        Date referenceDate = domesticCcyDiscountCurve.ReferenceDate;
        Date settlementDate = settlementDateInput ?? referenceDate;
        if (!(settlementDate >= referenceDate))
        {
            throw new ArgumentException(
                $"Settlement date ({settlementDate}) cannot be before discount curve reference date ({referenceDate})",
                "settlementDate");
        }

        Date valuationDate = npvDateInput ?? referenceDate;
        if (!(valuationDate >= referenceDate))
        {
            throw new ArgumentException(
                $"NPV date ({valuationDate}) cannot be before discount curve reference date ({referenceDate})",
                "npvDate");
        }

        Date spotFxSettleDate = spotFxSettleDateInput
            ?? (swap.FxResetFixingCalendar is null
                ? referenceDate
                : new FxResetConvention(swap.FxResetFixingDays, swap.FxResetFixingCalendar).ValueDate(
                    context.EvaluationDate));
        if (!(spotFxSettleDate >= referenceDate))
        {
            throw new ArgumentException(
                $"FX settlement date ({spotFxSettleDate}) cannot be before discount curve reference date " +
                $"({referenceDate})",
                "spotFxSettleDate");
        }

        double domesticFxSettlementDiscount = domesticCcyDiscountCurve.Discount(spotFxSettleDate);
        double foreignFxSettlementDiscount = foreignCcyDiscountCurve.Discount(spotFxSettleDate);
        if (foreignFxSettlementDiscount == 0.0)
        {
            throw new NanoQLException(
                $"Discount factor for currency {Currency.Format(foreignCcy)} at {spotFxSettleDate} cannot be zero");
        }

        double referenceDateFx = spotFx * domesticFxSettlementDiscount / foreignFxSettlementDiscount;

        double domesticValuationDiscount = domesticCcyDiscountCurve.Discount(valuationDate);
        double foreignValuationDiscount = foreignCcyDiscountCurve.Discount(valuationDate);
        if (domesticValuationDiscount == 0.0)
        {
            throw new NanoQLException(
                $"Discount factor for currency {Currency.Format(domesticCcy)} at {valuationDate} cannot be zero");
        }

        double valuationDateFx = referenceDateFx * foreignValuationDiscount / domesticValuationDiscount;

        ImmutableArray<ImmutableArray<CashFlow>> legs = swap.Legs;
        ImmutableArray<double> payer = swap.PayerMultipliers;
        ImmutableArray<Currency?> currencies = swap.Currencies;
        int numLegs = legs.Length;
        bool includeReferenceDateFlows = includeSettlementDateFlows ?? context.IncludeReferenceDateEvents;

        double value = 0.0;
        var legNPV = new double?[numLegs];
        var legBPS = new double?[numLegs];
        var startDiscounts = new double?[numLegs];
        var endDiscounts = new double?[numLegs];
        var inCcyLegNPV = new double?[numLegs];
        var inCcyLegBPS = new double?[numLegs];
        var npvDateDiscounts = new double?[numLegs];
        var fxResetRates = ImmutableArray.CreateBuilder<double>();
        var fxResetNotionals = ImmutableArray.CreateBuilder<double>();

        for (int legNo = 0; legNo < numLegs; ++legNo)
        {
            try
            {
                YieldTermStructure legDiscountCurve, otherLegCurve;
                if (currencies[legNo] == domesticCcy)
                {
                    legDiscountCurve = domesticCcyDiscountCurve;
                    otherLegCurve = foreignCcyDiscountCurve;
                }
                else
                {
                    if (currencies[legNo] != foreignCcy)
                    {
                        throw new NanoQLException(
                            $"leg ccy ({Currency.Format(currencies[legNo])}) must be domesticCcy " +
                            $"({Currency.Format(domesticCcy)}) or foreignCcy ({Currency.Format(foreignCcy)})");
                    }

                    legDiscountCurve = foreignCcyDiscountCurve;
                    otherLegCurve = domesticCcyDiscountCurve;
                }

                npvDateDiscounts[legNo] = legDiscountCurve.Discount(valuationDate);

                ImmutableArray<CashFlow> pricingLeg = legs[legNo];
                if (legNo == swap.ResettingLegIndex)
                {
                    // The resettable leg pays in currency R; the constant-notional leg pays in currency C. Its cash
                    // flows convert at FX rates quoted R per C, which they obtain from a pricer built from this
                    // engine's market data. Price copies, so that the calculation doesn't change the swap's cash
                    // flows.
                    Currency? resettableCcy = currencies[legNo];
                    Currency? constantLegCcy = currencies[swap.ConstantLegIndex];
                    bool spotIsResettablePerConstant = resettableCcy == domesticCcy;
                    var fxResetPricer = new DiscountingFxResetPricer(
                        constantLegCcy,
                        resettableCcy,
                        otherLegCurve,
                        legDiscountCurve,
                        spotFx,
                        spotIsResettablePerConstant,
                        spotFxSettleDate);
                    pricingLeg = FxResetCashFlowsFunctions.SetFxResetPricer(legs[legNo], fxResetPricer);

                    foreach (CashFlow cf in pricingLeg)
                    {
                        if (cf is FxResetCoupon coupon
                            && !coupon.HasOccurred(context, settlementDate, includeReferenceDateFlows))
                        {
                            double fxResetRate = fxResetPricer.FxRate(coupon.FxReset, context);
                            fxResetRates.Add(fxResetRate);
                            fxResetNotionals.Add(coupon.ConstantLegNotional * fxResetRate);
                        }
                    }
                }

                (double npv, double bps) = CashFlows.NpvBps(
                    pricingLeg, legDiscountCurve, context, includeReferenceDateFlows, settlementDate, valuationDate);

                double inCcyNpv = npv * payer[legNo];
                double inCcyBps = bps * payer[legNo];
                inCcyLegNPV[legNo] = inCcyNpv;
                inCcyLegBPS[legNo] = inCcyBps;

                if (currencies[legNo] != domesticCcy)
                {
                    inCcyNpv *= valuationDateFx;
                    inCcyBps *= valuationDateFx;
                }

                legNPV[legNo] = inCcyNpv;
                legBPS[legNo] = inCcyBps;

                Date startDate = CashFlows.StartDate(legs[legNo]);
                startDiscounts[legNo] = startDate >= referenceDate ? legDiscountCurve.Discount(startDate) : null;
                Date maturityDate = CashFlows.MaturityDate(legs[legNo]);
                endDiscounts[legNo] = maturityDate >= referenceDate ? legDiscountCurve.Discount(maturityDate) : null;
                value += inCcyNpv;
            }
            catch (Exception e)
            {
                throw new NanoQLException(
                    $"{DataFormattersFunctions.Ordinal(legNo + 1)} leg: {DiscountingSwapEngine.QuantLibText(e)}", e);
            }
        }

        return new MtMCrossCurrencyBasisSwapResults
        {
            NPV = value,
            ErrorEstimate = null,
            ValuationDate = valuationDate,
            NpvDateDiscount = domesticValuationDiscount,
            LegNPV = [.. legNPV],
            LegBPS = [.. legBPS],
            StartDiscounts = [.. startDiscounts],
            EndDiscounts = [.. endDiscounts],
            InCcyLegNPV = [.. inCcyLegNPV],
            InCcyLegBPS = [.. inCcyLegBPS],
            NpvDateDiscounts = [.. npvDateDiscounts],
            FxResetRates = fxResetRates.ToImmutable(),
            FxResetNotionals = fxResetNotionals.ToImmutable(),
        };
    }
}
