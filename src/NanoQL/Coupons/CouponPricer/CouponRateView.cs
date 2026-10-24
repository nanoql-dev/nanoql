// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/couponpricer.hpp ql/cashflows/couponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// A per-call view of a coupon's rates on one pricer: QuantLib's pricer after <c>initialize(coupon)</c>, which a capped
/// or collared coupon's rate queries for its swaplet, caplet and floorlet rates (<c>capflooredcoupon.cpp</c>'s
/// <c>rate()</c>).
/// </summary>
/// <remarks>
/// <see cref="FloatingRateCouponPricer.RateView"/> returns it, and it lives for the call that asks for it, as the
/// user's ruling for S-g1d's memo has it (the S-cms plan's CMR1 (ii)). Its default calls the pricer's public members,
/// so every pricer keeps its bits; a pricer whose per-coupon state costs more than its queries (the CMS pricers) builds
/// that state once for the view, as QuantLib's one <c>initialize</c> serves its three queries.
/// </remarks>
internal abstract class CouponRateView
{
    /// <summary>The swaplet's rate: the coupon's rate.</summary>
    /// <returns>The rate.</returns>
    public abstract double SwapletRate();

    /// <summary>The rate of a caplet on the coupon's fixing.</summary>
    /// <param name="effectiveCap">The cap on the fixing.</param>
    /// <returns>The rate.</returns>
    public abstract double CapletRate(double effectiveCap);

    /// <summary>The rate of a floorlet on the coupon's fixing.</summary>
    /// <param name="effectiveFloor">The floor on the fixing.</param>
    /// <returns>The rate.</returns>
    public abstract double FloorletRate(double effectiveFloor);
}
