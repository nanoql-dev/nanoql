// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/digitalcmsspreadcoupon.hpp ql/experimental/coupons/digitalcmsspreadcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;

namespace NanoQL.Experimental;

/// <summary>A CMS spread coupon with a digital call, a digital put or both: QuantLib's
/// <c>DigitalCmsSpreadCoupon</c>.</summary>
/// <remarks>
/// A <see cref="DigitalCoupon"/> on a <see cref="CmsSpreadCoupon"/>, replicated through capped and floored coupons on
/// it, each priced by its <see cref="CmsSpreadCouponPricer"/> (the visitor refuses any other pricer). Built by
/// <see cref="DigitalCmsSpreadLeg"/>. Experimental, as in QuantLib.
/// </remarks>
[Experimental("NQL0001")]
public sealed class DigitalCmsSpreadCoupon : DigitalCoupon
{
    /// <summary>Initializes a new instance of the <see cref="DigitalCmsSpreadCoupon"/> class.</summary>
    /// <param name="underlying">The CMS spread coupon whose rate the options are on.</param>
    /// <param name="callStrike">The call's strike, or <see langword="null"/> for no call.</param>
    /// <param name="callPosition">The call's position.</param>
    /// <param name="isCallAtmIncluded">Whether a fixed rate at the call's strike pays the call.</param>
    /// <param name="callDigitalPayoff">The cash-or-nothing call's payoff, or <see langword="null"/> for an
    /// asset-or-nothing call.</param>
    /// <param name="putStrike">The put's strike, or <see langword="null"/> for no put.</param>
    /// <param name="putPosition">The put's position.</param>
    /// <param name="isPutAtmIncluded">Whether a fixed rate at the put's strike pays the put.</param>
    /// <param name="putDigitalPayoff">The cash-or-nothing put's payoff, or <see langword="null"/> for an
    /// asset-or-nothing put.</param>
    /// <param name="replication">The replication; QuantLib's default, central with a gap of 1e-4, when
    /// <see langword="null"/>.</param>
    /// <param name="nakedOption">Whether the coupon pays the options alone, without the underlying's rate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlying"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The replication's gap isn't positive, or a payoff is given without its
    /// strike.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A position given with its strike isn't a member of its
    /// type.</exception>
    public DigitalCmsSpreadCoupon(
        CmsSpreadCoupon underlying,
        double? callStrike = null,
        PositionType callPosition = PositionType.Long,
        bool isCallAtmIncluded = false,
        double? callDigitalPayoff = null,
        double? putStrike = null,
        PositionType putPosition = PositionType.Long,
        bool isPutAtmIncluded = false,
        double? putDigitalPayoff = null,
        DigitalReplication? replication = null,
        bool nakedOption = false)
        : base(
            underlying,
            callStrike,
            callPosition,
            isCallAtmIncluded,
            callDigitalPayoff,
            putStrike,
            putPosition,
            isPutAtmIncluded,
            putDigitalPayoff,
            replication,
            nakedOption)
    {
    }
}
