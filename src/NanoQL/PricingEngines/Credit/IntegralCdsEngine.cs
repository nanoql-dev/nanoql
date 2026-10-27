// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/credit/integralcdsengine.hpp ql/pricingengines/credit/integralcdsengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The integral engine for credit default swaps: each coupon period's default leg integrated over steps of a fixed
/// period, from the later of the period's start and the evaluation date.
/// </summary>
/// <remarks>
/// <para>
/// A static class: <see cref="Calculate"/> takes the engine's inputs and returns the results, in place of QuantLib's
/// engine object (D14). An expired swap's results are <see cref="CreditDefaultSwap.SetupExpired"/>'s, as QuantLib's
/// instrument gives them before it calls the engine; otherwise the swap is validated and priced.
/// </para>
/// <para>
/// A curve accrues nothing before its reference date: the discount at a step's end before the discount curve's
/// reference date is 1, and the survival at a payment date and the default probability at a step's ends before the
/// probability curve's reference date are 1 and 0, where QuantLib's reads refuse them ("negative time"): on a curve that
/// settles after the evaluation date, as for QuantLib's own CDS on a T+2 curve, and on curves dated the evaluation date
/// for a live coupon whose period ended before it (one paid after the weekend that ended it). No default before the
/// reference date is QuantLib's own premise for its two-date default probability (<c>defaulttermstructure.cpp:112-120</c>).
/// A documented divergence (the S-core plan's CR7 (xxxiv)); wherever QuantLib's reads succeed, its values, bit for bit.
/// </para>
/// <para>
/// A negative step is refused, where QuantLib's loop walks back from the period's start until a curve refuses its date
/// (the plan's CR7 (xxv)).
/// </para>
/// </remarks>
public static class IntegralCdsEngine
{
    /// <summary>Prices a credit default swap.</summary>
    /// <param name="cds">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="integrationStep">The integration step.</param>
    /// <param name="probability">The default-probability curve, or <see langword="null"/>, QuantLib's empty
    /// handle.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <param name="includeSettlementDateFlows">Whether flows on the discount curve's reference date count; the
    /// context's <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.</param>
    /// <returns>The swap's results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cds"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The step is negative, on a swap not expired.</exception>
    /// <exception cref="NanoQLException">The step has no length ("null period set"); a curve is missing ("no discount
    /// term structure set", "no probability term structure set"); the swap fails its validation; or a curve refuses a
    /// date.</exception>
    public static CreditDefaultSwapResults Calculate(
        CreditDefaultSwap cds,
        EvaluationContext context,
        Period integrationStep,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        bool? includeSettlementDateFlows = null)
    {
        ArgumentNullException.ThrowIfNull(cds);
        ArgumentNullException.ThrowIfNull(context);
        if (cds.IsExpired(context))
        {
            return (CreditDefaultSwapResults)cds.SetupExpired();
        }

        cds.Validate();
        return Body(cds, context, integrationStep, probability, recoveryRate, discountCurve, includeSettlementDateFlows);
    }

    // QuantLib's calculate(): the engine's work on a live, validated swap.
    private static CreditDefaultSwapResults Body(
        CreditDefaultSwap cds,
        EvaluationContext context,
        Period integrationStep,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        bool? includeSettlementDateFlows)
    {
        if (!(integrationStep != default(Period)))
        {
            throw new NanoQLException("null period set");
        }

        if (!(integrationStep.Length > 0))
        {
            throw new ArgumentException(
                $"negative integration step ({integrationStep}) not allowed", nameof(integrationStep));
        }

        if (discountCurve is null)
        {
            throw new NanoQLException("no discount term structure set");
        }

        if (probability is null)
        {
            throw new NanoQLException("no probability term structure set");
        }

        Date today = context.EvaluationDate;
        Date settlementDate = discountCurve.ReferenceDate;

        // Upfront Flow NPV. Either we are on-the-run (no flow)
        // or we are forward start
        double upfPVO1 = 0.0;
        double upfrontNPV = 0.0;
        if (!cds.UpfrontPayment.HasOccurred(context, settlementDate, includeSettlementDateFlows))
        {
            // date determining the probability survival so we have to pay
            // the upfront (did not knock out)
            upfPVO1 = discountCurve.Discount(cds.UpfrontPayment.Date);
            upfrontNPV = upfPVO1 * cds.UpfrontPayment.Amount(context);
        }

        // Accrual rebate.
        double accrualRebateNPV = 0.0;
        if (cds.AccrualRebate is SimpleCashFlow rebate
            && !rebate.HasOccurred(context, settlementDate, includeSettlementDateFlows))
        {
            accrualRebateNPV = discountCurve.Discount(rebate.Date) * rebate.Amount(context);
        }

        double couponLegNPV = 0.0;
        double defaultLegNPV = 0.0;
        for (int i = 0; i < cds.Coupons.Length; ++i)
        {
            if (cds.Coupons[i].HasOccurred(context, settlementDate, includeSettlementDateFlows))
            {
                continue;
            }

            var coupon = (FixedRateCoupon)cds.Coupons[i];

            // In order to avoid a few switches, we calculate the NPV
            // of both legs as a positive quantity. We'll give them
            // the right sign at the end.
            Date paymentDate = coupon.Date,
                startDate = i == 0 ? cds.ProtectionStartDate : coupon.AccrualStartDate,
                endDate = coupon.AccrualEndDate;
            Date effectiveStartDate = (startDate <= today && today <= endDate) ? today : startDate;
            double couponAmount = coupon.Amount(context);

            double s = probability.AccruedSurvivalProbability(paymentDate);

            // On one side, we add the fixed rate payments in case of
            // survival.
            couponLegNPV += s * couponAmount * discountCurve.Discount(paymentDate);

            // On the other side, we add the payment (and possibly the
            // accrual) in case of default.
            Period step = integrationStep;
            Date d0 = effectiveStartDate;
            Date d1 = Min(d0 + step, endDate);
            double p0 = probability.AccruedDefaultProbability(d0);
            double endDiscount = discountCurve.Discount(paymentDate);
            do
            {
                double b = cds.PaysAtDefaultTime ? discountCurve.AccruedDiscount(d1) : endDiscount;

                double p1 = probability.AccruedDefaultProbability(d1);
                double dP = p1 - p0;

                // accrual...
                if (cds.SettlesAccrual)
                {
                    if (cds.PaysAtDefaultTime)
                    {
                        couponLegNPV += coupon.AccruedAmount(d1, context) * b * dP;
                    }
                    else
                    {
                        couponLegNPV += couponAmount * b * dP;
                    }
                }

                // ...and claim.
                double claim = cds.Claim.Amount(d1, cds.Notional, recoveryRate, context);
                defaultLegNPV += claim * b * dP;

                // setup for next time around the loop
                p0 = p1;
                d0 = d1;
                d1 = Min(d0 + step, endDate);
            }
            while (d0 < endDate);
        }

        double upfrontSign = 1.0;
        switch (cds.Side)
        {
            case ProtectionSide.Seller:
                defaultLegNPV *= -1.0;
                accrualRebateNPV *= -1.0;
                break;
            case ProtectionSide.Buyer:
                couponLegNPV *= -1.0;
                upfrontNPV *= -1.0;
                upfrontSign = -1.0;
                break;
            default:
                throw new NanoQLException("unknown protection side");
        }

        const double BasisPoint = 1.0e-4;
        return new CreditDefaultSwapResults
        {
            NPV = defaultLegNPV + couponLegNPV + upfrontNPV + accrualRebateNPV,
            ErrorEstimate = null,
            CouponLegNPV = couponLegNPV,
            DefaultLegNPV = defaultLegNPV,
            UpfrontNPV = upfrontNPV,
            AccrualRebateNPV = accrualRebateNPV,
            FairSpread = couponLegNPV != 0.0
                ? -defaultLegNPV * cds.RunningSpread / (couponLegNPV + accrualRebateNPV)
                : null,
            FairUpfront = upfPVO1 > 0.0
                ? -upfrontSign * (defaultLegNPV + couponLegNPV + accrualRebateNPV) / (upfPVO1 * cds.Notional)
                : null,
            CouponLegBPS = cds.RunningSpread != 0.0 ? couponLegNPV * BasisPoint / cds.RunningSpread : null,
            UpfrontBPS = cds.Upfront is double upfront && upfront != 0.0 ? upfrontNPV * BasisPoint / upfront : null,
        };
    }

    // std::min of two dates.
    private static Date Min(Date a, Date b) => b < a ? b : a;
}
