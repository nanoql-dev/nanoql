// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultprobabilitylatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The default-event latent model on the Gaussian copula: QuantLib's typedef <c>GaussianDefProbLM</c>.</summary>
[Experimental("NQL0001")]
public sealed class GaussianDefProbLM : DefaultLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="GaussianDefProbLM"/> class.</summary>
    /// <param name="factorWeights">The weights of each variable on the independent factors.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public GaussianDefProbLM(
        IReadOnlyList<IReadOnlyList<double>> factorWeights,
        LatentModelIntegrationType integralType,
        GaussianCopulaPolicyInitTraits initTraits = default)
        : base(factorWeights, integralType, initTraits)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GaussianDefProbLM"/> class on one factor of a market
    /// correlation.</summary>
    /// <param name="correlation">The correlation, whose square root is every variable's weight.</param>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public GaussianDefProbLM(
        double correlation,
        int nVariables,
        LatentModelIntegrationType integralType,
        GaussianCopulaPolicyInitTraits initTraits = default)
        : base(correlation, nVariables, integralType, initTraits)
    {
    }
}
