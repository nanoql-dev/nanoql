// SPDX-FileCopyrightText: Copyright (C) 2025 Hiroto Ogawa
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/futures/discountingperpetualfuturesengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>How <see cref="DiscountingPerpetualFuturesEngine"/> interpolates the funding rates and interest-rate
/// differences between their times.</summary>
public enum DiscountingPerpetualFuturesEngineInterpolationType
{
    /// <summary>Backward flat: each value holds back to the previous time.</summary>
    PiecewiseConstant,

    /// <summary>Linear.</summary>
    Linear,

    /// <summary>A natural cubic spline.</summary>
    CubicSpline,
}
