// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/abcd.hpp ql/termstructures/volatility/abcd.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>QuantLib's free function of <c>abcd.hpp</c>.</summary>
public static class AbcdFunctions
{
    /// <summary>The Black volatility to time u of the u-fixing rate under abcd instantaneous volatility: its average
    /// volatility over [0, u].</summary>
    /// <param name="u">The time.</param>
    /// <param name="a">a.</param>
    /// <param name="b">b.</param>
    /// <param name="c">c, positive.</param>
    /// <param name="d">d, non-negative.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException">The coefficients fail <see cref="Numerics.AbcdMathFunction.Validate"/>, or
    /// <paramref name="u"/> is negative.</exception>
    public static double AbcdBlackVolatility(double u, double a, double b, double c, double d)
    {
        var model = new AbcdFunction(a, b, c, d);
        return model.Volatility(0.0, u, u);
    }
}
