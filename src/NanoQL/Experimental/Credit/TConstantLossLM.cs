// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/constantlosslatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The constant-loss latent model on the Student t copula: QuantLib's typedef <c>TConstantLossLM</c>.</summary>
[Experimental("NQL0001")]
public sealed class TConstantLossLM : ConstantLossLatentModel<TCopulaPolicy, TCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="TConstantLossLM"/> class.</summary>
    /// <param name="factorWeights">The weights of each variable on the independent factors.</param>
    /// <param name="recoveries">Each name's recovery.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recoveries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public TConstantLossLM(
        IReadOnlyList<IReadOnlyList<double>> factorWeights,
        IReadOnlyList<double> recoveries,
        LatentModelIntegrationType integralType,
        TCopulaPolicyInitTraits initTraits = default)
        : base(factorWeights, recoveries, integralType, initTraits)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TConstantLossLM"/> class on one factor of a market
    /// correlation.</summary>
    /// <param name="correlation">The correlation, whose square root is every variable's weight.</param>
    /// <param name="recoveries">Each name's recovery.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recoveries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public TConstantLossLM(
        double correlation,
        IReadOnlyList<double> recoveries,
        LatentModelIntegrationType integralType,
        int nVariables,
        TCopulaPolicyInitTraits initTraits = default)
        : base(correlation, recoveries, integralType, nVariables, initTraits)
    {
    }
}
