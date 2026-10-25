// SPDX-FileCopyrightText: Copyright (C) 2017, 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// How <see cref="AndreasenHugeVolatilityInterpl"/> interpolates its calibrated volatilities between the calibration
/// strikes: QuantLib's <c>AndreasenHugeVolatilityInterpl::InterpolationType</c>.
/// </summary>
public enum AndreasenHugeVolatilityInterplInterpolationType
{
    /// <summary>Piecewise constant, each strike's volatility held to the midpoint with the next.</summary>
    PiecewiseConstant = 0,

    /// <summary>Linear.</summary>
    Linear = 1,

    /// <summary>The natural cubic spline.</summary>
    CubicSpline = 2,
}
