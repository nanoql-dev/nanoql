// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/mathconstants.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>
/// QuantLib's mathematical constants (<c>mathconstants.hpp</c>), keeping the macro names, as
/// <see cref="NumericLimits"/> keeps <c>qldefines.hpp</c>'s. QuantLib defines each where the C runtime's
/// <c>math.h</c> hasn't; MSVC's values (<c>_USE_MATH_DEFINES</c>) round to the same doubles.
/// </summary>
internal static class MathConstants
{
    /// <summary>e.</summary>
    internal const double M_E = 2.71828182845904523536;

    /// <summary>log₂ e.</summary>
    internal const double M_LOG2E = 1.44269504088896340736;

    /// <summary>log₁₀ e.</summary>
    internal const double M_LOG10E = 0.434294481903251827651;

    /// <summary>1/ln 10.</summary>
    internal const double M_IVLN10 = 0.434294481903251827651;

    /// <summary>ln 2.</summary>
    internal const double M_LN2 = 0.693147180559945309417;

    /// <summary>ln 2, under QuantLib's second name.</summary>
    internal const double M_LOG2_E = 0.693147180559945309417;

    /// <summary>ln 10.</summary>
    internal const double M_LN10 = 2.30258509299404568402;

    /// <summary>π.</summary>
    internal const double M_PI = 3.141592653589793238462643383280;

    /// <summary>2π.</summary>
    internal const double M_TWOPI = M_PI * 2.0;

    /// <summary>π/2.</summary>
    internal const double M_PI_2 = 1.57079632679489661923;

    /// <summary>π/4.</summary>
    internal const double M_PI_4 = 0.785398163397448309616;

    /// <summary>3π/4.</summary>
    internal const double M_3PI_4 = 2.3561944901923448370E0;

    /// <summary>√π.</summary>
    internal const double M_SQRTPI = 1.77245385090551602792981;

    /// <summary>1/π.</summary>
    internal const double M_1_PI = 0.318309886183790671538;

    /// <summary>2/π.</summary>
    internal const double M_2_PI = 0.636619772367581343076;

    /// <summary>1/√π.</summary>
    internal const double M_1_SQRTPI = 0.564189583547756286948;

    /// <summary>2/√π.</summary>
    internal const double M_2_SQRTPI = 1.12837916709551257390;

    /// <summary>√2.</summary>
    internal const double M_SQRT2 = 1.41421356237309504880;

    /// <summary>1/√2.</summary>
    internal const double M_SQRT_2 = 0.7071067811865475244008443621048490392848359376887;

    /// <summary>1/√2, under QuantLib's second name.</summary>
    internal const double M_SQRT1_2 = 0.7071067811865475244008443621048490392848359376887;

    /// <summary>The low part of ln 2.</summary>
    internal const double M_LN2LO = 1.9082149292705877000E-10;

    /// <summary>The high part of ln 2.</summary>
    internal const double M_LN2HI = 6.9314718036912381649E-1;

    /// <summary>√3.</summary>
    internal const double M_SQRT3 = 1.73205080756887719000;

    /// <summary>1/ln 2.</summary>
    internal const double M_INVLN2 = 1.4426950408889633870E0;
}
