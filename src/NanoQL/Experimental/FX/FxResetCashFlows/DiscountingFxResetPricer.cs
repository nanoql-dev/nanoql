// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/fxresetcashflows.hpp ql/experimental/fx/fxresetcashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The FX-reset pricer based on spot FX and two discount curves.</summary>
/// <remarks>
/// <para>
/// The returned FX rate is expressed in units of resettable-leg currency per unit of constant-leg currency. Historical
/// observations are obtained from the context's <see cref="EvaluationContext.ExchangeRates"/> on the reset fixing date;
/// future observations are projected to the reset value date.
/// </para>
/// <para>
/// Immutable, its spot a <see langword="double"/>. A reset fixed today is read from the registry unless the context
/// enforces today's historic fixings, and forecast when the registry holds no rate for it, the only refusal QuantLib's
/// comment means ("A today's fixing may not have been published yet"); QuantLib forecasts on any refusal, a published
/// non-positive fixing among them, which a fixing on a past date refuses
/// (<c>tests/NanoQL.Tests/Divergences/FxResetTodaysFixingTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class DiscountingFxResetPricer : FxResetPricer
{
    private readonly YieldTermStructure constantLegCurve;
    private readonly YieldTermStructure resettableLegCurve;
    private readonly double spotFx;
    private readonly bool spotIsResettablePerConstant;
    private readonly Date? spotFxSettleDate;

    /// <summary>Initializes a new instance of the <see cref="DiscountingFxResetPricer"/> class.</summary>
    /// <param name="constantLegCurrency">The constant leg's currency.</param>
    /// <param name="resettableLegCurrency">The resettable leg's currency.</param>
    /// <param name="constantLegCurve">The constant leg currency's discount curve.</param>
    /// <param name="resettableLegCurve">The resettable leg currency's discount curve.</param>
    /// <param name="spotFx">The spot FX rate.</param>
    /// <param name="spotIsResettablePerConstant">Whether the spot is quoted as units of the resettable leg's currency
    /// per unit of the constant leg's; otherwise it is inverted.</param>
    /// <param name="spotFxSettleDate">The settlement date of the spot; the resettable curve's reference date when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="spotFxSettleDate"/> is <c>default(Date)</c>.</exception>
    public DiscountingFxResetPricer(
        Currency? constantLegCurrency,
        Currency? resettableLegCurrency,
        YieldTermStructure constantLegCurve,
        YieldTermStructure resettableLegCurve,
        double spotFx,
        bool spotIsResettablePerConstant,
        Date? spotFxSettleDate = null)
    {
        if (constantLegCurve is null)
        {
            throw new ArgumentNullException(nameof(constantLegCurve), "Discounting term structure handle is empty.");
        }

        if (resettableLegCurve is null)
        {
            throw new ArgumentNullException(nameof(resettableLegCurve), "Discounting term structure handle is empty.");
        }

        Date.ThrowIfDefault(spotFxSettleDate, nameof(spotFxSettleDate));
        ConstantLegCurrency = constantLegCurrency;
        ResettableLegCurrency = resettableLegCurrency;
        this.constantLegCurve = constantLegCurve;
        this.resettableLegCurve = resettableLegCurve;
        this.spotFx = spotFx;
        this.spotIsResettablePerConstant = spotIsResettablePerConstant;
        this.spotFxSettleDate = spotFxSettleDate;
    }

    /// <summary>Gets the constant leg's currency.</summary>
    public Currency? ConstantLegCurrency { get; }

    /// <summary>Gets the resettable leg's currency.</summary>
    public Currency? ResettableLegCurrency { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// A past fixing, or today's where the context enforces it, is missing from the registry or isn't positive; the
    /// spot is zero where it is inverted; or a discount factor the projection divides by is zero.
    /// </exception>
    public override double FxRate(FxReset reset, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date referenceDate = resettableLegCurve.ReferenceDate;
        Date today = context.EvaluationDate;

        if (reset.FixingDate < today || (reset.FixingDate == today && context.EnforcesTodaysHistoricFixings))
        {
            try
            {
                return HistoricalRate(reset.FixingDate, context);
            }
            catch (ExchangeRates.NoConversionException e)
            {
                throw new NanoQLException(e.Message);
            }
        }

        if (reset.FixingDate == today)
        {
            try
            {
                return HistoricalRate(reset.FixingDate, context);
            }
            catch (ExchangeRates.NoConversionException)
            {
                // A today's fixing may not have been published yet; forecast it.
            }
        }

        double spot = spotFx;
        if (!spotIsResettablePerConstant)
        {
            if (spot == 0.0)
            {
                throw new NanoQLException("FX spot quote cannot be zero");
            }

            spot = 1.0 / spot;
        }

        Date settleDate = spotFxSettleDate ?? referenceDate;
        double constantSettleDiscount = constantLegCurve.Discount(settleDate);
        if (constantSettleDiscount == 0.0)
        {
            throw new NanoQLException(
                $"Discount factor for currency {Currency.Format(ConstantLegCurrency)} at {settleDate} cannot be zero");
        }

        double referenceDateFx = spot * resettableLegCurve.Discount(settleDate) / constantSettleDiscount;

        double resettableResetDiscount = resettableLegCurve.Discount(reset.ValueDate);
        if (resettableResetDiscount == 0.0)
        {
            throw new NanoQLException(
                $"Discount factor for currency {Currency.Format(ResettableLegCurrency)} at {reset.ValueDate} " +
                "cannot be zero");
        }

        return referenceDateFx * constantLegCurve.Discount(reset.ValueDate) / resettableResetDiscount;
    }

    // QuantLib's historicalRate, its missing rate raised as the registry's internal exception for the caller to tell
    // apart from other refusals.
    private double HistoricalRate(Date fixingDate, EvaluationContext context)
    {
        ExchangeRate exchangeRate = context.ExchangeRates.LookupCore(
            ConstantLegCurrency, ResettableLegCurrency, fixingDate, ExchangeRateType.Derived);
        double fx = exchangeRate.Exchange(new Money(1.0, ConstantLegCurrency)).Value;
        if (!(fx > 0.0))
        {
            throw new NanoQLException(
                $"FX fixing from {Currency.Format(ConstantLegCurrency)} to {Currency.Format(ResettableLegCurrency)} " +
                $"on {fixingDate} must be positive");
        }

        return fx;
    }
}
