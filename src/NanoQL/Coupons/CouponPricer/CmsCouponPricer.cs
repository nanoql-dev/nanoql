// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/couponpricer.hpp ql/cashflows/couponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Coupons;

/// <summary>The base class of the pricers for CMS coupons, on a swaption volatility.</summary>
/// <remarks>
/// Immutable: <see cref="WithSwaptionVolatility"/> returns a copy of the pricer's own type, where QuantLib's
/// <c>setSwaptionVolatility</c> replaces the pricer's handle in place. Unsealed, as in QuantLib, where Hagan's pricers
/// and the linear TSR pricer derive from it; a pricer of another assembly derives from it and implements
/// <see cref="FloatingRateCouponPricer"/>'s members.
/// </remarks>
public abstract class CmsCouponPricer : FloatingRateCouponPricer
{
    // Written by the constructor, and by WithSwaptionVolatility on a fresh copy only.
    private SwaptionVolatilityStructure? swaptionVolatility;

    /// <summary>Initializes a new instance of the <see cref="CmsCouponPricer"/> class.</summary>
    /// <param name="swaptionVolatility">The swaption volatility, or <see langword="null"/>, QuantLib's empty handle,
    /// refused when a coupon is priced.</param>
    protected CmsCouponPricer(SwaptionVolatilityStructure? swaptionVolatility = null)
    {
        this.swaptionVolatility = swaptionVolatility;
    }

    /// <summary>Gets the swaption volatility, or <see langword="null"/> for none.</summary>
    public SwaptionVolatilityStructure? SwaptionVolatility => swaptionVolatility;

    /// <summary>A copy of the pricer, of its own type, on another swaption volatility.</summary>
    /// <param name="swaptionVolatility">The swaption volatility, or <see langword="null"/> for none.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public virtual CmsCouponPricer WithSwaptionVolatility(SwaptionVolatilityStructure? swaptionVolatility)
    {
        var copy = (CmsCouponPricer)MemberwiseClone();
        copy.swaptionVolatility = swaptionVolatility;
        return copy;
    }

    // The swaption volatility a price reads, refused when there is none, as QuantLib's handle is dereferenced.
    private protected SwaptionVolatilityStructure Volatility() =>
        swaptionVolatility ?? throw new NanoQLException("empty Handle cannot be dereferenced");
}
