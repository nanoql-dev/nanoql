// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/stubiborcoupon.hpp ql/cashflows/stubiborcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// The convention selecting the index an irregular Ibor coupon fixes on: QuantLib's
/// <c>StubIndexSelection::Convention</c>. QuantLib has no stream operator for it.
/// </summary>
public enum StubIndexSelectionConvention
{
    /// <summary>The candidate whose maturity is closest to the coupon's end date; ties select the earlier maturity.</summary>
    ClosestIndex = 0,

    /// <summary>
    /// The two candidates whose maturities bracket the coupon's end date, interpolated linearly in calendar days; a
    /// candidate maturing on the end date alone.
    /// </summary>
    Interpolated = 1,
}
