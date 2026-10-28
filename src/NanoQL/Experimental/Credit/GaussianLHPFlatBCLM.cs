// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/basecorrelationlossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The vanilla base correlation model: the Gaussian LHP base model on a bilinear surface, QuantLib's typedef
/// <c>GaussianLHPFlatBCLM</c>.</summary>
[Experimental("NQL0001")]
public sealed class GaussianLHPFlatBCLM : BaseCorrelationLossModel<GaussianLHPLossModel, Bilinear, GaussianCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="GaussianLHPFlatBCLM"/> class.</summary>
    /// <param name="correlTS">The base correlation surface.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="traits">The Gaussian copula's parameters, none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="correlTS"/> or <paramref name="recoveries"/> is
    /// <see langword="null"/>.</exception>
    public GaussianLHPFlatBCLM(
        BaseCorrelationTermStructure<Bilinear> correlTS, IReadOnlyList<double> recoveries, GaussianCopulaPolicyInitTraits traits = default)
        : base(correlTS, recoveries, traits)
    {
    }
}
