// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/abcd.hpp ql/termstructures/volatility/abcd.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// The instantaneous covariance of two abcd rates as a function of time, f(T − t)·f(S − t): QuantLib's helper for its
/// integration tests.
/// </summary>
public sealed class AbcdSquared
{
    private readonly AbcdFunction abcd;
    private readonly double fixingT;
    private readonly double fixingS;

    /// <summary>Initializes a new instance of the <see cref="AbcdSquared"/> class.</summary>
    /// <param name="a">a.</param>
    /// <param name="b">b.</param>
    /// <param name="c">c, positive.</param>
    /// <param name="d">d, non-negative.</param>
    /// <param name="T">The first rate's fixing time.</param>
    /// <param name="S">The second rate's fixing time.</param>
    /// <exception cref="ArgumentException">The coefficients fail
    /// <see cref="Numerics.AbcdMathFunction.Validate"/>.</exception>
    public AbcdSquared(double a, double b, double c, double d, double T, double S)
    {
        abcd = new AbcdFunction(a, b, c, d);
        fixingT = T;
        fixingS = S;
    }

    /// <summary>The instantaneous covariance at a time, QuantLib's <c>operator()</c>.</summary>
    /// <param name="t">The time.</param>
    /// <returns>f(T − t)·f(S − t).</returns>
    public double Evaluate(double t) => abcd.Covariance(t, fixingT, fixingS);
}
