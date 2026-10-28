// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/inhomogeneouspooldef.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The inhomogeneous pool loss model on the Student t copula: QuantLib's typedef <c>IHStudentPoolLossModel</c>.</summary>
[Experimental("NQL0001")]
public sealed class IHStudentPoolLossModel : InhomogeneousPoolLossModel<TCopulaPolicy, TCopulaPolicyInitTraits>
{
    /// <summary>Initializes a new instance of the <see cref="IHStudentPoolLossModel"/> class.</summary>
    /// <param name="copula">The one-factor constant-loss latent model.</param>
    /// <param name="nBuckets">The number of buckets.</param>
    /// <param name="max">The factor grid's upper end.</param>
    /// <param name="min">The factor grid's lower end.</param>
    /// <param name="nSteps">The number of factor steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copula"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The latent model has more than one factor.</exception>
    public IHStudentPoolLossModel(
        ConstantLossLatentModel<TCopulaPolicy, TCopulaPolicyInitTraits> copula, int nBuckets, double max = 5.0, double min = -5.0, int nSteps = 50)
        : base(copula, nBuckets, max, min, nSteps)
    {
    }
}
