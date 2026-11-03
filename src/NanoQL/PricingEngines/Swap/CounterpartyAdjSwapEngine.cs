// SPDX-FileCopyrightText: Copyright (C) 2015 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swap/cvaswapengine.hpp ql/pricingengines/swap/cvaswapengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The bilateral counterparty-risk adjusted swap engine: the swap's NPV less the counterparty's expected loss on its
/// exposure, plus the investor's, each exposure a sequence of swaptions at the swap's fixed rate, one per fixed period,
/// weighted by the party's default probability over the period.
/// </summary>
/// <remarks>
/// <para>
/// Collateral is not considered, nor wrong-way risk: rates and default are uncorrelated (Sorensen and Bollier, "Pricing
/// swap default risk", Financial Analysts Journal, 1994; Brigo and Masetti, 2004 and 2005). The fair rate is QuantLib's
/// approximation, not an iteration.
/// </para>
/// <para>
/// QuantLib's three constructors are the overloads of <see cref="Calculate(FixedVsFloatingSwap, EvaluationContext,
/// YieldTermStructure, Func{Swaption, EvaluationContext, InstrumentResults}, DefaultProbabilityTermStructure, double,
/// DefaultProbabilityTermStructure?, double)"/>, its volatility quote and constant volatility one overload; with no
/// investor curve, QuantLib's default-free one, a flat hazard rate of 1e-12 on the counterparty curve's day counter, is
/// built per call under the context. QuantLib's loop over the fixed payment dates skips those before the counterparty
/// curve's reference date with no end check (<c>cvaswapengine.cpp:128</c>), reading past the dates when every one is
/// before it; here it stops at the end, and the swap is then worth its NPV.
/// </para>
/// <para>
/// QuantLib strikes every swaptionlet and its reverse at the swap's risk-free fair rate (<c>cvaswapengine.cpp:147</c>,
/// <c>:170</c>, <c>:179</c>), so its adjustment is the same whatever the swap's fixed rate: a swap away from par takes
/// the at-the-money swap's CVA and DVA. Here the NPV's are struck at the swap's fixed rate, the exposure of the
/// contract itself, which is QuantLib's within a few ulps where the fixed rate is the fair rate. The fair rate keeps
/// QuantLib's: its first-order root of the adjusted NPV, taken on swaptionlets struck at the risk-free fair rate, the
/// market's, so the same whatever the fixed rate; on the contract's strikes it would move with the fixed rate, away
/// from the root off par.
/// </para>
/// </remarks>
public static class CounterpartyAdjSwapEngine
{
    /// <summary>Prices a swap with its exposure priced by a swaption engine.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="swaptionEngine">The swaption engine of the exposure model, which determines its volatility.</param>
    /// <param name="ctptyDTS">The counterparty's default curve, whose reference date is the price date.</param>
    /// <param name="ctptyRecoveryRate">The counterparty's recovery rate.</param>
    /// <param name="invstDTS">The investor's default curve, or <see langword="null"/> for a default-free one.</param>
    /// <param name="invstRecoveryRate">The investor's recovery rate.</param>
    /// <returns>The swap's results: its NPV and fair rate, adjusted; the expired results when the swap has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the investor's curve is null, a curve or the engine with
    /// QuantLib's text.</exception>
    /// <exception cref="NanoQLException">The swap's nominals aren't constant, or its legs aren't a fixed-rate and a
    /// floating-rate Ibor leg, each with QuantLib's text.</exception>
    public static FixedVsFloatingSwapResults Calculate(
        FixedVsFloatingSwap swap,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        Func<Swaption, EvaluationContext, InstrumentResults> swaptionEngine,
        DefaultProbabilityTermStructure ctptyDTS,
        double ctptyRecoveryRate,
        DefaultProbabilityTermStructure? invstDTS = null,
        double invstRecoveryRate = 0.999)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        if (discountCurve is null)
        {
            throw new ArgumentNullException(nameof(discountCurve), "no discount term structure set");
        }

        if (ctptyDTS is null)
        {
            throw new ArgumentNullException(nameof(ctptyDTS), "no ctpty default term structure set");
        }

        if (swaptionEngine is null)
        {
            throw new ArgumentNullException(nameof(swaptionEngine), "no swap option engine set");
        }

        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        DefaultProbabilityTermStructure investor = invstDTS
            ?? FlatHazardRate.Create(0, new NullCalendar(), 1.0e-12, ctptyDTS.DayCounter, context);
        double nominal;
        try
        {
            nominal = swap.Nominal();
        }
        catch (NanoQLException)
        {
            throw new NanoQLException("non-constant nominals are not supported yet");
        }

        Date priceDate = ctptyDTS.ReferenceDate;
        ImmutableArray<Date> fixedPayDates = [.. swap.FixedLeg.Select(cf => cf.Date)];
        double cumOptVal = 0.0, cumPutVal = 0.0;
        double fairOptVal = 0.0, fairPutVal = 0.0;
        int nextFD = 0;
        Date swapletStart = priceDate;
        while (nextFD < fixedPayDates.Length && fixedPayDates[nextFD] < priceDate)
        {
            ++nextFD;
        }

        // Compute fair spread for strike value:
        // copy args into the non risky engine
        SwapResults vSResults = DiscountingSwapEngine.Calculate((Swap)swap, context, discountCurve);
        var coupon = swap.FixedLeg[0] as FixedRateCoupon
            ?? throw new NanoQLException("dynamic cast of fixed leg coupon failed.");
        double baseSwapRate = coupon.Rate(context);
        double legNpv0 = vSResults.LegNPV[0] ?? throw new NanoQLException("result not available");
        double legNpv1 = vSResults.LegNPV[1] ?? throw new NanoQLException("result not available");
        double baseSwapFairRate = -baseSwapRate * legNpv1 / legNpv0;
        double baseSwapNPV = vSResults.NPV ?? throw new NanoQLException("result not available");
        SwapType reversedType = swap.Type == SwapType.Payer ? SwapType.Receiver : SwapType.Payer;

        // Swaplet options summatory: struck at the swap's own rate for the NPV, and at QuantLib's strike, the fair
        // rate, for the fair rate
        while (nextFD < fixedPayDates.Length)
        {
            // iFD coupon not fixed, create swaptionlet:
            var floatCoupon = swap.FloatingLeg[0] as FloatingRateCoupon
                ?? throw new NanoQLException("dynamic cast of floating leg coupon failed.");
            var swapIndex = floatCoupon.Index as IborIndex
                ?? throw new NanoQLException("dynamic cast of floating leg index failed.");

            double ctptyProbability = ctptyDTS.DefaultProbability(swapletStart, fixedPayDates[nextFD]);
            double invstProbability = investor.DefaultProbability(swapletStart, fixedPayDates[nextFD]);
            (double call, double put) = Swaptionlets(
                swaptionEngine, context, swapIndex, swap.Type, reversedType, nominal, swapletStart, fixedPayDates[^1],
                baseSwapRate);
            (double fairCall, double fairPut) = baseSwapFairRate == baseSwapRate
                ? (call, put)
                : Swaptionlets(
                    swaptionEngine, context, swapIndex, swap.Type, reversedType, nominal, swapletStart,
                    fixedPayDates[^1], baseSwapFairRate);

            // atm underlying swap means that the value of put = value
            // call so this double pricing is not needed
            cumOptVal += call * ctptyProbability;
            cumPutVal += put * invstProbability;
            fairOptVal += fairCall * ctptyProbability;
            fairPutVal += fairPut * invstProbability;

            swapletStart = fixedPayDates[nextFD];
            ++nextFD;
        }

        double value = baseSwapNPV - ((1.0 - ctptyRecoveryRate) * cumOptVal) + ((1.0 - invstRecoveryRate) * cumPutVal);
        double fairRate = -baseSwapRate
            * (legNpv1 - ((1.0 - ctptyRecoveryRate) * fairOptVal) + ((1.0 - invstRecoveryRate) * fairPutVal))
            / legNpv0;
        return swap.FetchResults(new FixedVsFloatingSwapResults { NPV = value, FairRate = fairRate });
    }

    // QuantLib's swaptionlet and its reverse on the swap from a start to the last fixed payment, struck at a rate, each
    // exercised at the start: their NPVs.
    private static (double Call, double Put) Swaptionlets(
        Func<Swaption, EvaluationContext, InstrumentResults> swaptionEngine,
        EvaluationContext context,
        IborIndex swapIndex,
        SwapType type,
        SwapType reversedType,
        double nominal,
        Date swapletStart,
        Date end,
        double strike)
    {
        // Alternatively one could cap this period to, say, 1M
        var baseSwapsTenor = new Period(end.SerialNumber - swapletStart.SerialNumber, TimeUnit.Days);
        VanillaSwap swaplet = new MakeVanillaSwap(baseSwapsTenor, swapIndex)
            .WithFixedRate(strike)
            .WithType(type)
            .WithNominal(nominal)
            .WithEffectiveDate(swapletStart)
            .WithTerminationDate(end)
            .ToVanillaSwap(context);
        VanillaSwap revSwaplet = new MakeVanillaSwap(baseSwapsTenor, swapIndex)
            .WithFixedRate(strike)
            .WithType(reversedType)
            .WithNominal(nominal)
            .WithEffectiveDate(swapletStart)
            .WithTerminationDate(end)
            .ToVanillaSwap(context);
        var swaptionlet = new Swaption(swaplet, new EuropeanExercise(swapletStart));
        var putSwaplet = new Swaption(revSwaplet, new EuropeanExercise(swapletStart));
        return (Npv(swaptionEngine(swaptionlet, context)), Npv(swaptionEngine(putSwaplet, context)));
    }

    /// <summary>Prices a swap with its exposure priced by <see cref="BlackSwaptionEngine"/> on a constant
    /// volatility, QuantLib's constructors on a volatility and on a volatility quote.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="blackVol">The Black volatility of the exposure model.</param>
    /// <param name="ctptyDTS">The counterparty's default curve, whose reference date is the price date.</param>
    /// <param name="ctptyRecoveryRate">The counterparty's recovery rate.</param>
    /// <param name="invstDTS">The investor's default curve, or <see langword="null"/> for a default-free one.</param>
    /// <param name="invstRecoveryRate">The investor's recovery rate.</param>
    /// <returns>The swap's results: its NPV and fair rate, adjusted.</returns>
    /// <exception cref="ArgumentNullException">An argument but the investor's curve is null.</exception>
    /// <exception cref="NanoQLException">The swap's nominals aren't constant, or its legs aren't a fixed-rate and a
    /// floating-rate Ibor leg.</exception>
    public static FixedVsFloatingSwapResults Calculate(
        FixedVsFloatingSwap swap,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        double blackVol,
        DefaultProbabilityTermStructure ctptyDTS,
        double ctptyRecoveryRate,
        DefaultProbabilityTermStructure? invstDTS = null,
        double invstRecoveryRate = 0.999) =>
        Calculate(
            swap,
            context,
            discountCurve,
            (s, c) => BlackSwaptionEngine.Calculate(s, c, discountCurve, blackVol),
            ctptyDTS,
            ctptyRecoveryRate,
            invstDTS,
            invstRecoveryRate);

    private static double Npv(InstrumentResults results) =>
        results?.NPV ?? throw new NanoQLException("result not available");
}
