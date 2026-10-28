// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// A latent model's integration by a Gauss-Hermite rule per dimension: QuantLib's
/// <c>IntegrationBase&lt;GaussianQuadMultidimIntegrator&gt;</c>, scalar and vector.
/// </summary>
[Experimental("NQL0001")]
public sealed class GaussianQuadLMIntegration : LMIntegration
{
    private readonly GaussianQuadMultidimIntegrator integrator;

    /// <summary>Initializes a new instance of the <see cref="GaussianQuadLMIntegration"/> class.</summary>
    /// <param name="dimension">The number of systemic factors, from 1 to 15.</param>
    /// <param name="order">The rule's order per dimension.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is outside [1, 15].</exception>
    public GaussianQuadLMIntegration(int dimension, int order)
    {
        integrator = new GaussianQuadMultidimIntegrator(dimension, order);
    }

    /// <summary>Gets the rule's order.</summary>
    public int Order => integrator.Order;

    /// <inheritdoc/>
    public override double Integrate(MultidimIntegrand f) => integrator.Integrate(f);

    /// <inheritdoc/>
    public override double[] IntegrateV(MultidimVectorIntegrand f) => integrator.IntegrateV(f);
}
