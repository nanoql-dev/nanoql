// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The concept of a latent model's copula policy: the distributions of the systemic factors, the idiosyncratic
/// factors and the latent variables they combine into, which <see cref="LatentModel{TCopula, TInitTraits}"/> takes as
/// its template parameter.
/// </summary>
/// <remarks>
/// QuantLib's policies are classes a latent model holds and builds from its factor weights and the policy's
/// <c>initTraits</c>; nanoql's are <c>readonly struct</c>s over immutable state, built through <see cref="Create"/>.
/// The factors are expected of zero mean and unit variance; nothing checks it, as in QuantLib.
/// </remarks>
/// <typeparam name="TSelf">The policy itself.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters, QuantLib's nested <c>initTraits</c>.</typeparam>
[Experimental("NQL0001")]
public interface ICopulaPolicy<TSelf, TInitTraits>
    where TSelf : struct, ICopulaPolicy<TSelf, TInitTraits>
    where TInitTraits : struct
{
    /// <summary>Gets the number of independent random factors, systemic and idiosyncratic.</summary>
    int NumFactors { get; }

    /// <summary>The policy built from a latent model's factor weights and its initialization parameters.</summary>
    /// <param name="factorWeights">The weights, <c>factorWeights[iVariable][iFactor]</c>.</param>
    /// <param name="initTraits">The initialization parameters.</param>
    /// <returns>The policy.</returns>
    static abstract TSelf Create(ImmutableArray<ImmutableArray<double>> factorWeights, TInitTraits initTraits);

    /// <summary>A copy of the initialization parameters.</summary>
    /// <returns>The parameters.</returns>
    TInitTraits GetInitTraits();

    /// <summary>The cumulative probability of a latent variable taking a value.</summary>
    /// <param name="val">The value.</param>
    /// <param name="iVariable">The variable's index.</param>
    /// <returns>The probability.</returns>
    double CumulativeY(double val, int iVariable);

    /// <summary>The cumulative probability of the idiosyncratic factors, all the same.</summary>
    /// <param name="z">The value.</param>
    /// <returns>The probability.</returns>
    double CumulativeZ(double z);

    /// <summary>The joint density of a realization of the systemic factors, which are independent.</summary>
    /// <param name="m">The systemic factors' values.</param>
    /// <returns>The density.</returns>
    double Density(ReadOnlySpan<double> m);

    /// <summary>The inverse of a latent variable's cumulative distribution.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="iVariable">The variable's index.</param>
    /// <returns>The value.</returns>
    double InverseCumulativeY(double p, int iVariable);

    /// <summary>The inverse of the idiosyncratic factors' cumulative distribution.</summary>
    /// <param name="p">The probability.</param>
    /// <returns>The value.</returns>
    double InverseCumulativeZ(double p);

    /// <summary>The inverse of a systemic factor's cumulative distribution.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="iFactor">The factor's index.</param>
    /// <returns>The value.</returns>
    double InverseCumulativeDensity(double p, int iFactor);

    /// <summary>The inverse of every factor's cumulative distribution: the systemic factors', then the idiosyncratic
    /// ones'. The probabilities are expected uniform.</summary>
    /// <param name="probs">The probabilities, one per factor.</param>
    /// <returns>The factors' values.</returns>
    double[] AllFactorCumulInverter(ReadOnlySpan<double> probs);
}
