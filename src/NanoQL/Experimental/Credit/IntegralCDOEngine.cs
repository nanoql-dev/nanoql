// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/integralcdoengine.hpp ql/experimental/credit/integralcdoengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The integral engine for synthetic CDOs: each premium period integrated in steps of a given size from the later of
/// its start and the evaluation date, the premium paid on the tranche's notional less the expected loss at each step.
/// </summary>
/// <remarks>
/// <para>
/// A static class: <see cref="Calculate"/> takes the engine's inputs and returns the results (D14). It reads the
/// statistics through the CDO's basket, which must be of the engine's evaluation date (the S-xcr plan's XR1 (c)).
/// Premium accrual on default isn't paid, as in QuantLib's.
/// </para>
/// <para>
/// The legs and the upfront are levered by the CDO's leverage factor, which QuantLib's engine ignores, so a tranche at
/// twice its notional reads an unchanged value; the fair premium and fair upfront are the unlevered rates, the
/// additional "fairPremium" the instrument's, the fair upfront side-invariant (XR8 (ix);
/// <c>Divergences/CdoLeverageTests.cs</c>). A basket with realized defaults is priced on the tranche's outstanding
/// notional, and a discount before the curve's reference date is 1, as <see cref="MidPointCDOEngine"/>'s (XR8 (ii),
/// (x)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class IntegralCDOEngine
{
    /// <summary>Prices a synthetic CDO.</summary>
    /// <param name="cdo">The CDO.</param>
    /// <param name="context">The evaluation context, of the basket's evaluation date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="stepSize">The integration step, QuantLib's 3 months by default (<see langword="null"/>).</param>
    /// <returns>The CDO's results.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The basket's evaluation date isn't the context's, the basket has no loss model,
    /// or the model refuses a statistic.</exception>
    public static SyntheticCDOResults Calculate(
        SyntheticCDO cdo, EvaluationContext context, YieldTermStructure discountCurve, Period? stepSize = null)
    {
        ArgumentNullException.ThrowIfNull(cdo);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (cdo.IsExpired(context))
        {
            return (SyntheticCDOResults)cdo.SetupExpired();
        }

        Period step = stepSize ?? new Period(3, TimeUnit.Months);
        Basket basket = cdo.Basket;
        MidPointCDOEngine.CheckEvaluationDate(basket, context);
        Date today = context.EvaluationDate;
        double leverage = cdo.LeverageFactor;
        double premiumValue = 0.0;
        double protectionValue = 0.0;
        double upfrontPremiumValue = 0.0;
        int error = 0;
        var expectedTrancheLoss = ImmutableArray.CreateBuilder<double>(cdo.NormalizedLeg.Length + 1);
        double realized = basket.RealizedTrancheLoss;
        double outstanding = basket.RemainingDetachmentAmount() - basket.RemainingAttachmentAmount();
        var calendar = new NullCalendar();

        // compute expected loss at the beginning of first relevant period
        var first = (FixedRateCoupon)cdo.NormalizedLeg[0];
        bool upfrontDue = !first.HasOccurred(context, today);
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
            Date d1 = coupon.AccrualStartDate;
            Date d2 = coupon.Date;

            Date d, d0 = d1;
            double e2;
            do
            {
                d = calendar.Advance(d0 > today ? d0 : today, step);
                if (d > d2)
                {
                    d = d2;
                }

                e2 = basket.ExpectedTrancheLoss(d);

                // the premium on the outstanding notional less the live names' expected loss
                premiumValue += (outstanding - (e2 - realized))
                    * cdo.RunningRate
                    * cdo.DayCounter.YearFraction(d0, d)
                    * discountCurve.AccruedDiscount(d)
                    * leverage;

                if (e2 < e1)
                {
                    error++;
                }

                protectionValue += (e2 - e1) * discountCurve.AccruedDiscount(d) * leverage;

                d0 = d;
                e1 = e2;
            }
            while (d < d2);

            expectedTrancheLoss.Add(e2);
        }

        if (upfrontDue)
        {
            upfrontPremiumValue = outstanding * cdo.UpfrontRate * discountCurve.AccruedDiscount(first.AccrualStartDate) * leverage;
        }

        return CdoResults.Assemble(
            cdo, basket, premiumValue, protectionValue, upfrontPremiumValue, outstanding * leverage, error, expectedTrancheLoss.ToImmutable(),
            upfrontDue ? discountCurve.AccruedDiscount(first.AccrualStartDate) : null);
    }
}
