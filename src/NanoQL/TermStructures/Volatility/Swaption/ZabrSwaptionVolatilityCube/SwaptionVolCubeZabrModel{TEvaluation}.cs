// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/zabrswaptionvolatilitycube.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The ZABR model of a swaption volatility cube: QuantLib's <c>SwaptionVolCubeZabrModel&lt;Kernel&gt;</c> with its
/// <c>XabrModelTraits</c> specialization, its smiles fitted by <see cref="ZabrInterpolation{TEvaluation}"/> and its
/// sections <see cref="ZabrSmileSection{TEvaluation}"/>s, five parameters α, β, ν, ρ and γ.
/// </summary>
/// <typeparam name="TEvaluation">The kernel: <see cref="ZabrShortMaturityLognormal"/>, QuantLib's default, or
/// <see cref="ZabrShortMaturityNormal"/>.</typeparam>
/// <remarks>
/// A stateless trait: its members are static, and every instance, its zero value included, is the same model. Fewer
/// than five guesses or fixed flags are refused, as QuantLib's traits refuse them; more are ignored. ZABR has no shifted
/// form: a section on a shifted at-the-money structure is refused when it is built, at the cube's construction for an
/// at-the-money-calibrated cube and at its first query otherwise, as QuantLib's is.
/// </remarks>
public readonly struct SwaptionVolCubeZabrModel<TEvaluation> : IXabrCubeModel
    where TEvaluation : struct, IZabrEvaluation
{
    /// <summary>Gets the model's parameter count: 5.</summary>
    public static int NParams => 5;

    /// <summary>A ZABR smile fitted to discrete volatilities, from the five guesses and fixed flags.</summary>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="volatilities">The volatilities, one per strike.</param>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The guesses of α, β, ν, ρ and γ, at least five.</param>
    /// <param name="paramIsFixed">Whether each of α, β, ν, ρ and γ is fixed at its guess, at least five flags.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the strikes' Black vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for the fit's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for the fit's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and the restarts stop.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations.</param>
    /// <param name="shift">Ignored, as QuantLib ignores it.</param>
    /// <param name="volatilityType">Ignored, as QuantLib ignores it.</param>
    /// <returns>The fitted <see cref="ZabrInterpolation{TEvaluation}"/>.</returns>
    /// <exception cref="ArgumentException">There are fewer than five guesses or flags, or the fit refuses its
    /// arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    /// <exception cref="NanoQLException">The optimizer or the model's integration fails.</exception>
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
        if (!(parameters.Length >= NParams))
        {
            throw new ArgumentException(
                $"ZABR model requires {NParams} parameters, but {parameters.Length} provided", nameof(parameters));
        }

        if (!(paramIsFixed.Length >= NParams))
        {
            throw new ArgumentException(
                $"ZABR model requires {NParams} fixed flags, but {paramIsFixed.Length} provided", nameof(paramIsFixed));
        }

        return new ZabrInterpolation<TEvaluation>(
            strikes,
            volatilities,
            t,
            forward,
            parameters[0],
            parameters[1],
            parameters[2],
            parameters[3],
            parameters[4],
            paramIsFixed[0],
            paramIsFixed[1],
            paramIsFixed[2],
            paramIsFixed[3],
            paramIsFixed[4],
            vegaWeighted,
            endCriteria,
            optMethod,
            errorAccept,
            useMaxError,
            maxGuesses);
    }

    /// <summary>The fitted γ.</summary>
    /// <param name="interpolation">The fitted interpolation, one <see cref="CreateInterpolation"/> returned.</param>
    /// <returns>γ.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="interpolation"/> is null.</exception>
    /// <exception cref="InvalidCastException"><paramref name="interpolation"/> isn't this model's.</exception>
    public static double ExtractGamma(IXabrInterpolation interpolation)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        return ((ZabrInterpolation<TEvaluation>)interpolation).Gamma;
    }

    /// <summary>The ZABR smile section at given parameters, on an unshifted structure.</summary>
    /// <param name="optionTime">The exercise time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">α, β, ν, ρ and γ, in that order.</param>
    /// <param name="shift">The at-the-money structure's shift, which must be 0.</param>
    /// <param name="volatilityType">Ignored, as QuantLib ignores it.</param>
    /// <returns>The <see cref="ZabrSmileSection{TEvaluation}"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="shift"/> isn't close to 0.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    public static SmileSection CreateSmileSection(
        double optionTime, double forward, ReadOnlySpan<double> parameters, double shift, VolatilityType volatilityType)
    {
        if (!ComparisonFunctions.Close(shift, 0.0))
        {
            throw new ArgumentException(
                "ZABR model does not support shifted volatilities; shift = "
                + shift.ToString(CultureInfo.InvariantCulture) + " was provided",
                nameof(shift));
        }

        return new ZabrSmileSection<TEvaluation>(optionTime, forward, parameters);
    }
}
