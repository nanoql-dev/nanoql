// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomdefaultlatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The random default latent model simulation on the Gaussian copula, inverting a Sobol sequence: QuantLib's
/// typedef <c>GaussianRandomDefaultLM</c>.</summary>
[Experimental("NQL0001")]
public sealed class GaussianRandomDefaultLM : RandomDefaultLM<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits, SobolRsg>
{
    /// <summary>Initializes a new instance of the <see cref="GaussianRandomDefaultLM"/> class on a default latent model and its names'
    /// recoveries.</summary>
    /// <param name="model">The default latent model.</param>
    /// <param name="recoveries">The names' recoveries; <see langword="null"/> or empty for 0 each.</param>
    /// <param name="nSims">The number of paths, at least 1.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295]; QuantLib's default is 2863311530.</param>
    /// <param name="accuracy">The default day's solver accuracy.</param>
    /// <param name="maxHorizon">The simulation's horizon in days, QuantLib's 4,050 by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count or the seed lies outside its range.</exception>
    public GaussianRandomDefaultLM(
        DefaultLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> model,
        IReadOnlyList<double>? recoveries,
        int nSims,
        long seed,
        double accuracy = 1.0e-6,
        int maxHorizon = 4050)
        : base(model, recoveries, nSims, seed, accuracy, maxHorizon)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GaussianRandomDefaultLM"/> class on a constant-loss latent model, its
    /// recoveries the model's.</summary>
    /// <param name="model">The constant-loss latent model.</param>
    /// <param name="nSims">The number of paths, at least 1.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295]; QuantLib's default is 2863311530.</param>
    /// <param name="accuracy">The default day's solver accuracy.</param>
    /// <param name="maxHorizon">The simulation's horizon in days, QuantLib's 4,050 by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count or the seed lies outside its range.</exception>
    public GaussianRandomDefaultLM(
        ConstantLossLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> model, int nSims, long seed, double accuracy = 1.0e-6, int maxHorizon = 4050)
        : base(model, nSims, seed, accuracy, maxHorizon)
    {
    }
}
