// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The tensor product of one-dimensional Gaussian quadratures: a quadrature of the given order in each dimension,
/// each order's built once, and the weighted sum of a function at the product's nodes.
/// </summary>
/// <remarks>
/// No dimension is refused: QuantLib writes the first element of an empty vector of spacings
/// (<c>gaussianquadratures.cpp:101-102</c>), undefined behaviour; a documented divergence.
/// </remarks>
public sealed class MultiDimGaussianIntegration
{
    private readonly Vector weights_;
    private readonly ImmutableArray<Vector> x_;

    /// <summary>Initializes a new instance of the <see cref="MultiDimGaussianIntegration"/> class.</summary>
    /// <param name="ns">The order in each dimension.</param>
    /// <param name="genQuad">The quadrature of a given order.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ns"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An order is negative.</exception>
    public MultiDimGaussianIntegration(IEnumerable<int> ns, Func<int, GaussianQuadrature> genQuad)
    {
        ArgumentNullException.ThrowIfNull(ns);
        ArgumentNullException.ThrowIfNull(genQuad);
        int[] orders = [.. ns];
        foreach (int order in orders)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(order, nameof(ns));
        }

        int size = 1;
        foreach (int order in orders)
        {
            size = checked(size * order);
        }

        weights_ = new Vector(size, 1.0);
        var x = new Vector[size];

        int m = orders.Length;
        int n = size;

        // QuantLib's spacing[0] = 1 on an empty vector is undefined
        if (m == 0)
        {
            throw new ArgumentException("no dimension given", nameof(ns));
        }

        for (int i = 0; i < n; i++)
        {
            x[i] = new Vector(m);
        }

        int[] spacing = new int[m];
        spacing[0] = 1;
        for (int j = 1; j < m; j++)
        {
            spacing[j] = checked(spacing[j - 1] * orders[j - 1]);
        }

        var n2weights = new SortedDictionary<int, Vector>();
        var n2x = new SortedDictionary<int, Vector>();
        foreach (int order in orders)
        {
            if (!n2x.ContainsKey(order))
            {
                GaussianQuadrature quad = genQuad(order)
                    ?? throw new ArgumentException("the quadrature generator returned null", nameof(genQuad));
                n2x[order] = quad.X();
                n2weights[order] = quad.Weights();
            }
        }

        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < m; ++j)
            {
                int order = orders[j];
                int nx = (i / spacing[j]) % orders[j];
                weights_[i] *= n2weights[order][nx];
                x[i][j] = n2x[order][nx];
            }
        }

        x_ = [.. x];
    }

    /// <summary>The weighted sum of <paramref name="f"/> at the nodes.</summary>
    /// <param name="f">The function, given a copy of each node.</param>
    /// <returns>Σ wᵢ·f(xᵢ).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public double Evaluate(Func<Vector, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        double s = 0.0;
        int n = x_.Length;
        for (int i = 0; i < n; ++i)
        {
            s += weights_[i] * f(x_[i].Clone());
        }

        return s;
    }

    /// <summary>The weights.</summary>
    /// <returns>A copy of the weights.</returns>
    public Vector Weights() => weights_.Clone();

    /// <summary>The nodes.</summary>
    /// <returns>A copy of the nodes.</returns>
    public ImmutableArray<Vector> X() => [.. x_.Select(v => v.Clone())];
}
