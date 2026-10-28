// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/integralntdengine.hpp ql/experimental/credit/integralntdengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The integral engine for n-th-to-default swaps: each premium period's default leg integrated in steps of a given
/// size, the contract triggered by the basket's n-th default.
/// </summary>
/// <remarks>
/// <para>
/// A static class: <see cref="Calculate"/> takes the engine's inputs and returns the results, in place of QuantLib's
/// engine object (D14). It reads the statistics through the swap's basket, which must be of the engine's evaluation
/// date (the S-xcr plan's XR1 (c)). QuantLib's engine takes the basket as homogeneous, hard-coded: the recovery is read
/// from the basket's first name for all, and its branch for unequal recoveries is never taken, so it isn't ported (XR8
/// (xl)). Here the first live name's: once the first name has defaulted, QuantLib's on the restated basket, where a
/// read of the first name would price every future default at its settled recovery (XR8 (ii)). The fair premium leaves the upfront out, QuantLib's CDS convention (XR8 (xxxix)).
/// </para>
/// <para>
/// The upfront is the swap's nominal times its rate. QuantLib's is the basket's remaining notional times the rate
/// (<c>integralntdengine.cpp:157-163</c>), 5 for a 1,000 nominal on a 100 basket (XR8 (xi);
/// <c>Divergences/NthToDefaultUpfrontTests.cs</c>); where the nominal is the basket's notional, QuantLib's. A discount
/// before the discount curve's reference date is 1, and a default probability before a default curve's 0, where
/// QuantLib's reads refuse them ("negative time"; XR8 (x)). A realized default lowers the order the basket's
/// probabilities read; once the realized defaults reach it, the contract is triggered (XR8 (ii)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class IntegralNtdEngine
{
    /// <summary>Prices an n-th-to-default swap.</summary>
    /// <param name="ntd">The swap.</param>
    /// <param name="context">The evaluation context, of the basket's evaluation date.</param>
    /// <param name="integrationStep">The default leg's integration step, shortened to a day at each period's end.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>The swap's results.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The basket's evaluation date isn't the context's, the basket has no loss model,
    /// or the model refuses a statistic.</exception>
    public static NthToDefaultResults Calculate(
        NthToDefault ntd, EvaluationContext context, Period integrationStep, YieldTermStructure discountCurve)
    {
        ArgumentNullException.ThrowIfNull(ntd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (ntd.IsExpired(context))
        {
            return (NthToDefaultResults)ntd.SetupExpired();
        }

        Basket basket = ntd.Basket;
        if (basket.Context.EvaluationDate != context.EvaluationDate)
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"the basket's evaluation date ({basket.Context.EvaluationDate}) isn't the engine's ({context.EvaluationDate})"));
        }

        Date today = context.EvaluationDate;

        // the homogeneous recovery, the first live name's: QuantLib's first name where none has defaulted
        // (a basket of no live name binds no model, refused at its first statistic)
        int firstLive = basket.LiveList().IsEmpty ? 0 : basket.LiveList()[0];
        double premiumValue = 0.0;
        double upfrontPremiumValue = 0.0;
        double accrualValue = 0.0;
        double claimValue = 0.0;
        Date discountReference = discountCurve.ReferenceDate;
        Period oneDay = new(1, TimeUnit.Days);

        foreach (CashFlow i in ntd.PremiumLeg)
        {
            var coupon = (FixedRateCoupon)i;
            Date d = i.Date;
            if (d > discountReference)
            {
                // prob of contract not having been triggered by date of payment
                double probNonTriggered = 1.0 - basket.ProbAtLeastNEvents(ntd.Rank, d);
                premiumValue += i.Amount(context) * discountCurve.Discount(d) * probNonTriggered;

                d = coupon.AccrualStartDate >= discountReference ? coupon.AccrualStartDate : discountReference;

                // do steps of specified size
                Date d0 = d;
                Period stepSize = integrationStep;
                double defProb0 = basket.ProbAtLeastNEvents(ntd.Rank, d0);
                do
                {
                    double disc = discountCurve.Discount(d);

                    // take test out of the while loop
                    double defProb1 = basket.ProbAtLeastNEvents(ntd.Rank, d);
                    claimValue -= (defProb1 - defProb0)
                        * basket.Claim.Amount(d, ntd.Nominal, basket.RecoveryRate(d, firstLive), context)
                        * disc;

                    double dcfdd = defProb1 - defProb0;
                    defProb0 = defProb1;

                    if (ntd.SettlePremiumAccrual)
                    {
                        accrualValue += coupon.AccruedAmount(d, context) * disc * dcfdd;
                    }

                    d0 = d;
                    d = d0 + stepSize;

                    // reduce step size ?
                    if (stepSize != oneDay && d > coupon.AccrualEndDate)
                    {
                        stepSize = oneDay;
                        d = d0 + stepSize;
                    }
                }
                while (d <= coupon.AccrualEndDate);
            }
        }

        // The upfront might be due before the curve ref date...
        CashFlow first = ntd.PremiumLeg[0];
        if (!first.HasOccurred(context, today))
        {
            upfrontPremiumValue = ntd.Nominal * ntd.UpfrontRate
                * discountCurve.AccruedDiscount(((FixedRateCoupon)first).AccrualStartDate);
        }

        if (ntd.Side == ProtectionSide.Buyer)
        {
            premiumValue *= -1;
            accrualValue *= -1;
            claimValue *= -1;
            upfrontPremiumValue *= -1;
        }

        double value = premiumValue + accrualValue + claimValue + upfrontPremiumValue;
        double fairPremium = -ntd.Premium * claimValue / (premiumValue + accrualValue);
        return new NthToDefaultResults
        {
            NPV = value,
            ErrorEstimate = null,
            PremiumValue = premiumValue,
            UpfrontPremiumValue = upfrontPremiumValue,
            FairPremium = fairPremium,

            // alternatively use results buffers and omit locals.
            ProtectionValue = claimValue,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                .Add("fairPremium", fairPremium)
                .Add("premiumLegNPV", premiumValue + upfrontPremiumValue)
                .Add("protectionLegNPV", claimValue),
        };
    }
}
