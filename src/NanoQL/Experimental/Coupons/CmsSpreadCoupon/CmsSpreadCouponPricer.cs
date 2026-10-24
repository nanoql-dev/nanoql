// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/cmsspreadcoupon.hpp ql/experimental/coupons/cmsspreadcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;

namespace NanoQL.Experimental;

/// <summary>The base of the CMS spread coupon pricers, which read the correlation of the two swap rates.</summary>
/// <remarks>
/// QuantLib's <c>setCorrelation</c> changes the pricer in place; <see cref="WithCorrelation"/> returns a copy of the
/// pricer's own type, the receiver unchanged. QuantLib's correlation quote is a <c>double</c>, and its empty handle
/// <see langword="null"/>. Experimental, as in QuantLib.
/// </remarks>
[Experimental("NQL0001")]
public abstract class CmsSpreadCouponPricer : FloatingRateCouponPricer
{
    private double? correlation;

    /// <summary>Initializes a new instance of the <see cref="CmsSpreadCouponPricer"/> class.</summary>
    /// <param name="correlation">The correlation of the two swap rates, or <see langword="null"/>, QuantLib's empty
    /// handle.</param>
    protected CmsSpreadCouponPricer(double? correlation = null)
    {
        this.correlation = correlation;
    }

    /// <summary>Gets the correlation of the two swap rates, or <see langword="null"/> for none.</summary>
    public double? Correlation => correlation;

    /// <summary>A copy of the pricer, of its own type, with another correlation.</summary>
    /// <param name="correlation">The correlation, or <see langword="null"/> for none.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public virtual CmsSpreadCouponPricer WithCorrelation(double? correlation = null)
    {
        var copy = (CmsSpreadCouponPricer)MemberwiseClone();
        copy.correlation = correlation;
        return copy;
    }
}
