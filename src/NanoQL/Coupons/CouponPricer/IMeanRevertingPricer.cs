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
/// A (CMS) coupon pricer with a mean reversion, which can be calibrated to CMS market quotes: QuantLib's
/// <c>MeanRevertingPricer</c> mixin, an interface here as nanoql's mixins are.
/// </summary>
/// <remarks>
/// Hagan's pricers and the linear TSR pricer implement it. QuantLib's reversion is a quote its
/// <c>setMeanReversion</c> replaces in place; here it is a value, and <see cref="WithMeanReversion"/> returns a copy of
/// the pricer, its own type through the implementations' members of the same name.
/// </remarks>
public interface IMeanRevertingPricer
{
    /// <summary>Gets the mean reversion.</summary>
    double MeanReversion { get; }

    /// <summary>A copy of the pricer with another mean reversion.</summary>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    IMeanRevertingPricer WithMeanReversion(double meanReversion);
}
