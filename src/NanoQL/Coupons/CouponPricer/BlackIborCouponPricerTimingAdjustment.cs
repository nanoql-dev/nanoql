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
/// The timing adjustment a <see cref="BlackIborCouponPricer"/> applies to the fixing: QuantLib's
/// <c>BlackIborCouponPricer::TimingAdjustment</c>.
/// </summary>
/// <remarks>
/// References: Black76, Hull, <i>Options, Futures and Other Derivatives</i>, 4th ed., page 550; BivariateLognormal,
/// http://ssrn.com/abstract=2170721. QuantLib has no stream operator for it.
/// </remarks>
public enum BlackIborCouponPricerTimingAdjustment
{
    /// <summary>The standard in-arrears adjustment only.</summary>
    Black76 = 0,

    /// <summary>The bivariate lognormal adjustment, which also accounts for the payment date.</summary>
    BivariateLognormal = 1,
}
