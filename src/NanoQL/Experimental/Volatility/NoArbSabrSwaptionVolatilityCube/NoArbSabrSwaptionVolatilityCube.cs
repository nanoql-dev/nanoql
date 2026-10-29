// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2023 Ignacio Anguita
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabrswaptionvolatilitycube.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The no-arbitrage SABR swaption volatility cube: QuantLib's <c>NoArbSabrSwaptionVolatilityCube</c>, the XABR cube on
/// Doust's no-arbitrage SABR model, whose smiles are <see cref="NoArbSabrInterpolation"/> fits and whose sections
/// are <see cref="NoArbSabrSmileSection"/>s.
/// </summary>
/// <remarks>
/// QuantLib's typedef of <c>XabrSwaptionVolatilityCube&lt;SwaptionVolCubeNoArbSabrModel&gt;</c>, sealed over the generic
/// cube, which it adds nothing to: see <see cref="XabrSwaptionVolatilityCube{TModel}"/>.
/// </remarks>
[Experimental("NQL0001")]
public sealed class NoArbSabrSwaptionVolatilityCube : XabrSwaptionVolatilityCube<SwaptionVolCubeNoArbSabrModel>
{
    /// <summary>Initializes a new instance of the <see cref="NoArbSabrSwaptionVolatilityCube"/> class, and calibrates
    /// it.</summary>
    /// <param name="atmVolStructure">The at-the-money volatility structure; a <see cref="SwaptionVolatilityDiscrete"/>
    /// for an at-the-money-calibrated or single-pass cube.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing; at least 2.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years; at least 2.</param>
    /// <param name="strikeSpreads">The strike spreads from the at-the-money strike, increasing; at least 1.</param>
    /// <param name="volSpreads">The volatility spreads: a row per option and swap tenor pair, the pair (j, k) in row
    /// j·(swap tenors) + k, with a spread per strike spread.</param>
    /// <param name="swapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the swap
    /// tenors past the short index's.</param>
    /// <param name="shortSwapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the
    /// other swap tenors.</param>
    /// <param name="vegaWeightedSmileFit">Whether the fits weight their errors by the strikes' vegas.</param>
    /// <param name="parametersGuess">The guesses of α, β, ν and ρ: a row per option and swap tenor pair, laid out as the
    /// volatility spreads.</param>
    /// <param name="isParameterFixed">Whether each of α, β, ν and ρ is fixed at its guess.</param>
    /// <param name="isAtmCalibrated">Whether the cube fills and fits its dense grid.</param>
    /// <param name="context">The evaluation context, which the at-the-money strikes read.</param>
    /// <param name="endCriteria">The fits' end criteria, or <see langword="null"/> for the fit's default.</param>
    /// <param name="maxErrorTolerance">The error a fit must stay below, or <see langword="null"/> for QuantLib's 0.01,
    /// 0.0015 with vega weights.</param>
    /// <param name="optMethod">The fits' optimizer, or <see langword="null"/> for the fit's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and its restarts stop, or
    /// <see langword="null"/> for a fifth of the tolerance.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations of a fit.</param>
    /// <param name="backwardFlat">Whether the parameters are interpolated backward flat in option time.</param>
    /// <param name="cutoffStrike">The lowest strike plus shift a fit takes.</param>
    /// <param name="singlePassCalibration">Whether the cube fits its dense grid alone, from the quoted spreads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, the structure, a collection or an index is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The generic cube refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    /// <exception cref="NanoQLException">A fit fails or reaches the tolerance, an at-the-money strike can't be computed,
    /// or the dense grid reaches past the cube's.</exception>
    public NoArbSabrSwaptionVolatilityCube(
        SwaptionVolatilityStructure atmVolStructure,
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        IEnumerable<double> strikeSpreads,
        IEnumerable<IEnumerable<double>> volSpreads,
        SwapIndex swapIndexBase,
        SwapIndex shortSwapIndexBase,
        bool vegaWeightedSmileFit,
        IEnumerable<IEnumerable<double>> parametersGuess,
        IEnumerable<bool> isParameterFixed,
        bool isAtmCalibrated,
        EvaluationContext context,
        EndCriteria? endCriteria = null,
        double? maxErrorTolerance = null,
        OptimizationMethod? optMethod = null,
        double? errorAccept = null,
        bool useMaxError = false,
        int maxGuesses = 50,
        bool backwardFlat = false,
        double cutoffStrike = 0.0001,
        bool singlePassCalibration = false)
        : base(
            atmVolStructure,
            optionTenors,
            swapTenors,
            strikeSpreads,
            volSpreads,
            swapIndexBase,
            shortSwapIndexBase,
            vegaWeightedSmileFit,
            parametersGuess,
            isParameterFixed,
            isAtmCalibrated,
            context,
            endCriteria,
            maxErrorTolerance,
            optMethod,
            errorAccept,
            useMaxError,
            maxGuesses,
            backwardFlat,
            cutoffStrike,
            singlePassCalibration)
    {
    }
}
