// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2023 Ignacio Anguita
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabrswaptionvolatilitycube.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The XABR cube's model for Doust's no-arbitrage SABR: QuantLib's <c>SwaptionVolCubeNoArbSabrModel</c> with its
/// traits' specialization, the smiles <see cref="NoArbSabrInterpolation"/> fits, the sections
/// <see cref="NoArbSabrSmileSection"/>s, no γ.
/// </summary>
/// <remarks>QuantLib's specialization drops the volatility type the interpolation doesn't take, as here.</remarks>
[Experimental("NQL0001")]
public readonly struct SwaptionVolCubeNoArbSabrModel : IXabrCubeModel
{
    /// <summary>A no-arbitrage SABR fit to a smile.</summary>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="volatilities">The volatilities, one per strike.</param>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The guesses of α, β, ν and ρ.</param>
    /// <param name="paramIsFixed">Whether each is fixed.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by vega.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for the fit's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for the fit's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error.</param>
    /// <param name="maxGuesses">The most minimizations.</param>
    /// <param name="shift">The shift, which must be 0.</param>
    /// <param name="volatilityType">The volatility type, which the fit doesn't take.</param>
    /// <returns>The fit.</returns>
    /// <exception cref="ArgumentException">Fewer than four parameters or flags are given, the shift isn't 0, or the
    /// fit refuses its inputs.</exception>
    /// <exception cref="NanoQLException">The fit fails.</exception>
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
        // QuantLib's specialization reads params[0..3] and paramIsFixed[0..3] unchecked
        if (!(parameters.Length >= 4))
        {
            throw new ArgumentException(
                $"no-arbitrage SABR model requires 4 parameters, but {parameters.Length} provided", nameof(parameters));
        }

        if (!(paramIsFixed.Length >= 4))
        {
            throw new ArgumentException(
                $"no-arbitrage SABR model requires 4 fixed flags, but {paramIsFixed.Length} provided", nameof(paramIsFixed));
        }

        return new NoArbSabrInterpolation(
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
            shift);
    }

    /// <summary>The no-arbitrage SABR section at fitted parameters.</summary>
    /// <param name="optionTime">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">α, β, ν and ρ.</param>
    /// <param name="shift">The shift, which must be 0.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <returns>The section.</returns>
    /// <exception cref="ArgumentException">The section or the model refuses its inputs.</exception>
    /// <exception cref="NanoQLException">The model's integration fails.</exception>
    public static SmileSection CreateSmileSection(
        double optionTime, double forward, ReadOnlySpan<double> parameters, double shift, VolatilityType volatilityType) =>
        new NoArbSabrSmileSection(optionTime, forward, parameters, shift, volatilityType);
}
