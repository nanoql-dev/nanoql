// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/recursivelossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The recursive loss model on the Gaussian copula: QuantLib's typedef <c>RecursiveGaussLossModel</c>.</summary>
[Experimental("NQL0001")]
public sealed class RecursiveGaussLossModel : RecursiveLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="RecursiveGaussLossModel"/> class.</summary>
    /// <param name="m">The constant-loss latent model.</param>
    /// <param name="nbuckets">The number of loss units in the smallest loss given default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    public RecursiveGaussLossModel(ConstantLossLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> m, int nbuckets = 1)
        : base(m, nbuckets)
    {
    }
}
