// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/equitycashflow.hpp ql/cashflows/equitycashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Coupons;

/// <summary>QuantLib's free function <c>setCouponPricer</c> for equity cash flow pricers, on immutable legs.</summary>
public static class EquityCashFlowFunctions
{
    /// <summary>A leg with a pricer set on each of its equity cash flows; every other cash flow is kept as it
    /// is.</summary>
    /// <remarks>
    /// QuantLib's sets the pricer on the flows in place and its cast skips a null cash flow
    /// (<c>equitycashflow.cpp:44-51</c>); here the new leg is built, and a null cash flow is refused, as every nanoql
    /// leg refuses one.
    /// </remarks>
    /// <param name="leg">The leg.</param>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The new leg; this one is unchanged.</returns>
    /// <exception cref="ArgumentException">The leg holds a null cash flow, or is
    /// <c>default(ImmutableArray)</c>.</exception>
    public static ImmutableArray<CashFlow> SetCouponPricer(ImmutableArray<CashFlow> leg, EquityCashFlowPricer? pricer)
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
                EquityCashFlow c => c.SetPricer(pricer),
                _ => cf,
            });
        }

        return result.MoveToImmutable();
    }
}
