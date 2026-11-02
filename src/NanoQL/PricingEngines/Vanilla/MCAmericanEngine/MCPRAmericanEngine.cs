// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2022 Jonghee Lee
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcamericanengine.hpp ql/pricingengines/vanilla/mcamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCAmericanEngine"/> on pseudo-random draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCPRAmericanEngine</c>, QuantLib's <c>MCAmericanEngine&lt;PseudoRandom&gt;</c>, its calculations closed at
/// <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/> (D12).
/// </summary>
public static class MCPRAmericanEngine
{
    /// <summary>Prices an American or Bermudan vanilla option by Longstaff-Schwartz Monte Carlo.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of
    /// steps.</param>
    /// <param name="antitheticVariate">Whether the pricing uses antithetic variates.</param>
    /// <param name="controlVariate">Whether the European option is a control variate.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The pricing seed, in [1, 4294967295].</param>
    /// <param name="polynomialOrder">The regression basis' highest order.</param>
    /// <param name="polynomialType">The regression basis' family.</param>
    /// <param name="nCalibrationSamples">The calibration samples, or <see langword="null"/> for 2,048.</param>
    /// <param name="antitheticVariateCalibration">The calibration's antithetic variates, or <see langword="null"/> for
    /// the pricing's.</param>
    /// <param name="seedCalibration">The calibration seed, or <see langword="null"/> for (seed + 1768237423) mod
    /// 2³².</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The time steps are given neither or both ways, or aren't positive; the basis family is insufficient; a
    /// regression's matrix isn't finite (the second-kind Chebyshev basis on a call, VR7).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A number of samples is negative, a seed is outside its range, or the default calibration seed is 0.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't early or pays at expiry, the control variate's payoff isn't striked, neither a tolerance nor
    /// a number of samples is given, or the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int polynomialOrder,
        LsmBasisSystemPolynomialType polynomialType,
        int? nCalibrationSamples = null,
        bool? antitheticVariateCalibration = null,
        long? seedCalibration = null) =>
        MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(option, context, process, timeSteps, timeStepsPerYear, antitheticVariate, controlVariate, requiredSamples, requiredTolerance, maxSamples, seed, polynomialOrder, polynomialType, nCalibrationSamples, antitheticVariateCalibration, seedCalibration);
}
