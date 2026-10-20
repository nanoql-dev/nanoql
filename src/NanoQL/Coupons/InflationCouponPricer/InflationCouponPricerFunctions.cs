// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/inflationcouponpricer.hpp ql/cashflows/inflationcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;

namespace NanoQL.Coupons;

/// <summary>
/// QuantLib's free function <c>setCouponPricer</c> for inflation-coupon pricers, on immutable legs, with the swap and
/// bond forms beside it, as <see cref="CouponPricerFunctions"/> has for floating-rate pricers.
/// </summary>
public static class InflationCouponPricerFunctions
{
    /// <summary>
    /// A leg with a pricer set on each of its inflation coupons; every other cash flow is kept as it is.
    /// </summary>
    /// <remarks>
    /// QuantLib's <c>setCouponPricer</c> sets the pricer on the coupons in place, one after another, so a coupon
    /// refusing it leaves the earlier ones holding it, and its cast skips a null cash flow
    /// (<c>inflationcouponpricer.cpp:27-34</c>). Here the leg is immutable: the new leg is all or nothing, and a null
    /// cash flow is refused, as every nanoql leg refuses one, a documented divergence.
    /// </remarks>
    /// <param name="leg">The leg.</param>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The new leg; this one is unchanged. All or nothing: a coupon refusing the pricer leaves no
    /// leg.</returns>
    /// <exception cref="ArgumentException">An inflation coupon refuses the pricer, with QuantLib's "pricer given is
    /// wrong type"; or the leg holds a null cash flow, or is <c>default(ImmutableArray)</c>.</exception>
    public static ImmutableArray<CashFlow> SetCouponPricer(ImmutableArray<CashFlow> leg, InflationCouponPricer? pricer)
    {
        if (leg.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a leg; an empty leg is []", nameof(leg));
        }

        ImmutableArray<CashFlow>.Builder result = ImmutableArray.CreateBuilder<CashFlow>(leg.Length);
        foreach (CashFlow cf in leg)
        {
            result.Add(cf switch
            {
                null => throw new ArgumentException("null cash flow in leg", nameof(leg)),
                InflationCoupon c => c.SetPricer(pricer),
                _ => cf,
            });
        }

        return result.MoveToImmutable();
    }

    /// <summary>A copy of a swap with a pricer set on the inflation coupons of one of its legs.</summary>
    /// <typeparam name="T">The swap's type, which the copy keeps.</typeparam>
    /// <param name="swap">The swap; it is unchanged.</param>
    /// <param name="leg">The leg's index.</param>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="swap"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="leg"/> is negative.</exception>
    /// <exception cref="ArgumentException">The swap has no such leg, or a coupon refuses the pricer.</exception>
    public static T SetCouponPricer<T>(T swap, int leg, InflationCouponPricer? pricer)
        where T : Swap
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentOutOfRangeException.ThrowIfNegative(leg);
        if (leg >= swap.NumberOfLegs)
        {
            throw new ArgumentException($"leg #{leg} doesn't exist!", nameof(leg));
        }

        return (T)swap.WithLeg(leg, SetCouponPricer(swap.Legs[leg], pricer));
    }

    /// <summary>A copy of a bond with a pricer set on its inflation coupons.</summary>
    /// <typeparam name="T">The bond's type, which the copy keeps.</typeparam>
    /// <param name="bond">The bond; it is unchanged.</param>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bond"/> is null.</exception>
    /// <exception cref="ArgumentException">A coupon refuses the pricer.</exception>
    public static T SetCouponPricer<T>(T bond, InflationCouponPricer? pricer)
        where T : Bond
    {
        ArgumentNullException.ThrowIfNull(bond);
        return (T)bond.WithCashflows(SetCouponPricer(bond.Cashflows, pricer));
    }
}
