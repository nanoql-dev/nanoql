// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrinterpolatedsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib-Python's <c>ZabrShortMaturityLognormalInterpolatedSmileSection</c>: a
/// <see cref="ZabrInterpolatedSmileSection{TEvaluation}"/> with the short-maturity lognormal kernel.
/// </summary>
public sealed class ZabrShortMaturityLognormalInterpolatedSmileSection : ZabrInterpolatedSmileSection<ZabrShortMaturityLognormal>
{
    private ZabrShortMaturityLognormalInterpolatedSmileSection(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double?> vols,
        double? alpha,
        double? beta,
        double? nu,
        double? rho,
        double? gamma,
        Date referenceDate,
        bool isAlphaFixed,
        bool isBetaFixed,
        bool isNuFixed,
        bool isRhoFixed,
        bool isGammaFixed,
        bool vegaWeighted,
        EndCriteria? endCriteria,
        OptimizationMethod? method,
        DayCounter? dc)
        : base(
            optionDate,
            forward,
            strikes,
            hasFloatingStrikes,
            atmVolatility,
            vols,
            alpha,
            beta,
            nu,
            rho,
            gamma,
            referenceDate,
            isAlphaFixed,
            isBetaFixed,
            isNuFixed,
            isRhoFixed,
            isGammaFixed,
            vegaWeighted,
            endCriteria,
            method,
            dc)
    {
    }

    /// <summary>A section for an option date, whose reference date is the evaluation date, from market
    /// volatilities.</summary>
    /// <param name="optionDate">The option's exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strikes">The strikes, sorted; with floating strikes, the offsets from the forward.</param>
    /// <param name="hasFloatingStrikes">Whether the strikes and the volatilities are offsets and spreads.</param>
    /// <param name="atmVolatility">The at-the-money volatility, read with floating strikes.</param>
    /// <param name="vols">The market volatilities, or with floating strikes their spreads; <see langword="null"/>
    /// leaves its strike out of the fit.</param>
    /// <param name="alpha">The guess or fixed value of α; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="beta">The guess or fixed value of β; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="nu">The guess or fixed value of ν; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="rho">The guess or fixed value of ρ; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="gamma">The guess or fixed value of γ; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="isAlphaFixed">Whether α is fixed.</param>
    /// <param name="isBetaFixed">Whether β is fixed.</param>
    /// <param name="isNuFixed">Whether ν is fixed.</param>
    /// <param name="isRhoFixed">Whether ρ is fixed.</param>
    /// <param name="isGammaFixed">Whether γ is fixed.</param>
    /// <param name="vegaWeighted">Whether the fit's errors are weighted by vega.</param>
    /// <param name="endCriteria">The fit's end criteria, or <see langword="null"/> for ZABR's default.</param>
    /// <param name="method">The fit's optimization method, or <see langword="null"/> for ZABR's default.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="NanoQLException">The fit fails.</exception>
    public static new ZabrShortMaturityLognormalInterpolatedSmileSection Create(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double?> vols,
        double? alpha,
        double? beta,
        double? nu,
        double? rho,
        double? gamma,
        EvaluationContext context,
        bool isAlphaFixed = false,
        bool isBetaFixed = false,
        bool isNuFixed = false,
        bool isRhoFixed = false,
        bool isGammaFixed = false,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null,
        DayCounter? dc = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ZabrShortMaturityLognormalInterpolatedSmileSection(
            optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, vols, alpha, beta, nu, rho, gamma,
            context.EvaluationDate, isAlphaFixed, isBetaFixed, isNuFixed, isRhoFixed, isGammaFixed, vegaWeighted, endCriteria,
            method, dc);
    }
}
