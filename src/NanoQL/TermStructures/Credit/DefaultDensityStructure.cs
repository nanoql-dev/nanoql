// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/defaultdensitystructure.hpp ql/termstructures/credit/defaultdensitystructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The default-density term structure: an adapter that lets a derived curve implement the default density, from which
/// the hazard rates follow.
/// </summary>
/// <remarks>
/// An extension point. The default survival probability is QuantLib's: 1 less the default density integrated
/// numerically with a 48-point Gauss-Chebyshev rule, floored at 0, which QuantLib warns "might be inefficient and
/// inaccurate. Derived classes should override it if a more efficient implementation is available": the rule
/// integrates a constant over [-1, 1] to 2.000357018144566, a relative error of 1.785e-4. No QuantLib class reads it;
/// the curves of S-infl's slot implement it exactly (the S-xcr plan's XR3 (a)).
/// </remarks>
public abstract class DefaultDensityStructure : DefaultProbabilityTermStructure
{
    private static readonly GaussChebyshevIntegration Integral = new(48);

    /// <summary>Initializes a new instance of the <see cref="DefaultDensityStructure"/> class with a fixed reference
    /// date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar, if any.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentException">Jump dates are given and their number differs from the jumps', or one of
    /// them is <c>default(Date)</c>.</exception>
    protected DefaultDensityStructure(
        Date referenceDate,
        Calendar? cal,
        DayCounter dc,
        int? settlementDays = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
        : base(referenceDate, cal, dc, settlementDays, jumps, jumpDates)
    {
    }

    /// <summary>The survival probability at a time: QuantLib's default, 1 less the default density integrated from 0
    /// with a 48-point Gauss-Chebyshev rule, floored at 0, which derived curves should override where they can do
    /// better.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The probability.</returns>
    protected override double SurvivalProbabilityImpl(double t)
    {
        // the Gauss-Chebyshev quadratures integrate over [-1,1],
        // hence the remapping (and the Jacobian term t/2)
        double p = 1.0 - (Integral.Evaluate(x => DefaultDensityImpl((x + 1.0) * t / 2.0)) * t / 2.0);
        return Math.Max(p, 0.0);
    }
}
