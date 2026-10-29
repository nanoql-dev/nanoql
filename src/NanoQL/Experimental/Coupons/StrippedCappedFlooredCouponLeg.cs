// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/strippedcapflooredcoupon.hpp ql/experimental/coupons/strippedcapflooredcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;

namespace NanoQL.Experimental;

/// <summary>
/// A leg whose capped or floored coupons are replaced by their stripped options: QuantLib's
/// <c>StrippedCappedFlooredCouponLeg</c>, whose conversion to a leg is <see cref="ToLeg"/>.
/// </summary>
[Experimental("NQL0001")]
public sealed class StrippedCappedFlooredCouponLeg
{
    private readonly ImmutableArray<CashFlow> underlyingLeg;

    /// <summary>Initializes a new instance of the <see cref="StrippedCappedFlooredCouponLeg"/> class.</summary>
    /// <param name="underlyingLeg">The leg.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlyingLeg"/> is null.</exception>
    public StrippedCappedFlooredCouponLeg(IEnumerable<CashFlow> underlyingLeg)
    {
        ArgumentNullException.ThrowIfNull(underlyingLeg);
        this.underlyingLeg = [.. underlyingLeg];
    }

    /// <summary>The leg, each capped or floored coupon stripped and every other cash flow as it is.</summary>
    /// <returns>The leg.</returns>
    public ImmutableArray<CashFlow> ToLeg() =>
        [.. underlyingLeg.Select(i => i is CappedFlooredCoupon c ? (CashFlow)new StrippedCappedFlooredCoupon(c) : i)];
}
