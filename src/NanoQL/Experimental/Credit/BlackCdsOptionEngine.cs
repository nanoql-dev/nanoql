// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Stamm
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/blackcdsoptionengine.hpp ql/experimental/credit/blackcdsoptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The Black-formula engine for options on credit default swaps: the underlying's fair spread against its running
/// spread, discounted by its risky annuity, plus the front-end protection of a payer option that doesn't knock out.
/// </summary>
/// <remarks>
/// The engine assumes, as QuantLib's warns, that the exercise date equals the start date of the swap. The swap is
/// priced by its engine, D14's delegate, where QuantLib's reads the swap's attached engine (the S-xcr plan's XD4). The
/// time to the exercise is floored at 0: an exercise between the evaluation date and the discount curve's reference
/// date gives QuantLib a negative time and a NaN standard deviation, here the intrinsic value (XR8 (xv);
/// <c>Divergences/BlackCdsOptionEngineTests.cs</c>); there a payer that doesn't knock out reads its front-end protection
/// with a default probability of 0 and a discount of 1 before each curve's date, as XR8 (x) reads them.
/// </remarks>
[Experimental("NQL0001")]
public static class BlackCdsOptionEngine
{
    /// <summary>Prices an option on a credit default swap.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="probability">The default-probability curve, or <see langword="null"/>, QuantLib's empty handle,
    /// which a knock-out option doesn't read.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="termStructure">The discount curve.</param>
    /// <param name="volatility">The spread's Black volatility.</param>
    /// <param name="swapEngine">The underlying swap's engine.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument but <paramref name="probability"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The swap starts on or before the exercise ("Underlying CDS should start after
    /// option maturity"); the swap's engine gives no fair spread or coupon-leg NPV; a payer option that doesn't knock
    /// out has no default-probability curve ("empty Handle cannot be dereferenced"); or a curve refuses a
    /// date.</exception>
    public static CdsOptionResults Calculate(
        CdsOption option,
        EvaluationContext context,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        YieldTermStructure termStructure,
        double volatility,
        Func<CreditDefaultSwap, EvaluationContext, CreditDefaultSwapResults> swapEngine)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(termStructure);
        ArgumentNullException.ThrowIfNull(swapEngine);
        if (option.IsExpired(context))
        {
            return (CdsOptionResults)option.SetupExpired();
        }

        option.Validate();
        return Body(option, context, probability, recoveryRate, termStructure, volatility, swapEngine);
    }

    // QuantLib's calculate(): the engine's work on a live, validated option, which the implied volatility also calls.
    internal static CdsOptionResults Body(
        CdsOption option,
        EvaluationContext context,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        YieldTermStructure termStructure,
        double volatility,
        Func<CreditDefaultSwap, EvaluationContext, CreditDefaultSwapResults> swapEngine)
    {
        CreditDefaultSwap swap = option.UnderlyingSwap;
        Date maturityDate = swap.Coupons[0].Date;
        Date exerciseDate = option.Exercise.Date(0);
        if (!(maturityDate > exerciseDate))
        {
            throw new NanoQLException("Underlying CDS should start after option maturity");
        }

        Date settlement = termStructure.ReferenceDate;
        CreditDefaultSwapResults swapResults = swapEngine(swap, context);
        double spotFwdSpread = FairSpread(swapResults);
        double swapSpread = swap.RunningSpread;
        DayCounter tSDc = termStructure.DayCounter;

        // The sense of the underlying/option has to be sent this way
        // to the Black formula, no sign.
        double couponLegNPV = swapResults.CouponLegNPV ?? throw new NanoQLException("coupon-leg NPV not available");
        double riskyAnnuity = Math.Abs(couponLegNPV / swapSpread);

        // floored at 0 where the exercise precedes the curve's reference date, QuantLib's NaN
        double t = Math.Max(tSDc.YearFraction(settlement, exerciseDate), 0.0);
        double stdDev = volatility * Math.Sqrt(t);
        OptionType callPut = swap.Side == ProtectionSide.Buyer ? OptionType.Call : OptionType.Put;

        double value = BlackFormulaFunctions.BlackFormula(callPut, swapSpread, spotFwdSpread, stdDev, riskyAnnuity);

        // if a non knock-out payer option, add front end protection value
        if (swap.Side == ProtectionSide.Buyer && !option.KnocksOut)
        {
            DefaultProbabilityTermStructure curve = probability ?? throw new NanoQLException("empty Handle cannot be dereferenced");

            // between the evaluation date and a curve's later date, a default probability of 0 and a discount of 1 (XR8
            // (x); the S-xcr code review's C4 O1-3)
            Date today = context.EvaluationDate;
            double defaultProbability = exerciseDate >= today && exerciseDate < curve.ReferenceDate
                ? 0.0
                : curve.DefaultProbability(exerciseDate);
            double discount = exerciseDate >= today && exerciseDate < termStructure.ReferenceDate
                ? 1.0
                : termStructure.Discount(exerciseDate);
            double frontEndProtection = (int)callPut * swap.Notional
                * (1.0 - recoveryRate)
                * defaultProbability
                * discount;
            value += frontEndProtection;
        }

        return new CdsOptionResults { NPV = value, RiskyAnnuity = riskyAnnuity };
    }

    // QuantLib's CreditDefaultSwap::fairSpread(): the engine's result, refused where it gives none.
    internal static double FairSpread(CreditDefaultSwapResults results) =>
        results.FairSpread ?? throw new NanoQLException("fair spread not available");
}
