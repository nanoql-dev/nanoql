// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/inhomogeneouspooldef.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The inhomogeneous pool loss model on the Gaussian copula: QuantLib's typedef <c>IHGaussPoolLossModel</c>.</summary>
[Experimental("NQL0001")]
public sealed class IHGaussPoolLossModel
    : InhomogeneousPoolLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>, IBaseCorrelationModel<GaussianCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="IHGaussPoolLossModel"/> class.</summary>
    /// <param name="copula">The one-factor constant-loss latent model.</param>
    /// <param name="nBuckets">The number of buckets.</param>
    /// <param name="max">The factor grid's upper end.</param>
    /// <param name="min">The factor grid's lower end.</param>
    /// <param name="nSteps">The number of factor steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copula"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The latent model has more than one factor.</exception>
    public IHGaussPoolLossModel(
        ConstantLossLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> copula, int nBuckets, double max = 5.0, double min = -5.0, int nSteps = 50)
        : base(copula, nBuckets, max, min, nSteps)
    {
    }

    /// <summary>The model on a Gaussian constant-loss latent model of one correlation, its Gaussian-quadrature
    /// integration over one variable per recovery, in 500 buckets: QuantLib's base-correlation sub-model, whose own
    /// <c>\todo</c> notes it fails on equity tranches ("the base model works all right, its the link here") and asks
    /// for the bucket count as a parameter.</summary>
    /// <param name="correlation">The correlation.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="traits">The Gaussian copula's parameters, none.</param>
    /// <returns>The model, unbound.</returns>
    /// <exception cref="ArgumentException">As the latent model's constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As the latent model's constructor.</exception>
    public static IDefaultLossModel ForCorrelation(double correlation, ImmutableArray<double> recoveries, GaussianCopulaPolicyInitTraits traits) =>
        new IHGaussPoolLossModel(
            new GaussianConstantLossLM(correlation, recoveries, LatentModelIntegrationType.GaussianQuadrature, recoveries.Length, traits), 500);
}
