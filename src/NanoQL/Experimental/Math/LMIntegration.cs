// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A latent model's integration over its systemic factors: QuantLib's <c>LMIntegration</c>, the common base of its
/// two integrator families, so that an integrable model chooses its algorithm apart.
/// </summary>
/// <remarks>Immutable. The two implementations are QuantLib's <c>IntegrationBase</c> specializations.</remarks>
[Experimental("NQL0001")]
public abstract class LMIntegration
{
    /// <summary>The integral of a scalar function.</summary>
    /// <param name="f">The integrand.</param>
    /// <returns>The integral.</returns>
    public abstract double Integrate(MultidimIntegrand f);

    /// <summary>The integral of a vector function, which an integration may not provide.</summary>
    /// <param name="f">The integrand.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="NanoQLException">The integration has no vector form ("No vector integration
    /// provided").</exception>
    public virtual double[] IntegrateV(MultidimVectorIntegrand f) => throw new NanoQLException("No vector integration provided");

    // QuantLib's IntegrationFactory::createLMIntegration: the integration of a type over a number of factors.
    internal static LMIntegration Create(int dimension, LatentModelIntegrationType type)
    {
        EnumArguments.Check(type, nameof(type));
        switch (type)
        {
            case LatentModelIntegrationType.GaussianQuadrature:
                return new GaussianQuadLMIntegration(dimension, 25);
            default:
                {
                    var integrals = new List<Integrator>(dimension);
                    for (int i = 0; i < dimension; i++)
                    {
                        integrals.Add(new TrapezoidIntegral<Default>(1.0e-4, 20));
                    }

                    /* This integration domain is tailored for the T
                    distribution; it is too wide for normals or Ts of high
                    order.
                    */
                    return new MultidimLMIntegration(integrals, -35.0, 35.0);
                }
        }
    }
}
