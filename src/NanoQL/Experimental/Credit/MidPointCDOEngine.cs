// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/midpointcdoengine.hpp ql/experimental/credit/midpointcdoengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The mid-point engine for synthetic CDOs: each premium period's loss taken at its midpoint, the premium paid on the
/// tranche's notional less the expected loss at the period's end.
/// </summary>
/// <remarks>
/// <para>
/// A static class: <see cref="Calculate"/> takes the engine's inputs and returns the results (D14). It reads the
/// statistics through the CDO's basket, which must be of the engine's evaluation date (the S-xcr plan's XR1 (c)).
/// Premium accrual on default isn't paid, as in QuantLib's.
/// </para>
/// <para>
/// The legs and the upfront are levered by the CDO's leverage factor, and the fair premium and fair upfront are the
/// unlevered rates: QuantLib's levers the premium leg alone, so a tranche at twice its notional reads half its fair
/// premium, 0.02268 for 0.04535; and its additional "fairPremium" is −(protection + upfront)·rate/premium, −0.04535
/// where the instrument's is 0.04535 (XR8 (ix); <c>Divergences/CdoLeverageTests.cs</c>). The fair upfront is the
/// side-invariant rate that zeroes the value, over the first accrual's discount: QuantLib's reads −0.2000 for a buyer
/// where the seller's is 0.2000, and a forward start's value at it −0.22.
/// </para>
/// <para>
/// A basket with realized defaults is priced on the tranche's outstanding notional, its inception notional less the
/// realized tranche loss and the amortized recovery (ORE's): the premium leg pays on it less the live names' expected
/// loss, the upfront is due on it, and a paid first coupon starts the losses from the realized tranche loss (XR8 (ii)).
/// A discount before the discount curve's reference date is 1, and a default probability before a default curve's 0,
/// where QuantLib's reads refuse a seasoned CDO and curves dated T+2 ("negative time"; XR8 (x)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class MidPointCDOEngine
{
    /// <summary>Prices a synthetic CDO.</summary>
    /// <param name="cdo">The CDO.</param>
    /// <param name="context">The evaluation context, of the basket's evaluation date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>The CDO's results.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The basket's evaluation date isn't the context's, the basket has no loss model,
    /// or the model refuses a statistic.</exception>
    public static SyntheticCDOResults Calculate(SyntheticCDO cdo, EvaluationContext context, YieldTermStructure discountCurve)
    {
        ArgumentNullException.ThrowIfNull(cdo);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (cdo.IsExpired(context))
        {
            return (SyntheticCDOResults)cdo.SetupExpired();
        }

        Basket basket = cdo.Basket;
        CheckEvaluationDate(basket, context);
        Date today = context.EvaluationDate;
        double leverage = cdo.LeverageFactor;
        double premiumValue = 0.0;
        double protectionValue = 0.0;
        double upfrontPremiumValue = 0.0;
        var expectedTrancheLoss = ImmutableArray.CreateBuilder<double>(cdo.NormalizedLeg.Length + 1);
        double inceptionTrancheNotional = basket.TrancheNotional;
        double realized = basket.RealizedTrancheLoss;
        double outstanding = basket.RemainingDetachmentAmount() - basket.RemainingAttachmentAmount();

        // compute expected loss at the beginning of first relevant period
        var first = (FixedRateCoupon)cdo.NormalizedLeg[0];
        bool upfrontDue = !first.HasOccurred(context, today);

        // a paid first coupon starts from the losses realized: QuantLib's from 0, the same without them
        double e1 = upfrontDue ? basket.ExpectedTrancheLoss(first.AccrualStartDate) : realized;
        expectedTrancheLoss.Add(e1);
        foreach (CashFlow i in cdo.NormalizedLeg)
        {
            if (i.HasOccurred(context, today))
            {
                expectedTrancheLoss.Add(0.0);
                continue;
            }

            var coupon = (FixedRateCoupon)i;
            Date paymentDate = coupon.Date;
            Date startDate = coupon.AccrualStartDate > discountCurve.ReferenceDate ? coupon.AccrualStartDate : discountCurve.ReferenceDate;
            Date endDate = coupon.AccrualEndDate;

            // we assume the loss within the period took place on this date:
            Date defaultDate = startDate + ((endDate - startDate) / 2);

            double e2 = basket.ExpectedTrancheLoss(endDate);
            expectedTrancheLoss.Add(e2);

            // the premium on the outstanding notional less the live names' expected loss
            premiumValue += (outstanding - (e2 - realized)) / inceptionTrancheNotional
                * coupon.Amount(context)
                * discountCurve.AccruedDiscount(paymentDate);

            // default flows:
            double discount = discountCurve.AccruedDiscount(defaultDate);
            protectionValue += discount * (e2 - e1) * leverage;
            e1 = e2;
        }

        if (upfrontDue)
        {
            upfrontPremiumValue = outstanding * cdo.UpfrontRate * discountCurve.AccruedDiscount(first.AccrualStartDate) * leverage;
        }

        return CdoResults.Assemble(
            cdo, basket, premiumValue, protectionValue, upfrontPremiumValue, outstanding * leverage, 0, expectedTrancheLoss.ToImmutable(),
            upfrontDue ? discountCurve.AccruedDiscount(first.AccrualStartDate) : null);
    }

    /// <summary>Refuses a basket of another evaluation date than the engine's.</summary>
    /// <param name="basket">The basket.</param>
    /// <param name="context">The engine's context.</param>
    /// <exception cref="NanoQLException">The dates differ.</exception>
    internal static void CheckEvaluationDate(Basket basket, EvaluationContext context)
    {
        if (basket.Context.EvaluationDate != context.EvaluationDate)
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"the basket's evaluation date ({basket.Context.EvaluationDate}) isn't the engine's ({context.EvaluationDate})"));
        }
    }
}
