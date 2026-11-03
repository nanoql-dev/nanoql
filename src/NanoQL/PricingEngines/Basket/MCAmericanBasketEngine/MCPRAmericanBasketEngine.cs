// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/mcamericanbasketengine.hpp ql/pricingengines/basket/mcamericanbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCAmericanBasketEngine"/> on pseudo-random draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCPRAmericanBasketEngine</c>, QuantLib's <c>MCAmericanBasketEngine&lt;PseudoRandom&gt;</c>, its calculations
/// closed at <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/>.
/// </summary>
public static class MCPRAmericanBasketEngine
{
    /// <summary>Prices an American or Bermudan basket option by Longstaff-Schwartz.</summary>
    /// <param name="option">The basket option, of an early exercise.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="nCalibrationSamples">The calibration samples, or <see langword="null"/> for 2,048.</param>
    /// <param name="polynomialOrder">The regression basis' highest order.</param>
    /// <param name="polynomialType">The regression basis' family.</param>
    /// <param name="seedCalibration">
    /// The calibration seed, in [1, 4294967295], or <see langword="null"/> for the Longstaff-Schwartz base's default;
    /// QuantLib's engine takes none, and its base's default is always 4294967295, the narrowed null, which passing that
    /// seed reproduces.
    /// </param>
    /// <returns>The option's results: the value, its error estimate, and the exercise probability.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The steps are given neither or both ways or aren't positive, or the basis family is another.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A number of samples is negative, or the seed is outside its range.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The first process isn't a Black-Scholes process, the exercise isn't early or pays at expiry, or the samples
    /// can't reach the tolerance.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? nCalibrationSamples = null,
        int polynomialOrder = 2,
        LsmBasisSystemPolynomialType polynomialType = LsmBasisSystemPolynomialType.Monomial,
        long? seedCalibration = null) =>
        MCAmericanBasketEngine.Calculate<PseudoRandom, GeneralStatistics>(option, context, processes, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed, nCalibrationSamples, polynomialOrder, polynomialType, seedCalibration);
}
