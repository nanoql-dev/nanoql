// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib-Python's <c>ZabrFullFdSmileSection</c>: a <see cref="ZabrSmileSection{TEvaluation}"/> with the
/// full finite-difference kernel.
/// </summary>
public sealed class ZabrFullFdSmileSection : ZabrSmileSection<ZabrFullFd>
{
    /// <summary>Initializes a new instance of the <see cref="ZabrFullFdSmileSection"/> class for an
    /// exercise time.</summary>
    /// <param name="timeToExpiry">The exercise time, positive.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="zabrParameters">α, β, ν, ρ and γ, in that order; values after the fifth are ignored.</param>
    /// <param name="moneyness">The strike grid's moneyness, empty for the default.</param>
    /// <param name="fdRefinement">The strike grid's points between two of its strikes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fdRefinement"/> is negative.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    public ZabrFullFdSmileSection(
        double timeToExpiry,
        double forward,
        ReadOnlySpan<double> zabrParameters,
        ReadOnlySpan<double> moneyness = default,
        int fdRefinement = 5)
        : base(timeToExpiry, forward, zabrParameters, moneyness, fdRefinement)
    {
    }

    private ZabrFullFdSmileSection(
        Date d,
        double forward,
        ReadOnlySpan<double> zabrParameters,
        Date referenceDate,
        DayCounter? dc,
        ReadOnlySpan<double> moneyness,
        int fdRefinement)
        : base(d, forward, zabrParameters, referenceDate, dc, moneyness, fdRefinement)
    {
    }

    /// <summary>A section for an exercise date, whose reference date is the evaluation date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="zabrParameters">α, β, ν, ρ and γ, in that order.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="moneyness">The strike grid's moneyness, empty for the default.</param>
    /// <param name="fdRefinement">The strike grid's points between two of its strikes.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fdRefinement"/> is negative.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    public static new ZabrFullFdSmileSection Create(
        Date d,
        double forward,
        ReadOnlySpan<double> zabrParameters,
        EvaluationContext context,
        DayCounter? dc = null,
        ReadOnlySpan<double> moneyness = default,
        int fdRefinement = 5)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ZabrFullFdSmileSection(
            d, forward, zabrParameters, context.EvaluationDate, dc, moneyness, fdRefinement);
    }
}
