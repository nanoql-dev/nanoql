// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016, 2017 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Paul Giltinan
// SPDX-FileCopyrightText: Copyright (C) 2017 Werner Kuerzinger
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/blackswaptionengine.hpp ql/pricingengines/swaption/blackswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The generic Black-style-formula swaption engine that the Black and Bachelier swaption engines share: QuantLib's
/// <c>detail::BlackStyleSwaptionEngine&lt;Spec&gt;</c>, its formula the spec's.
/// </summary>
/// <typeparam name="TSpec">The formula.</typeparam>
/// <remarks>
/// <para>
/// The engine assumes that the exercise date lies before the start date of the passed swap. Volatilities are quoted
/// for zero-spreaded swaps, so a spread on the floating leg is removed with a corresponding correction of the strike
/// and the forward rate. The eleven additional results, each a <see cref="double"/>, are QuantLib's:
/// <c>spreadCorrection</c>, <c>strike</c>, <c>atmForward</c>, <c>annuity</c>, <c>swapLength</c>, <c>stdDev</c>,
/// <c>vega</c>, <c>delta</c>, <c>timeToExpiry</c>, <c>impliedVolatility</c> and <c>forwardPrice</c>.
/// </para>
/// <para>
/// The underlying swap is priced by a <see cref="DiscountingSwapEngine"/> on the discount curve inside the call, and
/// nothing is set on the caller's swap, where QuantLib's engine sets that engine on it.
/// </para>
/// </remarks>
internal static class BlackStyleSwaptionEngine<TSpec>
    where TSpec : struct, IBlackStyleSwaptionSpec
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>
    /// The constant volatility that QuantLib's engine constructors on a volatility build: a
    /// <see cref="ConstantSwaptionVolatility"/> on no calendar, whose reference date is the evaluation date, with the
    /// formula's volatility type.
    /// </summary>
    /// <param name="vol">The volatility.</param>
    /// <param name="dc">The volatility's day counter.</param>
    /// <param name="displacement">The volatility's shift.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The volatility structure.</returns>
    internal static ConstantSwaptionVolatility ConstantVolatility(
        double vol, DayCounter dc, double displacement, EvaluationContext context) =>
        ConstantSwaptionVolatility.Create(
            0, new NullCalendar(), BusinessDayConvention.Following, vol, dc, context, TSpec.Type, displacement);

    /// <summary>
    /// Prices a swaption through QuantLib's <c>Instrument::calculate</c>: its expired results, or its checks, the body
    /// and its results.
    /// </summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The volatility structure.</param>
    /// <param name="model">The cash annuity model.</param>
    /// <returns>The results.</returns>
    internal static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        SwaptionVolatilityStructure vol,
        CashAnnuityModel model)
    {
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();
        InstrumentResults results = Body(swaption, context, discountCurve, vol, model);
        return swaption.FetchResults(results);
    }

    /// <summary>The engine's body: QuantLib's <c>calculate()</c>.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The volatility structure.</param>
    /// <param name="model">The cash annuity model.</param>
    /// <returns>The results.</returns>
    internal static InstrumentResults Body(
        Swaption swaption,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        SwaptionVolatilityStructure vol,
        CashAnnuityModel model)
    {
        if (!(swaption.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not a European option");
        }

        Date exerciseDate = swaption.Exercise.Date(0);

        // The part of the swap preceding exerciseDate should be truncated to avoid taking into
        // account unwanted cashflows. For the moment we add a check avoiding this situation.
        FixedVsFloatingSwap swap = swaption.Underlying;

        ImmutableArray<CashFlow> fixedLeg = swap.FixedLeg;
        var firstCoupon = (FixedRateCoupon)fixedLeg[0];
        if (!(firstCoupon.AccrualStartDate >= exerciseDate))
        {
            throw new NanoQLException(
                $"swap start ({firstCoupon.AccrualStartDate}) before exercise date ({exerciseDate}) not supported in "
                + "Black swaption engine");
        }

        double strike = swap.FixedRate;

        // using the discounting curve
        // swap.iborIndex() might be using a different forwarding curve
        FixedVsFloatingSwapResults swapResults =
            DiscountingSwapEngine.Calculate(swap, context, discountCurve, includeSettlementDateFlows: false);
        Date valuationDate = swapResults.ValuationDate ?? throw new NanoQLException("valuation date not provided");
        double atmForward = swapResults.FairRate ?? throw new NanoQLException("result not available");

        var additionalResults = ImmutableSortedDictionary.CreateBuilder<string, object>();

        // Volatilities are quoted for zero-spreaded swaps.
        // Therefore, any spread on the floating leg must be removed
        // with a corresponding correction on the fixed leg.
        double spread = swap.Spread;
        if (spread != 0.0)
        {
            double correction = spread * Math.Abs(FloatingLegBPS(swapResults) / FixedLegBPS(swapResults));
            strike -= correction;
            atmForward -= correction;
            additionalResults["spreadCorrection"] = correction;
        }
        else
        {
            additionalResults["spreadCorrection"] = 0.0;
        }

        additionalResults["strike"] = strike;
        additionalResults["atmForward"] = atmForward;

        double annuity;
        if (swaption.SettlementType == SettlementType.Physical
            || (swaption.SettlementType == SettlementType.Cash
                && swaption.SettlementMethod == SettlementMethod.CollateralizedCashPrice))
        {
            annuity = Math.Abs(FixedLegBPS(swapResults)) / BasisPoint;
        }
        else if (swaption.SettlementType == SettlementType.Cash
            && swaption.SettlementMethod == SettlementMethod.ParYieldCurve)
        {
            DayCounter dayCount = firstCoupon.DayCounter;

            // we assume that the cash settlement date is equal
            // to the swap start date
            Date discountDate = model == CashAnnuityModel.DiscountCurve
                ? firstCoupon.AccrualStartDate
                : valuationDate;
            Frequency freq = Frequency.Annual;
            Schedule fixedSchedule = swap.FixedSchedule;
            if (fixedSchedule.HasTenor)
            {
                freq = fixedSchedule.Tenor().Frequency();
            }

            double fixedLegCashBPS = CashFlows.Bps(
                fixedLeg,
                new InterestRate(atmForward, dayCount, Compounding.Compounded, freq),
                context,
                false,
                discountDate);
            annuity = Math.Abs(fixedLegCashBPS / BasisPoint) * discountCurve.Discount(discountDate);
        }
        else
        {
            throw new NanoQLException("invalid (settlementType, settlementMethod) pair");
        }

        additionalResults["annuity"] = annuity;

        Schedule floatingSchedule = swap.FloatingSchedule;
        double swapLength = vol.SwapLength(floatingSchedule.Dates[0], floatingSchedule.Dates[^1]);

        // swapLength is rounded to whole months. To ensure we can read a variance
        // and a shift from vol_ we floor swapLength at 1/12 here therefore.
        swapLength = swapLength < 1.0 / 12.0 ? 1.0 / 12.0 : swapLength;
        additionalResults["swapLength"] = swapLength;

        double variance = vol.BlackVariance(exerciseDate, swapLength, strike);

        double displacement = vol.VolatilityType == VolatilityType.ShiftedLognormal
            ? vol.Shift(exerciseDate, swapLength)
            : 0.0;

        double stdDev = Math.Sqrt(variance);
        additionalResults["stdDev"] = stdDev;
        OptionType w = swap.Type == SwapType.Payer ? OptionType.Call : OptionType.Put;
        double value = TSpec.Value(w, strike, atmForward, stdDev, annuity, displacement);

        double exerciseTime = vol.TimeFromReference(exerciseDate);
        additionalResults["vega"] = TSpec.Vega(strike, atmForward, stdDev, exerciseTime, annuity, displacement);
        additionalResults["delta"] = TSpec.Delta(w, strike, atmForward, stdDev, annuity, displacement);
        additionalResults["timeToExpiry"] = exerciseTime;
        additionalResults["impliedVolatility"] = stdDev / Math.Sqrt(exerciseTime);
        additionalResults["forwardPrice"] = value / discountCurve.Discount(exerciseDate);

        return new InstrumentResults
        {
            NPV = value,
            ValuationDate = valuationDate,
            AdditionalResults = additionalResults.ToImmutable(),
        };
    }

    // The swap's leg BPS as QuantLib's swap reads them: "result not available" for a null one.
    private static double FixedLegBPS(FixedVsFloatingSwapResults results) =>
        results.FixedLegBPS ?? throw new NanoQLException("result not available");

    private static double FloatingLegBPS(FixedVsFloatingSwapResults results) =>
        results.FloatingLegBPS ?? throw new NanoQLException("result not available");
}
