// SPDX-FileCopyrightText: Copyright (C) 2019 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2022 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvolsurfacedelta.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// How <see cref="BlackVolatilitySurfaceDelta"/> interpolates its smile sections in strike: QuantLib's
/// <c>BlackVolatilitySurfaceDelta::SmileInterpolationMethod</c>.
/// </summary>
public enum BlackVolatilitySurfaceDeltaSmileInterpolationMethod
{
    /// <summary>Linear interpolation.</summary>
    Linear = 0,

    /// <summary>Kruger's cubic, natural at both ends.</summary>
    NaturalCubic = 1,

    /// <summary>Kruger's monotonic cubic, natural on the left and flat on the right.</summary>
    FinancialCubic = 2,

    /// <summary>The natural cubic spline.</summary>
    CubicSpline = 3,
}
