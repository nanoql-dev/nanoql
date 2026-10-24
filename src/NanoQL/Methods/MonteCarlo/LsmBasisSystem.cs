// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/lsmbasissystem.hpp ql/methods/montecarlo/lsmbasissystem.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>The basis systems of the Longstaff-Schwartz early-exercise Monte Carlo regressions.</summary>
/// <remarks>
/// A static class, as QuantLib's static members: each system is an immutable array of functions, the polynomials of
/// orders 0 to the order given, the orthogonal ones weighted by the square root of their weight function.
/// </remarks>
public static class LsmBasisSystem
{
    /// <summary>The one-factor basis: the polynomials of orders 0 to <paramref name="order"/>.</summary>
    /// <param name="order">The highest order.</param>
    /// <param name="type">The polynomial family.</param>
    /// <returns>The basis functions, <paramref name="order"/> + 1 of them.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is negative.</exception>
    /// <exception cref="NanoQLException">The family is unknown.</exception>
    public static ImmutableArray<Func<double, double>> PathBasisSystem(int order, LsmBasisSystemPolynomialType type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ImmutableArray<Func<double, double>>.Builder ret = ImmutableArray.CreateBuilder<Func<double, double>>(order + 1);
        for (int i = 0; i <= order; ++i)
        {
            int n = i;
            switch (type)
            {
                case LsmBasisSystemPolynomialType.Monomial:
                    ret.Add(x => Monomial(n, x));
                    break;
                case LsmBasisSystemPolynomialType.Laguerre:
                {
                    var p = new GaussLaguerrePolynomial();
                    ret.Add(x => p.WeightedValue(n, x));
                }

                break;
                case LsmBasisSystemPolynomialType.Hermite:
                {
                    var p = new GaussHermitePolynomial();
                    ret.Add(x => p.WeightedValue(n, x));
                }

                break;
                case LsmBasisSystemPolynomialType.Hyperbolic:
                {
                    var p = new GaussHyperbolicPolynomial();
                    ret.Add(x => p.WeightedValue(n, x));
                }

                break;
                case LsmBasisSystemPolynomialType.Legendre:
                {
                    var p = new GaussLegendrePolynomial();
                    ret.Add(x => p.WeightedValue(n, x));
                }

                break;
                case LsmBasisSystemPolynomialType.Chebyshev:
                {
                    var p = new GaussChebyshevPolynomial();
                    ret.Add(x => p.WeightedValue(n, x));
                }

                break;
                case LsmBasisSystemPolynomialType.Chebyshev2nd:
                {
                    var p = new GaussChebyshev2ndPolynomial();
                    ret.Add(x => p.WeightedValue(n, x));
                }

                break;
                default:
                    throw new NanoQLException("unknown regression type");
            }
        }

        return ret.MoveToImmutable();
    }

    /// <summary>
    /// The multi-factor basis: the products over the factors of the one-factor polynomials whose orders sum to at most
    /// <paramref name="order"/>, the constant first, then each total order's terms in increasing lexicographic order of
    /// their orders.
    /// </summary>
    /// <param name="dim">The number of factors.</param>
    /// <param name="order">The highest total order.</param>
    /// <param name="type">The polynomial family.</param>
    /// <returns>The basis functions, each of a vector with one element per factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is negative.</exception>
    /// <exception cref="NanoQLException"><paramref name="dim"/> isn't positive ("zero dimension"), or the family is unknown.</exception>
    public static ImmutableArray<Func<Vector, double>> MultiPathBasisSystem(
        int dim, int order, LsmBasisSystemPolynomialType type)
    {
        if (!(dim > 0))
        {
            throw new NanoQLException("zero dimension");
        }

        // get single factor basis
        ImmutableArray<Func<double, double>> pathBasis = PathBasisSystem(order, type);
        ImmutableArray<Func<Vector, double>>.Builder ret = ImmutableArray.CreateBuilder<Func<Vector, double>>();

        // 0-th order term
        var term = new Func<double, double>[dim];
        Array.Fill(term, pathBasis[0]);
        ret.Add(MultiDimFct([.. term]));

        // start with all 0 tuple
        List<int[]> tuples = [new int[dim]];

        // add multi-factor terms
        for (int i = 1; i <= order; ++i)
        {
            tuples = NextOrderTuples(tuples);

            // now we have all tuples of order i
            // for each tuple add the corresponding term
            foreach (int[] tuple in tuples)
            {
                for (int k = 0; k < dim; ++k)
                {
                    term[k] = pathBasis[tuple[k]];
                }

                ret.Add(MultiDimFct([.. term]));
            }
        }

        return ret.ToImmutable();
    }

    // pow(x, order)
    private static double Monomial(int order, double x)
    {
        double ret = 1.0;
        for (int i = 0; i < order; ++i)
        {
            ret *= x;
        }

        return ret;
    }

    // multiplies [Real -> Real] functors
    // to create [Array -> Real] functor
    private static Func<Vector, double> MultiDimFct(ImmutableArray<Func<double, double>> b)
    {
        if (b.IsEmpty)
        {
            throw new NanoQLException("zero size basis");
        }

        return a =>
        {
            double ret = b[0](a[0]);
            for (int i = 1; i < b.Length; ++i)
            {
                ret *= b[i](a[i]);
            }

            return ret;
        };
    }

    // check size and order of tuples
    private static void CheckTuples(List<int[]> v, int dim, int order)
    {
        foreach (int[] i in v)
        {
            if (!(dim == i.Length))
            {
                throw new NanoQLException("wrong tuple size");
            }

            if (!(order == i.Sum()))
            {
                throw new NanoQLException("wrong tuple order");
            }
        }
    }

    // build order N+1 tuples from order N tuples, unique and in increasing lexicographic order, QuantLib's std::set of
    // vectors
    private static List<int[]> NextOrderTuples(List<int[]> v)
    {
        int order = v[0].Sum();
        int dim = v[0].Length;

        CheckTuples(v, dim, order);

        // the set of unique tuples
        var tuples = new SortedSet<int[]>(Comparer<int[]>.Create(Lexicographic));
        for (int i = 0; i < dim; ++i)
        {
            // increase i-th value in every tuple by 1
            foreach (int[] j in v)
            {
                int[] x = [.. j];
                x[i] += 1;
                tuples.Add(x);
            }
        }

        return [.. tuples];
    }

    private static int Lexicographic(int[]? x, int[]? y)
    {
        for (int k = 0; k < Math.Min(x!.Length, y!.Length); k++)
        {
            int c = x[k].CompareTo(y[k]);
            if (c != 0)
            {
                return c;
            }
        }

        return x.Length.CompareTo(y.Length);
    }
}
