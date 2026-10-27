// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/flatvol.hpp ql/models/marketmodels/models/flatvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Models;

/// <summary>The flat-volatility model's covariance function.</summary>
public static class FlatVolFunctions
{
    /// <summary>
    /// The covariance over [t1, t2] of two rates with constant volatilities, each until its own fixing time: the time
    /// both are alive within the interval times the volatilities.
    /// </summary>
    /// <param name="t1">The interval's start.</param>
    /// <param name="t2">The interval's end, not before its start.</param>
    /// <param name="T">The first rate's fixing time.</param>
    /// <param name="S">The second rate's fixing time.</param>
    /// <param name="v1">The first rate's volatility.</param>
    /// <param name="v2">The second rate's volatility.</param>
    /// <returns>The covariance.</returns>
    /// <exception cref="ArgumentException">The interval's bounds are in reverse order.</exception>
    public static double FlatVolCovariance(double t1, double t2, double T, double S, double v1, double v2)
    {
        if (!(t1 <= t2))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"integrations bounds ({t1},{t2}) are in reverse order"), nameof(t2));
        }

        // std::min(S, T), std::min(t2, cutOff)
        double cutOff = T < S ? T : S;
        if (t1 >= cutOff)
        {
            return 0.0;
        }

        cutOff = cutOff < t2 ? cutOff : t2;
        return (cutOff - t1) * v1 * v2;
    }
}
