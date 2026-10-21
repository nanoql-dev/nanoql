// SPDX-FileCopyrightText: Copyright (C) 2026 Chirag Desai
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/discountingfxforwardengine.hpp ql/pricingengines/forward/discountingfxforwardengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The discounting engine for <see cref="FxForward"/>: each leg discounted on its currency's curve from the settlement
/// date to maturity, the target leg converted at the spot rate, and the result discounted to each curve's reference
/// date.
/// </summary>
/// <remarks>
/// <para>
/// In the source currency, the NPV is <c>(±N_source·D_source(τ,T) ∓ N_target·D_target(τ,T) / S)·D_source(τ)</c>, the
/// signs by the side paid, where <c>D(τ,T)</c> is a curve's discount factor from the settlement date <c>τ</c> to the
/// maturity <c>T</c> and <c>S</c> the spot rate, units of the target per unit of the source; the fair forward rate is
/// <c>S·D_source(τ,T) / D_target(τ,T)</c>. The results' NPV is the source currency's, and each NPV is as of its own
/// curve's reference date.
/// </para>
/// <para>
/// The additional results are QuantLib's: "spotFx", "sourceCurrencyDiscountFactor", "targetCurrencyDiscountFactor",
/// "sourceCurrencySettlementDiscountFactor", "targetCurrencySettlementDiscountFactor", "sourceCurrencyPV" and
/// "targetCurrencyPV". QuantLib's inspectors of the engine's curves and quote aren't ported: they are the arguments.
/// </para>
/// </remarks>
public static class DiscountingFxForwardEngine
{
    /// <summary>Prices an FX forward.</summary>
    /// <param name="forward">The forward.</param>
    /// <param name="context">The evaluation context, whose evaluation date sets the settlement date.</param>
    /// <param name="sourceCurrencyDiscountCurve">The source currency's discount curve.</param>
    /// <param name="targetCurrencyDiscountCurve">The target currency's discount curve.</param>
    /// <param name="spotFx">The spot rate, units of the target currency per unit of the source.</param>
    /// <returns>The forward's results.</returns>
    /// <exception cref="ArgumentNullException">The forward, the context or a curve is <see langword="null"/>, a curve
    /// with QuantLib's text.</exception>
    /// <exception cref="ArgumentException">
    /// A curve's reference date is after the settlement date, or the spot rate isn't positive.
    /// </exception>
    /// <exception cref="NanoQLException">A curve can't be read at the settlement or maturity date.</exception>
    public static FxForwardResults Calculate(
        FxForward forward,
        EvaluationContext context,
        YieldTermStructure sourceCurrencyDiscountCurve,
        YieldTermStructure targetCurrencyDiscountCurve,
        double spotFx)
    {
        ArgumentNullException.ThrowIfNull(forward);
        ArgumentNullException.ThrowIfNull(context);
        if (sourceCurrencyDiscountCurve is null)
        {
            throw new ArgumentNullException(
                nameof(sourceCurrencyDiscountCurve), "source currency discount curve handle is empty");
        }

        if (targetCurrencyDiscountCurve is null)
        {
            throw new ArgumentNullException(
                nameof(targetCurrencyDiscountCurve), "target currency discount curve handle is empty");
        }

        if (forward.IsExpired(context))
        {
            return forward.SetupExpired();
        }

        forward.Validate();
        FxForwardResults results = Body(
            forward, context, sourceCurrencyDiscountCurve, targetCurrencyDiscountCurve, spotFx);
        return forward.FetchResults(results);
    }

    private static FxForwardResults Body(
        FxForward forward,
        EvaluationContext context,
        YieldTermStructure sourceCurrencyDiscountCurve,
        YieldTermStructure targetCurrencyDiscountCurve,
        double spotFx)
    {
        Date maturityDate = forward.MaturityDate;
        Date settlementDate = forward.SettlementDate(context);

        // Validate that curve reference dates are on or before settlement date
        Date sourceRefDate = sourceCurrencyDiscountCurve.ReferenceDate;
        Date targetRefDate = targetCurrencyDiscountCurve.ReferenceDate;
        if (!(sourceRefDate <= settlementDate))
        {
            throw new ArgumentException(
                $"source currency discount curve reference date ({sourceRefDate}) must be on or before settlement date "
                + $"({settlementDate})",
                nameof(sourceCurrencyDiscountCurve));
        }

        if (!(targetRefDate <= settlementDate))
        {
            throw new ArgumentException(
                $"target currency discount curve reference date ({targetRefDate}) must be on or before settlement date "
                + $"({settlementDate})",
                nameof(targetCurrencyDiscountCurve));
        }

        // Get the spot FX rate (targetCurrency/sourceCurrency)
        double spotFxRate = spotFx;
        if (!(spotFxRate > 0.0))
        {
            throw new ArgumentException("spot FX rate must be positive", nameof(spotFx));
        }

        // Get discount factors from settlement to maturity
        double dfSource = sourceCurrencyDiscountCurve.Discount(maturityDate)
            / sourceCurrencyDiscountCurve.Discount(settlementDate);
        double dfTarget = targetCurrencyDiscountCurve.Discount(maturityDate)
            / targetCurrencyDiscountCurve.Discount(settlementDate);
        double dfSourceSettlement = sourceCurrencyDiscountCurve.Discount(settlementDate);
        double dfTargetSettlement = targetCurrencyDiscountCurve.Discount(settlementDate);

        // Calculate fair forward rate: F = S * dfSource / dfTarget
        double fairForwardRate = spotFxRate * dfSource / dfTarget;

        // Calculate settlement-date present values of each leg
        double pvSource = forward.SourceNominal * dfSource;
        double pvTarget = forward.TargetNominal * dfTarget;

        // Convert target currency PV to source currency using spot FX rate
        double pvTargetInSourceCurrency = pvTarget / spotFxRate;

        // NPV = -PVSource + PVTarget when paying the source currency, +PVSource - PVTarget otherwise (source terms)
        double npvAtSettlementInSourceCurrency = forward.PaySourceCurrency
            ? -pvSource + pvTargetInSourceCurrency
            : pvSource - pvTargetInSourceCurrency;

        double npvInSourceCurrency = npvAtSettlementInSourceCurrency * dfSourceSettlement;
        double npvInTargetCurrency = npvAtSettlementInSourceCurrency * spotFxRate * dfTargetSettlement;

        ImmutableSortedDictionary<string, object>.Builder additional =
            ImmutableSortedDictionary.CreateBuilder<string, object>();
        additional["spotFx"] = spotFxRate;
        additional["sourceCurrencyDiscountFactor"] = dfSource;
        additional["targetCurrencyDiscountFactor"] = dfTarget;
        additional["sourceCurrencySettlementDiscountFactor"] = dfSourceSettlement;
        additional["targetCurrencySettlementDiscountFactor"] = dfTargetSettlement;
        additional["sourceCurrencyPV"] = pvSource;
        additional["targetCurrencyPV"] = pvTarget;

        // NPV is as of the curve reference date
        return new FxForwardResults
        {
            NPV = npvInSourceCurrency,
            ErrorEstimate = null,
            FairForwardRate = fairForwardRate,
            NpvSourceCurrency = npvInSourceCurrency,
            NpvTargetCurrency = npvInTargetCurrency,
            AdditionalResults = additional.ToImmutable(),
        };
    }
}
