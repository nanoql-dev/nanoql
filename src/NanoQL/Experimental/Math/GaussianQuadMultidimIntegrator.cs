// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/multidimquadrature.hpp ql/experimental/math/multidimquadrature.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// Integrates a scalar or a vector function of a vector over ℝ^d with a Gauss-Hermite rule per dimension, the last
/// dimension outermost.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; QuantLib's integration-variable buffer is a per-call buffer. The dimension runs from 1 to 15;
/// QuantLib's entry points read <c>integrationEntries_[dimension - 1]</c>, so a dimension of 0 or above 15 reads
/// outside its 15 entries (<c>multidimquadrature.hpp:193</c>), which nanoql refuses.
/// </para>
/// <para>
/// The vector integration weights every node: QuantLib's applies its first node's weight inside a
/// <c>std::for_each</c> that discards the result (<c>multidimquadrature.hpp:61-64</c>), so node n − 1 enters
/// unweighted, E[M²] reading 1.0000000116 where the scalar rule reads 0.99999999995. nanoql's equals the scalar rule
/// per component, bit for bit. A documented divergence (<c>Divergences/VectorQuadratureWeightTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class GaussianQuadMultidimIntegrator
{
    private const int MaxDimensions = 15;

    private readonly GaussHermiteIntegration integral;
    private readonly double[] x;
    private readonly double[] w;
    private readonly int dimension;

    /// <summary>Initializes a new instance of the <see cref="GaussianQuadMultidimIntegrator"/> class.</summary>
    /// <param name="dimension">The dimension of the integrand's argument, from 1 to 15.</param>
    /// <param name="quadOrder">The quadrature's order.</param>
    /// <param name="mu">The parameter of the Gauss-Hermite weight.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is outside [1, 15].</exception>
    public GaussianQuadMultidimIntegrator(int dimension, int quadOrder, double mu = 0.0)
    {
        if (dimension is < 1 or > MaxDimensions)
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "the dimension must be from 1 to 15");
        }

        integral = new GaussHermiteIntegration(quadOrder, mu);
        x = [.. integral.X()];
        w = [.. integral.Weights()];
        this.dimension = dimension;
    }

    /// <summary>Gets the quadrature's order.</summary>
    public int Order => integral.Order;

    /// <summary>The integral of a scalar function over ℝ^d.</summary>
    /// <param name="f">The integrand.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public double Integrate(MultidimIntegrand f)
    {
        ArgumentNullException.ThrowIfNull(f);
        var varBuffer = new double[dimension];

        // integration entry level is selected now
        return integral.Evaluate(m => ScalarIntegrator(dimension, f, m, varBuffer));
    }

    /// <summary>The integral of a vector function over ℝ^d, component by component.</summary>
    /// <param name="f">The integrand.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public double[] IntegrateV(MultidimVectorIntegrand f)
    {
        ArgumentNullException.ThrowIfNull(f);
        var varBuffer = new double[dimension];
        return VectorIntegrator(m => VectorIntegratorVR(dimension, f, m, varBuffer));
    }

    private double ScalarIntegrator(int intgDepth, MultidimIntegrand f, double mFctr, double[] varBuffer)
    {
        varBuffer[intgDepth - 1] = mFctr;
        return intgDepth == 1
            ? f(varBuffer)
            : integral.Evaluate(m => ScalarIntegrator(intgDepth - 1, f, m, varBuffer));
    }

    private double[] VectorIntegratorVR(int intgDepth, MultidimVectorIntegrand f, double mFctr, double[] varBuffer)
    {
        varBuffer[intgDepth - 1] = mFctr;
        return intgDepth == 1
            ? f(varBuffer)
            : VectorIntegrator(m => VectorIntegratorVR(intgDepth - 1, f, m, varBuffer));
    }

    // The Gauss-Hermite rule on a vector function, from the last node to the first, as the scalar rule sums.
    private double[] VectorIntegrator(Func<double, double[]> f)
    {
        int i = x.Length - 1;
        double[] term = f(x[i]);
        var sum = new double[term.Length];
        for (int j = 0; j < term.Length; j++)
        {
            sum[j] = w[i] * term[j];
        }

        for (i--; i >= 0; --i)
        {
            term = f(x[i]);

            // sum[j] += term[j] * w_[i];
            for (int j = 0; j < sum.Length; j++)
            {
                sum[j] = (w[i] * term[j]) + sum[j];
            }
        }

        return sum;
    }
}
