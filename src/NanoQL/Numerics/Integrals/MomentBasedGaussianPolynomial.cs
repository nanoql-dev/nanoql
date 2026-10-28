// SPDX-FileCopyrightText: Copyright (C) 2020 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/momentbasedgaussianpolynomial.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// An orthogonal polynomial defined by the moments of its distribution: the three-term recurrence's coefficients α
/// and β computed from the moments (Golub and Welsch 1969; Morandi Cecchi and Redivo Zaglia 1993), so that a
/// Gaussian quadrature follows from the moments alone. An extension point: a derived class gives
/// <see cref="Moment"/> and <see cref="GaussianOrthogonalPolynomial.W"/>.
/// </summary>
/// <remarks>
/// QuantLib's template over its number type; only <c>Real</c> is instantiated by its default build, so this is the
/// <c>double</c> instantiation. QuantLib memoises the recursion's values (its <c>b_</c>, <c>c_</c> and <c>z_</c>),
/// without which a 12-node rule makes 511,659,097 moment calls; the memo is kept here, filled lazily (a constructor
/// can't call the derived class's <see cref="Moment"/>) and under a lock, so one polynomial serves several threads,
/// each reading QuantLib's values. A value that is NaN is recomputed at each read, as QuantLib's NaN marks a value
/// not yet computed.
/// </remarks>
public abstract class MomentBasedGaussianPolynomial : GaussianOrthogonalPolynomial
{
    private readonly object sync = new();
    private readonly List<double> b = [];
    private readonly List<double> c = [];
    private readonly List<List<double>> z = [[]];

    /// <summary>The i-th moment of the distribution, its zeroth 1.</summary>
    /// <param name="i">The moment's order, non-negative.</param>
    /// <returns>The moment.</returns>
    public abstract double Moment(int i);

    /// <summary>The zeroth moment, which must be 1.</summary>
    /// <returns>The zeroth moment.</returns>
    /// <exception cref="NanoQLException">The zeroth moment isn't close enough to 1.</exception>
    public override double Mu_0()
    {
        double m0 = Moment(0);
        if (!ComparisonFunctions.CloseEnough(m0, 1.0))
        {
            throw new NanoQLException("zero moment must by one.");
        }

        return Moment(0);
    }

    /// <summary>The recurrence's coefficient αᵢ.</summary>
    /// <param name="i">The index, non-negative.</param>
    /// <returns>The coefficient.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    public override double Alpha(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        lock (sync)
        {
            return AlphaCore(i);
        }
    }

    /// <summary>The recurrence's coefficient βᵢ, β₀ being 1.</summary>
    /// <param name="i">The index, non-negative.</param>
    /// <returns>The coefficient.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    public override double Beta(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        lock (sync)
        {
            return BetaCore(i);
        }
    }

    private static void Resize(List<double> values, int size)
    {
        while (values.Count < size)
        {
            values.Add(double.NaN);
        }
    }

    private double Z(int k, int i)
    {
        if (k == -1)
        {
            return 0.0;
        }

        int rows = z.Count;
        int cols = z[0].Count;

        if (cols <= i)
        {
            for (int l = 0; l < rows; ++l)
            {
                Resize(z[l], i + 1);
            }
        }

        if (rows <= k)
        {
            while (z.Count < k + 1)
            {
                var row = new List<double>(z[0].Count);
                Resize(row, z[0].Count);
                z.Add(row);
            }
        }

        if (double.IsNaN(z[k][i]))
        {
            if (k == 0)
            {
                z[k][i] = Moment(i);
            }
            else
            {
                double tmp = Z(k - 1, i + 1) - (AlphaCore(k - 1) * Z(k - 1, i)) - (BetaCore(k - 1) * Z(k - 2, i));
                z[k][i] = tmp;
            }
        }

        return z[k][i];
    }

    private double AlphaCore(int u)
    {
        if (b.Count <= u)
        {
            Resize(b, u + 1);
        }

        if (double.IsNaN(b[u]))
        {
            if (u == 0)
            {
                b[u] = Moment(1);
            }
            else
            {
                double tmp = (-Z(u - 1, u) / Z(u - 1, u - 1)) + (Z(u, u + 1) / Z(u, u));
                b[u] = tmp;
            }
        }

        return b[u];
    }

    private double BetaCore(int u)
    {
        if (u == 0)
        {
            return 1.0;
        }

        if (c.Count <= u)
        {
            Resize(c, u + 1);
        }

        if (double.IsNaN(c[u]))
        {
            double tmp = Z(u, u) / Z(u - 1, u - 1);
            c[u] = tmp;
        }

        return c[u];
    }
}
