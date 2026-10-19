// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2023 Ignacio Anguita
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/sabrswaptionvolatilitycube.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The SABR model of a swaption volatility cube: QuantLib's <c>SwaptionVolCubeSabrModel</c> on the primary
/// <c>XabrModelTraits</c>, its smiles fitted by <see cref="SABRInterpolation"/> and its sections
/// <see cref="SabrSmileSection"/>s.
/// </summary>
/// <remarks>
/// A stateless trait: its members are static, and every instance, its zero value included, is the same model. QuantLib's
/// primary traits read the first four guesses and fixed flags unchecked; fewer than four of either are refused here, in
/// the form of the checks QuantLib's ZABR traits make. More are ignored, as QuantLib ignores them.
/// </remarks>
public readonly struct SwaptionVolCubeSabrModel : IXabrCubeModel
{
    /// <summary>A SABR smile fitted to discrete volatilities, from the four guesses and fixed flags.</summary>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="volatilities">The volatilities, one per strike.</param>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The guesses of α, β, ν and ρ, at least four.</param>
    /// <param name="paramIsFixed">Whether each of α, β, ν and ρ is fixed at its guess, at least four flags.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the strikes' Black vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for the fit's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for the fit's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and the restarts stop.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations.</param>
    /// <param name="shift">The shift of a shifted SABR model.</param>
    /// <param name="volatilityType">The volatilities' type.</param>
    /// <returns>The fitted <see cref="SABRInterpolation"/>.</returns>
    /// <exception cref="ArgumentException">There are fewer than four guesses or flags, or the fit refuses its
    /// arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative, or
    /// <paramref name="volatilityType"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The optimizer fails.</exception>
    public static IXabrInterpolation CreateInterpolation(
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> volatilities,
        double t,
        double forward,
        ReadOnlySpan<double> parameters,
        ReadOnlySpan<bool> paramIsFixed,
        bool vegaWeighted,
        EndCriteria? endCriteria,
        OptimizationMethod? optMethod,
        double errorAccept,
        bool useMaxError,
        int maxGuesses,
        double shift,
        VolatilityType volatilityType)
    {
        // QuantLib's primary traits read params[0..3] and paramIsFixed[0..3] unchecked.
        if (!(parameters.Length >= 4))
        {
            throw new ArgumentException(
                $"SABR model requires 4 parameters, but {parameters.Length} provided", nameof(parameters));
        }

        if (!(paramIsFixed.Length >= 4))
        {
            throw new ArgumentException(
                $"SABR model requires 4 fixed flags, but {paramIsFixed.Length} provided", nameof(paramIsFixed));
        }

        return new SABRInterpolation(
            strikes,
            volatilities,
            t,
            forward,
            parameters[0],
            parameters[1],
            parameters[2],
            parameters[3],
            paramIsFixed[0],
            paramIsFixed[1],
            paramIsFixed[2],
            paramIsFixed[3],
            vegaWeighted,
            endCriteria,
            optMethod,
            errorAccept,
            useMaxError,
            maxGuesses,
            shift,
            volatilityType);
    }

    /// <summary>The SABR smile section at given parameters.</summary>
    /// <param name="optionTime">The exercise time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">α, β, ν and ρ, in that order.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <returns>The <see cref="SabrSmileSection"/>.</returns>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    public static SmileSection CreateSmileSection(
        double optionTime, double forward, ReadOnlySpan<double> parameters, double shift, VolatilityType volatilityType) =>
        new SabrSmileSection(optionTime, forward, parameters, shift, volatilityType);
}
