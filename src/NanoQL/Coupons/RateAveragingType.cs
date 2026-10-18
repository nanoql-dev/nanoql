// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/rateaveraging.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>How the rates of sub-periods make a coupon's rate: QuantLib's <c>RateAveraging::Type</c>.</summary>
public enum RateAveragingType
{
    /// <summary>
    /// The simple convention: the interest of each sub-period applies its rate to the principal, and the payment due at
    /// the end of the period is the sum of those amounts.
    /// </summary>
    Simple,

    /// <summary>
    /// The compound convention: the interest of each sub-period applies its rate both to the principal and to the
    /// accumulated unpaid interest.
    /// </summary>
    Compound,
}
