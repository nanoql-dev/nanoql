// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/credit/isdacdsengine.hpp ql/pricingengines/credit/isdacdsengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The ISDA engine for credit default swaps: the ISDA CDS Standard Model, the J.P. Morgan CDX model ISDA adopted in
/// 2009, which integrates the protection and the default accrual exactly between the curves' nodes.
/// </summary>
/// <remarks>
/// <para>
/// References: [1] The Pricing and Risk Management of Credit Default Swaps, with a Focus on the ISDA Model, OpenGamma
/// Quantitative Research, version as of 15 October 2013; [2] ISDA CDS Standard Model Proposed Numerical Fix, Markit,
/// 15 November 2012; [3] Markit Interest Rate Curve XML Specifications, version 1.16, 15 October 2013. According to [1]
/// the options matching the standard model's C code are <see cref="IsdaCdsEngineAccrualBias.HalfDayBias"/> and
/// <see cref="IsdaCdsEngineForwardsInCouponPeriod.Flat"/> before its version 1.8.2, and
/// <see cref="IsdaCdsEngineAccrualBias.NoBias"/> and <see cref="IsdaCdsEngineForwardsInCouponPeriod.Flat"/> from it;
/// the theoretically correct setting is <see cref="IsdaCdsEngineAccrualBias.NoBias"/> and
/// <see cref="IsdaCdsEngineForwardsInCouponPeriod.Piecewise"/>.
/// </para>
/// <para>
/// The curves must be ISDA compliant: Actual/365 (Fixed), dated on the evaluation date, a discount curve log-linear in
/// its discounts or flat in its forwards, and a credit curve log-linear in its survival probabilities or flat in its
/// hazard rates. QuantLib casts the curves to its interpolated curve types; here a curve qualifies by the
/// <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/> it implements, which the interpolated and the piecewise
/// curves of those kinds both do, or by being a <see cref="FlatForward"/> or a <see cref="FlatHazardRate"/>. For
/// precise consistency with the specification, Ibor coupons should be at par and the rate helpers' Ibor index should
/// not provide the evaluation date's fixing; neither is checked.
/// </para>
/// <para>
/// The protection leg runs to the maturity: the maturity joins the curves' nodes when their last one is before it.
/// QuantLib adds the maturity only when the curves have no nodes, so on curves that end before the maturity it drops
/// the protection from their last node to the maturity, a documented divergence. A static class: an expired swap's
/// results are <see cref="CreditDefaultSwap.SetupExpired"/>'s.
/// </para>
/// </remarks>
public static class IsdaCdsEngine
{
    /// <summary>Prices a credit default swap.</summary>
    /// <param name="cds">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="probability">The default-probability curve, or <see langword="null"/>, QuantLib's empty
    /// handle.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <param name="includeSettlementDateFlows">Whether flows on the effective protection start and on the evaluation
    /// date count; the context's <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when
    /// <see langword="null"/>.</param>
    /// <param name="numericalFix">How zero denominators are avoided.</param>
    /// <param name="accrualBias">Whether the default accrual keeps the half-day term.</param>
    /// <param name="forwardsInCouponPeriod">Whether the default accrual takes the nodes inside a period.</param>
    /// <returns>The swap's results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cds"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option isn't a member of its type, with QuantLib's
    /// text.</exception>
    /// <exception cref="ArgumentException">A curve isn't ISDA compliant; the swap doesn't settle its accrual, pays at
    /// the period's end or has another claim than a face-value one; or a coupon's day counter isn't Actual/365 (Fixed)
    /// or Actual/360.</exception>
    /// <exception cref="NanoQLException">A curve is missing ("no discount term structure set", "no probability term
    /// structure set"), or the swap fails its validation.</exception>
    public static CreditDefaultSwapResults Calculate(
        CreditDefaultSwap cds,
        EvaluationContext context,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        bool? includeSettlementDateFlows = null,
        IsdaCdsEngineNumericalFix numericalFix = IsdaCdsEngineNumericalFix.Taylor,
        IsdaCdsEngineAccrualBias accrualBias = IsdaCdsEngineAccrualBias.HalfDayBias,
        IsdaCdsEngineForwardsInCouponPeriod forwardsInCouponPeriod = IsdaCdsEngineForwardsInCouponPeriod.Piecewise)
    {
        ArgumentNullException.ThrowIfNull(cds);
        ArgumentNullException.ThrowIfNull(context);
        if (cds.IsExpired(context))
        {
            return (CreditDefaultSwapResults)cds.SetupExpired();
        }

        cds.Validate();
        return Body(
            cds,
            context,
            probability,
            recoveryRate,
            discountCurve,
            includeSettlementDateFlows,
            numericalFix,
            accrualBias,
            forwardsInCouponPeriod);
    }

    // QuantLib's calculate(): the engine's work on a live, validated swap, which the implied quantities also call.
    internal static CreditDefaultSwapResults Body(
        CreditDefaultSwap cds,
        EvaluationContext context,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        bool? includeSettlementDateFlows,
        IsdaCdsEngineNumericalFix numericalFix,
        IsdaCdsEngineAccrualBias accrualBias,
        IsdaCdsEngineForwardsInCouponPeriod forwardsInCouponPeriod)
    {
        if (!(numericalFix is IsdaCdsEngineNumericalFix.None or IsdaCdsEngineNumericalFix.Taylor))
        {
            throw new ArgumentOutOfRangeException(
                nameof(numericalFix), numericalFix, "numerical fix must be None or Taylor");
        }

        if (!(accrualBias is IsdaCdsEngineAccrualBias.HalfDayBias or IsdaCdsEngineAccrualBias.NoBias))
        {
            throw new ArgumentOutOfRangeException(
                nameof(accrualBias), accrualBias, "accrual bias must be HalfDayBias or NoBias");
        }

        if (!(forwardsInCouponPeriod is IsdaCdsEngineForwardsInCouponPeriod.Flat
              or IsdaCdsEngineForwardsInCouponPeriod.Piecewise))
        {
            throw new ArgumentOutOfRangeException(
                nameof(forwardsInCouponPeriod), forwardsInCouponPeriod, "forwards in coupon period must be Flat or Piecewise");
        }

        // it would be possible to handle the cases which are excluded below,
        // but the ISDA engine is not explicitly specified to handle them,
        // so we just forbid them too
        DayCounter dc = new Actual365Fixed();
        DayCounter dc1 = new Actual360();
        DayCounter dc2 = new Actual360(true);

        Date evalDate = context.EvaluationDate;

        // check if given curves are ISDA compatible
        // (the interpolation is checked below)
        if (discountCurve is null)
        {
            throw new NanoQLException("no discount term structure set");
        }

        if (probability is null)
        {
            throw new NanoQLException("no probability term structure set");
        }

        if (!(discountCurve.DayCounter == dc))
        {
            throw new ArgumentException(
                $"yield term structure day counter ({discountCurve.DayCounter.Name}) should be Act/365(Fixed)",
                nameof(discountCurve));
        }

        if (!(probability.DayCounter == dc))
        {
            throw new ArgumentException(
                $"probability term structure day counter ({probability.DayCounter.Name}) should be Act/365(Fixed)",
                nameof(probability));
        }

        if (!(discountCurve.ReferenceDate == evalDate))
        {
            throw new ArgumentException(
                $"yield term structure reference date ({discountCurve.ReferenceDate} should be evaluation date "
                + $"({evalDate})",
                nameof(discountCurve));
        }

        if (!(probability.ReferenceDate == evalDate))
        {
            throw new ArgumentException(
                $"probability term structure reference date ({probability.ReferenceDate} should be evaluation date "
                + $"({evalDate})",
                nameof(probability));
        }

        if (!cds.SettlesAccrual)
        {
            throw new ArgumentException("ISDA engine not compatible with non accrual paying CDS", nameof(cds));
        }

        if (!cds.PaysAtDefaultTime)
        {
            throw new ArgumentException("ISDA engine not compatible with end period payment", nameof(cds));
        }

        if (cds.Claim is not FaceValueClaim)
        {
            throw new ArgumentException("ISDA engine not compatible with non face value claim", nameof(cds));
        }

        Date maturity = cds.Maturity;
        Date effectiveProtectionStart = cds.ProtectionStartDate > evalDate + 1 ? cds.ProtectionStartDate : evalDate + 1;

        // collect nodes from both curves and sort them
        List<Date> nodes = Union(DiscountNodes(discountCurve), CreditNodes(probability));

        // the protection runs to the maturity: QuantLib adds it only to empty nodes, so on curves ending before it, it
        // drops the protection from their last node to the maturity
        if (nodes.Count == 0 || nodes[^1] < maturity)
        {
            nodes.Add(maturity);
        }

        double nFix = numericalFix == IsdaCdsEngineNumericalFix.None ? 1E-50 : 0.0;

        // protection leg pricing (npv is always negative at this stage)
        double protectionNpv = 0.0;

        Date d0 = effectiveProtectionStart - 1;
        double p0 = discountCurve.Discount(d0);
        double q0 = probability.SurvivalProbability(d0);
        Date d1;
        for (int k = UpperBound(nodes, effectiveProtectionStart); k < nodes.Count; ++k)
        {
            if (nodes[k] > maturity)
            {
                d1 = maturity;
                k = nodes.Count - 1; // early exit
            }
            else
            {
                d1 = nodes[k];
            }

            double p1 = discountCurve.Discount(d1);
            double q1 = probability.SurvivalProbability(d1);

            double fhat = Math.Log(p0) - Math.Log(p1);
            double hhat = Math.Log(q0) - Math.Log(q1);
            double fhphh = fhat + hhat;

            if (fhphh < 1E-4 && numericalFix == IsdaCdsEngineNumericalFix.Taylor)
            {
                double fhphhq = fhphh * fhphh;
                protectionNpv +=
                    p0 * q0 * hhat * (1.0 - (0.5 * fhphh) + (1.0 / 6.0 * fhphhq) - (1.0 / 24.0 * fhphhq * fhphh)
                                      + (1.0 / 120 * fhphhq * fhphhq));
            }
            else
            {
                protectionNpv += hhat / (fhphh + nFix) * ((p0 * q0) - (p1 * q1));
            }

            d0 = d1;
            p0 = p1;
            q0 = q1;
        }

        protectionNpv *= cds.Claim.Amount(null, cds.Notional, recoveryRate, context);

        double defaultLegNPV = protectionNpv;

        // premium leg pricing (npv is always positive at this stage)
        double premiumNpv = 0.0, defaultAccrualNpv = 0.0;
        foreach (CashFlow cf in cds.Coupons)
        {
            var coupon = (FixedRateCoupon)cf;

            if (!(coupon.DayCounter == dc || coupon.DayCounter == dc1 || coupon.DayCounter == dc2))
            {
                throw new ArgumentException(
                    $"ISDA engine requires a coupon day counter Act/365Fixed or Act/360 ({coupon.DayCounter.Name})",
                    nameof(cds));
            }

            // premium coupons
            if (!cf.HasOccurred(context, effectiveProtectionStart, includeSettlementDateFlows))
            {
                premiumNpv += coupon.Amount(context) * discountCurve.Discount(coupon.Date)
                              * probability.SurvivalProbability(coupon.Date - 1);
            }

            // default accruals
            if (!new SimpleEvent(coupon.AccrualEndDate).HasOccurred(context, effectiveProtectionStart, false))
            {
                Date start = (coupon.AccrualStartDate > effectiveProtectionStart
                    ? coupon.AccrualStartDate
                    : effectiveProtectionStart) - 1;
                Date end = coupon.Date - 1;
                double tstart = discountCurve.TimeFromReference(coupon.AccrualStartDate - 1)
                                - (accrualBias == IsdaCdsEngineAccrualBias.HalfDayBias ? 1.0 / 730.0 : 0.0);
                var localNodes = new List<Date> { start };

                // add intermediary nodes, if any
                if (forwardsInCouponPeriod == IsdaCdsEngineForwardsInCouponPeriod.Piecewise)
                {
                    int it0 = UpperBound(nodes, start);
                    int it1 = LowerBound(nodes, end);
                    for (int k = it0; k < it1; ++k)
                    {
                        localNodes.Add(nodes[k]);
                    }
                }

                localNodes.Add(end);

                double defaultAccrThisNode = 0.0;
                double t0 = discountCurve.TimeFromReference(localNodes[0]);
                double pn0 = discountCurve.Discount(localNodes[0]);
                double qn0 = probability.SurvivalProbability(localNodes[0]);

                for (int node = 1; node < localNodes.Count; ++node)
                {
                    double t1 = discountCurve.TimeFromReference(localNodes[node]);
                    double pn1 = discountCurve.Discount(localNodes[node]);
                    double qn1 = probability.SurvivalProbability(localNodes[node]);
                    double fhat = Math.Log(pn0) - Math.Log(pn1);
                    double hhat = Math.Log(qn0) - Math.Log(qn1);
                    double fhphh = fhat + hhat;
                    if (fhphh < 1E-4 && numericalFix == IsdaCdsEngineNumericalFix.Taylor)
                    {
                        // see above, terms up to (f+h)^3 seem more than enough,
                        // what exactly is implemented in the standard isda C
                        // code ?
                        double fhphhq = fhphh * fhphh;
                        defaultAccrThisNode +=
                            hhat * pn0 * qn0
                            * (((t0 - tstart)
                                * (1.0 - (0.5 * fhphh) + (1.0 / 6.0 * fhphhq) - (1.0 / 24.0 * fhphhq * fhphh)))
                               + ((t1 - t0)
                                  * (0.5 - (1.0 / 3.0 * fhphh) + (1.0 / 8.0 * fhphhq) - (1.0 / 30.0 * fhphhq * fhphh))));
                    }
                    else
                    {
                        defaultAccrThisNode +=
                            hhat / (fhphh + nFix)
                            * (((t1 - t0) * ((((pn0 * qn0) - (pn1 * qn1)) / (fhphh + nFix)) - (pn1 * qn1)))
                               + ((t0 - tstart) * ((pn0 * qn0) - (pn1 * qn1))));
                    }

                    t0 = t1;
                    pn0 = pn1;
                    qn0 = qn1;
                }

                defaultAccrualNpv += defaultAccrThisNode * cds.Notional * coupon.InterestRate.Rate * 365.0 / 360.0;
            }
        }

        double couponLegNPV = premiumNpv + defaultAccrualNpv;

        // upfront flow npv
        double upfPVO1 = 0.0;
        double upfrontNPV = 0.0;
        if (!cds.UpfrontPayment.HasOccurred(context, evalDate, includeSettlementDateFlows))
        {
            upfPVO1 = discountCurve.Discount(cds.UpfrontPayment.Date);
            double upfrontAmount = cds.UpfrontPayment.Amount(context);
            if (upfrontAmount != 0.0)
            {
                upfrontNPV = upfPVO1 * upfrontAmount;
            }
        }

        double accrualRebateNPV = 0.0;
        if (cds.AccrualRebate is SimpleCashFlow rebate && rebate.Amount(context) != 0.0
            && !rebate.HasOccurred(context, evalDate, includeSettlementDateFlows))
        {
            accrualRebateNPV = discountCurve.Discount(rebate.Date) * rebate.Amount(context);
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
        double upfrontSensitivity = upfPVO1 * cds.Notional;
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
            FairUpfront = upfrontSensitivity != 0.0
                ? -upfrontSign * (defaultLegNPV + couponLegNPV + accrualRebateNPV) / upfrontSensitivity
                : null,
            CouponLegBPS = cds.RunningSpread != 0.0 ? couponLegNPV * BasisPoint / cds.RunningSpread : null,
            UpfrontBPS = cds.Upfront is double upfront && upfront != 0.0 ? upfrontNPV * BasisPoint / upfront : null,
        };
    }

    // QuantLib's casts of the discount curve to its ISDA-compliant interpolated curves, as the curves' interfaces.
    private static IReadOnlyList<Date> DiscountNodes(YieldTermStructure discountCurve) => discountCurve switch
    {
        IInterpolatedCurve<Discount, LogLinear> c => c.Dates(),
        IInterpolatedCurve<ForwardRate, BackwardFlat> c => c.Dates(),
        IInterpolatedCurve<ForwardRate, ForwardFlat> c => c.Dates(),

        // no dates to extract
        FlatForward => [],
        _ => throw new ArgumentException("Yield curve must be flat forward interpolated", nameof(discountCurve)),
    };

    // QuantLib's casts of the credit curve to its ISDA-compliant interpolated curves, as the curves' interfaces.
    private static IReadOnlyList<Date> CreditNodes(DefaultProbabilityTermStructure probability) => probability switch
    {
        IInterpolatedCurve<SurvivalProbability, LogLinear> c => c.Dates(),
        IInterpolatedCurve<HazardRate, BackwardFlat> c => c.Dates(),

        // no dates to extract
        FlatHazardRate => [],
        _ => throw new ArgumentException("Credit curve must be flat forward interpolated", nameof(probability)),
    };

    // std::set_union of two sorted ranges: each date as many times as the range holding it more often holds it.
    private static List<Date> Union(IReadOnlyList<Date> a, IReadOnlyList<Date> b)
    {
        var result = new List<Date>(a.Count + b.Count);
        int i = 0, j = 0;
        while (i < a.Count && j < b.Count)
        {
            if (a[i] < b[j])
            {
                result.Add(a[i++]);
            }
            else if (b[j] < a[i])
            {
                result.Add(b[j++]);
            }
            else
            {
                result.Add(a[i++]);
                j++;
            }
        }

        while (i < a.Count)
        {
            result.Add(a[i++]);
        }

        while (j < b.Count)
        {
            result.Add(b[j++]);
        }

        return result;
    }

    // std::upper_bound: the first index whose date is after d.
    private static int UpperBound(List<Date> nodes, Date d)
    {
        int k = 0;
        while (k < nodes.Count && !(d < nodes[k]))
        {
            k++;
        }

        return k;
    }

    // std::lower_bound: the first index whose date isn't before d.
    private static int LowerBound(List<Date> nodes, Date d)
    {
        int k = 0;
        while (k < nodes.Count && nodes[k] < d)
        {
            k++;
        }

        return k;
    }
}
