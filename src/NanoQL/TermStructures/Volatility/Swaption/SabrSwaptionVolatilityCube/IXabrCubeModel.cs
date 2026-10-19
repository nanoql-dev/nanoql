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
/// The smile model of an XABR swaption volatility cube, QuantLib's <c>XabrModelTraits&lt;Model&gt;</c> with its
/// model's <c>Interpolation</c> and <c>SmileSection</c> types: the model's parameter count, the fit of a smile, the
/// fitted fifth parameter of a five-parameter model, and the smile section at given parameters.
/// </summary>
/// <remarks>
/// <para>
/// A stateless trait: a model is a <see langword="readonly"/> struct implementing it, its members static, as
/// <see cref="SwaptionVolCubeSabrModel"/> is. <see cref="NParams"/> and <see cref="ExtractGamma"/> carry QuantLib's
/// primary template's defaults, four parameters and no fifth, which a five-parameter model overrides; the two factories
/// are the model's own.
/// </para>
/// <para>An extension point: a model of one's own implements it, to be fitted by an XABR cube on it.</para>
/// </remarks>
public interface IXabrCubeModel
{
    /// <summary>Gets the model's parameter count; by default 4, α, β, ν and ρ.</summary>
    static virtual int NParams => 4;

    /// <summary>The model's smile fitted to discrete volatilities: QuantLib's <c>createInterpolation</c>.</summary>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="volatilities">The volatilities, one per strike.</param>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The parameters' guesses, one per parameter; values after the model's count are
    /// ignored.</param>
    /// <param name="paramIsFixed">Whether each parameter is fixed at its guess; flags after the model's count are
    /// ignored.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the strikes' vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for the fit's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for the fit's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and the restarts stop.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations.</param>
    /// <param name="shift">The shift of a shifted model.</param>
    /// <param name="volatilityType">The volatilities' type.</param>
    /// <returns>The fitted interpolation.</returns>
    static abstract IXabrInterpolation CreateInterpolation(
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
        VolatilityType volatilityType);

    /// <summary>The fitted fifth parameter of a five-parameter model; by default 0.</summary>
    /// <param name="interpolation">The fitted interpolation, one <see cref="CreateInterpolation"/> returned.</param>
    /// <returns>The parameter.</returns>
    static virtual double ExtractGamma(IXabrInterpolation interpolation) => 0.0;

    /// <summary>The model's smile section at given parameters: QuantLib's <c>createSmileSection</c>.</summary>
    /// <param name="optionTime">The exercise time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The parameters, one per parameter.</param>
    /// <param name="shift">The shift of a shifted model.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <returns>The section.</returns>
    static abstract SmileSection CreateSmileSection(
        double optionTime, double forward, ReadOnlySpan<double> parameters, double shift, VolatilityType volatilityType);
}
