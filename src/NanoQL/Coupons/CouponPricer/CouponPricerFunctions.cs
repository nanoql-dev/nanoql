// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/couponpricer.hpp ql/cashflows/couponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the visitor's cases for the CMS, CMS-spread, digital, range-accrual and multiple-resets coupons (the capped coupon's CMS and CMS-spread checks among them), which wait for S-rx and batch 3; the CMS pricer bases CmsCouponPricer and MeanRevertingPricer, which wait for B3.3

using System.Collections.Immutable;

namespace NanoQL.Coupons;

/// <summary>
/// Setting pricers on a leg's coupons: QuantLib's <c>setCouponPricer</c> and <c>setCouponPricers</c>, which return a
/// new leg here, where QuantLib's change the coupons.
/// </summary>
/// <remarks>
/// QuantLib's visitor is a <c>switch</c> over the coupon types, most derived first, as <c>accept</c> dispatches; each
/// later coupon type adds its case in its own port. A leg is set all or nothing: a pricer one coupon refuses leaves no
/// new leg, where QuantLib has already changed the earlier coupons (a documented divergence). Cash flows that aren't
/// floating-rate coupons are kept as they are.
/// </remarks>
public static class CouponPricerFunctions
{
    /// <summary>A copy of the leg with the pricer set on each of its floating-rate coupons.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The new leg; the given one is unchanged.</returns>
    /// <exception cref="ArgumentException">A coupon refuses the pricer, or the leg holds a null cash flow.</exception>
    public static ImmutableArray<CashFlow> SetCouponPricer(ImmutableArray<CashFlow> leg, FloatingRateCouponPricer? pricer)
    {
        CheckLeg(leg);
        if (leg.IsDefaultOrEmpty)
        {
            return leg.IsDefault ? [] : leg;
        }

        ImmutableArray<CashFlow>.Builder result = ImmutableArray.CreateBuilder<CashFlow>(leg.Length);
        foreach (CashFlow cf in leg)
        {
            result.Add(Visit(cf, pricer));
        }

        return result.MoveToImmutable();
    }

    /// <summary>
    /// A copy of the leg with one pricer per cash flow, in order: the last pricer is set on the cash flows past the
    /// list's end.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="pricers">The pricers, no more than the leg's cash flows.</param>
    /// <returns>The new leg; the given one is unchanged.</returns>
    /// <exception cref="ArgumentException">The leg is empty; there are more pricers than cash flows, or none; a
    /// coupon refuses its pricer; or the leg holds a null cash flow.</exception>
    public static ImmutableArray<CashFlow> SetCouponPricers(
        ImmutableArray<CashFlow> leg,
        IEnumerable<FloatingRateCouponPricer?> pricers)
    {
        ArgumentNullException.ThrowIfNull(pricers);
        ImmutableArray<FloatingRateCouponPricer?> p = [.. pricers];
        int nCashFlows = leg.IsDefault ? 0 : leg.Length;
        if (nCashFlows == 0)
        {
            throw new ArgumentException("no cashflows", nameof(leg));
        }

        int nPricers = p.Length;
        if (nCashFlows < nPricers)
        {
            throw new ArgumentException(
                $"mismatch between leg size ({nCashFlows}) and number of pricers ({nPricers})", nameof(pricers));
        }

        if (nPricers == 0)
        {
            // QuantLib reads the pricer before the list's start.
            throw new ArgumentException("no pricers given", nameof(pricers));
        }

        CheckLeg(leg);
        ImmutableArray<CashFlow>.Builder result = ImmutableArray.CreateBuilder<CashFlow>(nCashFlows);
        for (int i = 0; i < nCashFlows; ++i)
        {
            result.Add(Visit(leg[i], i < nPricers ? p[i] : p[nPricers - 1]));
        }

        return result.MoveToImmutable();
    }

    /// <summary>A copy of the leg with the first of two pricers each cash flow accepts set on it.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="p1">The first pricer tried.</param>
    /// <param name="p2">The second pricer tried.</param>
    /// <returns>The new leg; a coupon that accepts neither keeps its pricer.</returns>
    /// <exception cref="ArgumentException">The leg holds a null cash flow.</exception>
    public static ImmutableArray<CashFlow> SetCouponPricers(
        ImmutableArray<CashFlow> leg,
        FloatingRateCouponPricer? p1,
        FloatingRateCouponPricer? p2) =>
        SetCouponPricersFirstMatching(leg, [p1, p2]);

    /// <summary>A copy of the leg with the first of three pricers each cash flow accepts set on it.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="p1">The first pricer tried.</param>
    /// <param name="p2">The second pricer tried.</param>
    /// <param name="p3">The third pricer tried.</param>
    /// <returns>The new leg; a coupon that accepts none keeps its pricer.</returns>
    /// <exception cref="ArgumentException">The leg holds a null cash flow.</exception>
    public static ImmutableArray<CashFlow> SetCouponPricers(
        ImmutableArray<CashFlow> leg,
        FloatingRateCouponPricer? p1,
        FloatingRateCouponPricer? p2,
        FloatingRateCouponPricer? p3) =>
        SetCouponPricersFirstMatching(leg, [p1, p2, p3]);

    /// <summary>A copy of the leg with the first of four pricers each cash flow accepts set on it.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="p1">The first pricer tried.</param>
    /// <param name="p2">The second pricer tried.</param>
    /// <param name="p3">The third pricer tried.</param>
    /// <param name="p4">The fourth pricer tried.</param>
    /// <returns>The new leg; a coupon that accepts none keeps its pricer.</returns>
    /// <exception cref="ArgumentException">The leg holds a null cash flow.</exception>
    public static ImmutableArray<CashFlow> SetCouponPricers(
        ImmutableArray<CashFlow> leg,
        FloatingRateCouponPricer? p1,
        FloatingRateCouponPricer? p2,
        FloatingRateCouponPricer? p3,
        FloatingRateCouponPricer? p4) =>
        SetCouponPricersFirstMatching(leg, [p1, p2, p3, p4]);

    /// <summary>
    /// The cash flow with the pricer set, as QuantLib's <c>PricerSetter</c> visits it: each coupon type's case checks
    /// that the pricer suits it, with QuantLib's text, and sets it; a cash flow that isn't a floating-rate coupon is
    /// returned as it is.
    /// </summary>
    /// <param name="cf">The cash flow.</param>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The cash flow with the pricer set.</returns>
    /// <exception cref="ArgumentException">The coupon refuses the pricer.</exception>
    internal static CashFlow Visit(CashFlow cf, FloatingRateCouponPricer? pricer) => cf switch
    {
        // The coupon types' cases go here, most derived first.
        CappedFlooredIborCoupon c => pricer is IborCouponPricer
            ? c.SetPricer(pricer)
            : throw new ArgumentException("pricer not compatible with Ibor coupon", nameof(pricer)),
        CappedFlooredCoupon c => VisitCappedFlooredCoupon(c, pricer),
        IborCoupon c => pricer is IborCouponPricer
            ? c.SetPricer(pricer)
            : throw new ArgumentException("pricer not compatible with Ibor coupon", nameof(pricer)),

        // The overnight coupons' cases go here, after the Ibor ones.
        FloatingRateCoupon c => c.SetPricer(pricer),
        _ => cf,
    };

    private static CappedFlooredCoupon VisitCappedFlooredCoupon(CappedFlooredCoupon c, FloatingRateCouponPricer? pricer)
    {
        // we might end up here because a CappedFlooredCoupon
        // was directly constructed; we should then check
        // the underlying for consistency with the pricer
        if (c.Underlying is IborCoupon && pricer is not IborCouponPricer)
        {
            throw new ArgumentException("pricer not compatible with Ibor Coupon", nameof(pricer));
        }

        return c.SetPricer(pricer);
    }

    private static ImmutableArray<CashFlow> SetCouponPricersFirstMatching(
        ImmutableArray<CashFlow> leg,
        FloatingRateCouponPricer?[] p)
    {
        // QuantLib skips a null cash flow here, whose dereference its catch-all swallows; it is refused.
        CheckLeg(leg);
        if (leg.IsDefaultOrEmpty)
        {
            return leg.IsDefault ? [] : leg;
        }

        ImmutableArray<CashFlow>.Builder result = ImmutableArray.CreateBuilder<CashFlow>(leg.Length);
        foreach (CashFlow cf in leg)
        {
            CashFlow set = cf;
            foreach (FloatingRateCouponPricer? pricer in p)
            {
                try
                {
                    set = Visit(cf, pricer);
                    break;
                }
                catch (Exception e) when (e is ArgumentException or NanoQLException)
                {
                    // try the next pricer
                }
            }

            result.Add(set);
        }

        return result.MoveToImmutable();
    }

    private static void CheckLeg(ImmutableArray<CashFlow> leg)
    {
        if (leg.IsDefault)
        {
            return;
        }

        foreach (CashFlow cf in leg)
        {
            if (cf is null)
            {
                throw new ArgumentException("null cash flow in leg", nameof(leg));
            }
        }
    }
}
