// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/spotlosslatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The spot-recovery latent model on the Gaussian copula: QuantLib's typedef <c>GaussianSpotLossLM</c>.</summary>
[Experimental("NQL0001")]
public sealed class GaussianSpotLossLM : SpotRecoveryLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="GaussianSpotLossLM"/> class.</summary>
    /// <param name="factorWeights">The weights of the n default variables, then of the n recovery variables.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="modelA">The model's parameter A, not 0.</param>
    /// <param name="integralType">The integration's type.</param>
    /// <param name="initTraits">The policy's initialization parameters.</param>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights' rows are odd in number, the recoveries don't match the names,
    /// or A is 0.</exception>
    public GaussianSpotLossLM(
        IReadOnlyList<IReadOnlyList<double>> factorWeights,
        IReadOnlyList<double> recoveries,
        double modelA,
        LatentModelIntegrationType integralType,
        GaussianCopulaPolicyInitTraits initTraits = default)
        : base(factorWeights, recoveries, modelA, integralType, initTraits)
    {
    }
}
