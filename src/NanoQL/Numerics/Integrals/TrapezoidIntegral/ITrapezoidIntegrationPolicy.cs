// SPDX-FileCopyrightText: Copyright (C) 2003 Roman Gitlin
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/trapezoidintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The concept of QuantLib's trapezoid integration policies (<c>Default</c>, <c>MidPoint</c>): one refinement of an
/// estimate, and the factor by which it multiplies the number of intervals.
/// </summary>
public interface ITrapezoidIntegrationPolicy
{
    /// <summary>The refined estimate over <paramref name="N"/> intervals times the policy's factor.</summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit.</param>
    /// <param name="b">The upper limit.</param>
    /// <param name="I">The estimate over <paramref name="N"/> intervals.</param>
    /// <param name="N">The number of intervals of <paramref name="I"/>.</param>
    /// <returns>The refined estimate.</returns>
    static abstract double Integrate(Func<double, double> f, double a, double b, double I, int N);

    /// <summary>The factor by which a refinement multiplies the number of intervals.</summary>
    /// <returns>The factor; a refinement evaluates the integrand N·(factor − 1) times.</returns>
    static abstract int NbEvalutions();

    /// <summary>
    /// The one-interval estimate the refinements start from, and the number of evaluations it takes. The default is
    /// QuantLib's start for every policy, the trapezoid's (<c>trapezoidintegral.hpp:62-64</c>); a policy whose
    /// refinement doesn't refine the trapezoid overrides it.
    /// </summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit.</param>
    /// <param name="b">The upper limit.</param>
    /// <returns>The estimate and its number of evaluations.</returns>
    static virtual IntegrationResult InitialEstimate(Func<double, double> f, double a, double b) =>
        new((f(a) + f(b)) * (b - a) / 2.0, 2);
}
