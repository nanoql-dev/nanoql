// SPDX-FileCopyrightText: Copyright (C) 2008 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/credit/midpointcdsengine.hpp ql/pricingengines/credit/midpointcdsengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The mid-point engine for credit default swaps: each coupon period's default falls at its middle, from the later of
/// its start and the evaluation date.
/// </summary>
/// <remarks>
/// A static class: <see cref="Calculate"/> takes the engine's inputs and returns the results, in place of QuantLib's
/// engine object (D14). An expired swap's results are <see cref="CreditDefaultSwap.SetupExpired"/>'s, as QuantLib's
/// instrument gives them before it calls the engine; otherwise the swap is validated and priced.
/// </remarks>
public static class MidPointCdsEngine
{
    /// <summary>Prices a credit default swap.</summary>
    /// <param name="cds">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="probability">The default-probability curve, or <see langword="null"/>, QuantLib's empty
    /// handle.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <param name="includeSettlementDateFlows">Whether flows on the discount curve's reference date count; the
    /// context's <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.</param>
    /// <returns>The swap's results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cds"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="NanoQLException">A curve is missing ("no discount term structure set", "no probability term
    /// structure set"); the swap fails its validation; or a curve refuses a date.</exception>
    public static CreditDefaultSwapResults Calculate(
        CreditDefaultSwap cds,
        EvaluationContext context,
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
        return Body(cds, context, probability, recoveryRate, discountCurve, includeSettlementDateFlows);
    }

    // QuantLib's calculate(): the engine's work on a live, validated swap, which the implied quantities also call.
    internal static CreditDefaultSwapResults Body(
        CreditDefaultSwap cds,
        EvaluationContext context,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        bool? includeSettlementDateFlows)
    {
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

        // Upfront amount.
        double upfPVO1 = 0.0;
        double upfrontNPV = 0.0;
        if (!cds.UpfrontPayment.HasOccurred(context, settlementDate, includeSettlementDateFlows))
        {
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
                startDate = coupon.AccrualStartDate,
                endDate = coupon.AccrualEndDate;

            // this is the only point where it might not coincide
            if (i == 0)
            {
                startDate = cds.ProtectionStartDate;
            }

            Date effectiveStartDate = (startDate <= today && today <= endDate) ? today : startDate;

            // mid-point
            Date defaultDate = effectiveStartDate + ((endDate - effectiveStartDate) / 2);

            double s = probability.SurvivalProbability(paymentDate);
            double p = probability.DefaultProbability(effectiveStartDate, endDate);

            // on one side, we add the fixed rate payments in case of
            // survival...
            couponLegNPV += s * coupon.Amount(context) * discountCurve.Discount(paymentDate);

            // ...possibly including accrual in case of default.
            if (cds.SettlesAccrual)
            {
                if (cds.PaysAtDefaultTime)
                {
                    couponLegNPV += p * coupon.AccruedAmount(defaultDate, context) * discountCurve.Discount(defaultDate);
                }
                else
                {
                    // pays at the end
                    couponLegNPV += p * coupon.Amount(context) * discountCurve.Discount(paymentDate);
                }
            }

            // on the other side, we add the payment in case of default.
            double claim = cds.Claim.Amount(defaultDate, cds.Notional, recoveryRate, context);
            if (cds.PaysAtDefaultTime)
            {
                defaultLegNPV += p * claim * discountCurve.Discount(defaultDate);
            }
            else
            {
                defaultLegNPV += p * claim * discountCurve.Discount(paymentDate);
            }
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
}
