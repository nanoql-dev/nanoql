// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/binomiallossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The binomial loss model on the Student t copula: QuantLib's typedef <c>TBinomialLossModel</c>.</summary>
[Experimental("NQL0001")]
public sealed class TBinomialLossModel : BinomialLossModel<TCopulaPolicy, TCopulaPolicyInitTraits>, IBaseCorrelationModel<TCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="TBinomialLossModel"/> class.</summary>
    /// <param name="copula">The loss latent model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copula"/> is <see langword="null"/>.</exception>
    public TBinomialLossModel(ConstantLossLatentModel<TCopulaPolicy, TCopulaPolicyInitTraits> copula)
        : base(copula)
    {
    }

    /// <summary>The model on a Student t constant-loss latent model of one correlation, its Gaussian-quadrature
    /// integration over one variable per recovery: QuantLib's base-correlation sub-model.</summary>
    /// <param name="correlation">The correlation.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="traits">The copula's initialization parameters.</param>
    /// <returns>The model, unbound.</returns>
    /// <exception cref="ArgumentException">As the latent model's constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As the latent model's constructor.</exception>
    public static IDefaultLossModel ForCorrelation(double correlation, ImmutableArray<double> recoveries, TCopulaPolicyInitTraits traits) =>
        new TBinomialLossModel(new TConstantLossLM(correlation, recoveries, LatentModelIntegrationType.GaussianQuadrature, recoveries.Length, traits));
}
