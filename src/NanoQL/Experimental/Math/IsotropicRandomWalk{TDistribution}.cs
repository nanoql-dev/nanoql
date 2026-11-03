// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/isotropicrandomwalk.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// An isotropic random walk: a step whose radius is drawn from a distribution and whose direction on the
/// d-dimensional sphere is uniform, every direction equally likely: QuantLib's <c>IsotropicRandomWalk</c>.
/// </summary>
/// <remarks>
/// <para>
/// The radius is drawn on the given engine, the direction on the walk's own Mersenne Twister, as QuantLib's. In one
/// dimension the sign is a fair coin and in two the angle is uniform on the circle, QuantLib's forms and bits. From
/// three dimensions QuantLib's hyperspherical angles are each uniform in [0, π], where a uniform direction needs
/// densities ∝ sin^(d−k−1)φ, so its draws crowd the poles; nanoql's direction is Muller's (1959), d standard normals
/// from the walk's generator through the inverse cumulative normal, scaled to unit length, the radius and weights then
/// QuantLib's (<c>Divergences/IsotropicRandomWalkTests.cs</c>).
/// </para>
/// <para>
/// A working type, as the generators are: each draw and each <c>SetDimension</c> changes it, and it is not
/// thread-safe. The seed is required: QuantLib's default, 0, seeds the twister from the clock.
/// </para>
/// </remarks>
/// <typeparam name="TDistribution">The radius's distribution.</typeparam>
[Experimental("NQL0001")]
[WorkingType]
public sealed class IsotropicRandomWalk<TDistribution>
    where TDistribution : class, IRandomWalkDistribution
{
    private readonly MersenneTwisterUniformRng engine;
    private readonly IRandomWalkDistribution distribution;
    private readonly MersenneTwisterUniformRng rng;
    private Vector weights;
    private int dim;

    /// <summary>Initializes a new instance of the <see cref="IsotropicRandomWalk{TDistribution}"/> class.</summary>
    /// <param name="engine">The radius's engine, which it copies.</param>
    /// <param name="distribution">The radius's distribution, which it copies.</param>
    /// <param name="dim">The dimension.</param>
    /// <param name="weights">Each dimension's weight; <see langword="null"/> or empty for 1 each.</param>
    /// <param name="seed">The direction's twister's seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException">The weights are not one per dimension, with QuantLib's text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public IsotropicRandomWalk(
        MersenneTwisterUniformRng engine, TDistribution distribution, int dim, Vector? weights, long seed)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(distribution);
        ArgumentOutOfRangeException.ThrowIfNegative(dim);
        this.engine = engine.Clone();
        this.distribution = distribution.Clone();
        rng = new MersenneTwisterUniformRng(seed);
        this.dim = dim;
        if (weights is null || weights.Empty)
        {
            this.weights = new Vector(dim, 1.0);
        }
        else
        {
            if (dim != weights.Size)
            {
                throw new ArgumentException("Invalid weights", nameof(weights));
            }

            this.weights = weights.Clone();
        }
    }

    private IsotropicRandomWalk(IsotropicRandomWalk<TDistribution> other)
    {
        engine = other.engine.Clone();
        distribution = other.distribution.Clone();
        rng = other.rng.Clone();
        weights = other.weights.Clone();
        dim = other.dim;
    }

    /// <summary>Gets the dimension.</summary>
    public int Dimension => dim;

    /// <summary>Draws the next step into the first <see cref="Dimension"/> elements: QuantLib's <c>nextReal</c>.</summary>
    /// <param name="first">The step's elements.</param>
    /// <exception cref="ArgumentException"><paramref name="first"/> is shorter than the dimension.</exception>
    public void NextReal(Span<double> first)
    {
        if (first.Length < Math.Max(dim, 1))
        {
            throw new ArgumentException($"a step of dimension {dim} needs as many elements, got {first.Length}", nameof(first));
        }

        double radius = distribution.Next(engine);
        if (dim > 2)
        {
            // Muller's direction: d standard normals scaled to unit length
            var z = new double[dim];
            double norm = 0.0;
            for (int i = 0; i < dim; i++)
            {
                z[i] = InverseCumulativeNormal.StandardValue(rng.NextReal());
                norm += z[i] * z[i];
            }

            norm = Math.Sqrt(norm);
            for (int i = 0; i < dim; i++)
            {
                first[i] = radius * (z[i] / norm) * weights[i];
            }
        }
        else if (dim == 2)
        {
            // isotropic random direction on the circle
            double phi = Math.PI * rng.NextReal();
            first[0] = radius * Math.Cos(2.0 * phi) * weights[0];
            first[1] = radius * Math.Sin(2.0 * phi) * weights[1];
        }
        else
        {
            if (rng.NextReal() < 0.5)
            {
                first[0] = -radius * weights[0];
            }
            else
            {
                first[0] = radius * weights[0];
            }
        }
    }

    /// <summary>Sets the dimension, each weight 1: QuantLib's <c>setDimension(Size)</c>.</summary>
    /// <param name="dim">The dimension.</param>
    public void SetDimension(int dim)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dim);
        this.dim = dim;
        weights = new Vector(dim, 1.0);
    }

    /// <summary>Sets the dimension and the weights: QuantLib's <c>setDimension(Size, const Array&amp;)</c>.</summary>
    /// <param name="dim">The dimension.</param>
    /// <param name="weights">Each dimension's weight.</param>
    /// <exception cref="ArgumentException">The weights are not one per dimension, with QuantLib's text.</exception>
    public void SetDimension(int dim, Vector weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (dim != weights.Size)
        {
            throw new ArgumentException("Invalid weights", nameof(weights));
        }

        this.dim = dim;
        this.weights = weights.Clone();
    }

    /// <summary>
    /// Sets the dimension, each weight its bound's width over the largest width, so the sphere becomes an ellipsoid:
    /// QuantLib's <c>setDimension(Size, const Array&amp;, const Array&amp;)</c>. The walk doesn't keep its draws within
    /// the bounds.
    /// </summary>
    /// <param name="dim">The dimension.</param>
    /// <param name="lowerBound">The lower bounds.</param>
    /// <param name="upperBound">The upper bounds.</param>
    /// <exception cref="ArgumentException">A bound is not one per dimension, with QuantLib's texts.</exception>
    public void SetDimension(int dim, Vector lowerBound, Vector upperBound)
    {
        ArgumentNullException.ThrowIfNull(lowerBound);
        ArgumentNullException.ThrowIfNull(upperBound);
        if (dim != lowerBound.Size)
        {
            throw new ArgumentException("Incompatible dimension and lower bound", nameof(lowerBound));
        }

        if (dim != upperBound.Size)
        {
            throw new ArgumentException("Incompatible dimension and upper bound", nameof(upperBound));
        }

        // find the largest bound
        Vector bounds = upperBound - lowerBound;
        double maxBound = bounds[0];
        for (int j = 1; j < dim; j++)
        {
            if (bounds[j] > maxBound)
            {
                maxBound = bounds[j];
            }
        }

        // each dimension's weight is its bound's size divided by the largest bound
        maxBound = 1.0 / maxBound;
        bounds *= maxBound;
        SetDimension(dim, bounds);
    }

    /// <summary>Copies the walk deeply: the copy's next step equals this walk's.</summary>
    /// <returns>The copy.</returns>
    public IsotropicRandomWalk<TDistribution> Clone() => new(this);
}
