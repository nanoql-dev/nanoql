// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/fxresetcashflows.hpp ql/experimental/fx/fxresetcashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>QuantLib's free function <c>setFxResetPricer</c>, with a swap form.</summary>
/// <remarks>
/// QuantLib sets the pricer on the cash flows a leg holds; nanoql's cash flows are immutable, so these return a new
/// leg, or a copy of the swap holding it, as <see cref="Coupons.CouponPricerFunctions"/> does for coupon pricers. Cash
/// flows that aren't FX-reset coupons or exchanges are kept as they are.
/// </remarks>
[Experimental("NQL0001")]
public static class FxResetCashFlowsFunctions
{
    /// <summary>A copy of the leg with the FX-reset pricer set on each of its FX-reset cash flows.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The new leg; the given one is unchanged.</returns>
    /// <exception cref="ArgumentException">The leg is <c>default(ImmutableArray)</c> or holds a null cash
    /// flow.</exception>
    public static ImmutableArray<CashFlow> SetFxResetPricer(ImmutableArray<CashFlow> leg, FxResetPricer? pricer)
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
                FxResetCoupon coupon => coupon.SetFxResetPricer(pricer),
                FxResetNotionalExchange exchange => exchange.SetFxResetPricer(pricer),
                null => throw new ArgumentException("null cash flow in leg", nameof(leg)),
                _ => cf,
            });
        }

        return result.MoveToImmutable();
    }

    /// <summary>
    /// A copy of a built swap, of its own type, with the FX-reset pricer set on the FX-reset cash flows of one of its
    /// legs: QuantLib's <c>setFxResetPricer(swap->leg(j), pricer)</c>, which changes the cash flows the swap holds.
    /// </summary>
    /// <typeparam name="T">The swap's type.</typeparam>
    /// <param name="swap">The swap.</param>
    /// <param name="leg">The leg's index.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy, with the swap's other legs and terms; the given swap is unchanged.</returns>
    /// <exception cref="ArgumentNullException">The swap is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The leg's index is negative.</exception>
    /// <exception cref="ArgumentException">The swap has no leg at the index.</exception>
    public static T SetFxResetPricer<T>(T swap, int leg, FxResetPricer? pricer)
        where T : Swap
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentOutOfRangeException.ThrowIfNegative(leg);
        if (leg >= swap.NumberOfLegs)
        {
            throw new ArgumentException($"leg #{leg} doesn't exist!", nameof(leg));
        }

        return (T)swap.WithLeg(leg, SetFxResetPricer(swap.Legs[leg], pricer));
    }
}
