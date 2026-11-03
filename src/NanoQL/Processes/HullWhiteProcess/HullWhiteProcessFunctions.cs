// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/hullwhiteprocess.hpp ql/processes/hullwhiteprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Processes;

/// <summary>
/// The Hull-White processes' kernel, in the cancellation-free form of QuantLib's master's Vasicek (#2882), in which
/// their members are written.
/// </summary>
internal static class HullWhiteProcessFunctions
{
    /// <summary>(1 − e^(−x))/x, accurate for small x as well, and 1 at 0: QuantLib's master's <c>oneMinusExpOverX</c>.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>(1 − e^(−x))/x.</returns>
    internal static double OneMinusExpOverX(double x) => x == 0.0 ? 1.0 : -Expm1Functions.StdExpm1(-x) / x;
}
