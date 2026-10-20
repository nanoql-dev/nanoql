// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/credit/isdacdsengine.hpp ql/pricingengines/credit/isdacdsengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// Whether the ISDA engine's default accrual takes the curves' nodes inside a coupon period: QuantLib's
/// <c>IsdaCdsEngine::ForwardsInCouponPeriod</c>, with its values.
/// </summary>
public enum IsdaCdsEngineForwardsInCouponPeriod
{
    /// <summary>Flat forwards over the period, the OpenGamma paper's formula (52) with its second, error, term: the
    /// standard model's C code.</summary>
    Flat = 0,

    /// <summary>Piecewise forwards between the curves' nodes, without the second term: the theoretically correct
    /// setting.</summary>
    Piecewise = 1,
}
