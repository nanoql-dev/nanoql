// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomlosslatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The random spot recovery loss model simulation on the Student t copula, inverting a Sobol sequence:
/// QuantLib's typedef <c>TRandomLossLM</c>.</summary>
[Experimental("NQL0001")]
public sealed class TRandomLossLM : RandomLossLM<TCopulaPolicy, TCopulaPolicyInitTraits, SobolRsg>
{
    /// <summary>Initializes a new instance of the <see cref="TRandomLossLM"/> class.</summary>
    /// <param name="copula">The spot-recovery latent model.</param>
    /// <param name="nSims">The number of paths, at least 1.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295]; QuantLib's default is 2863311530.</param>
    /// <param name="accuracy">The default day's solver accuracy.</param>
    /// <param name="maxHorizon">The simulation's horizon in days, QuantLib's 4,050 by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copula"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count or the seed lies outside its range.</exception>
    public TRandomLossLM(
        SpotRecoveryLatentModel<TCopulaPolicy, TCopulaPolicyInitTraits> copula, int nSims, long seed, double accuracy = 1.0e-6, int maxHorizon = 4050)
        : base(copula, nSims, seed, accuracy, maxHorizon)
    {
    }
}
