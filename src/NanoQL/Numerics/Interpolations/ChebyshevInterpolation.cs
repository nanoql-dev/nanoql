// SPDX-FileCopyrightText: Copyright (C) 2021 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/chebyshevinterpolation.hpp ql/math/interpolations/chebyshevinterpolation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Chebyshev interpolation on [−1, 1]: the Lagrange interpolation of values at the Chebyshev nodes (S. A. Sarra,
/// "Chebyshev Interpolation: An Interactive Tour").
/// </summary>
/// <remarks>
/// Immutable, as every <see cref="Interpolation"/>: QuantLib's <c>updateY</c>, which overwrites the values in place, is
/// <see cref="WithY"/>, a copy on the same nodes sharing the receiver's barycentric weights, which depend on the nodes
/// alone. The value and derivative are <see cref="LagrangeInterpolation"/>'s, and so are the refusals of the primitive
/// and the second derivative.
/// </remarks>
public sealed class ChebyshevInterpolation : Interpolation
{
    private readonly LagrangeInterpolation impl;
    private readonly ChebyshevInterpolationPointsType pointsType;

    /// <summary>Initializes a new instance of the <see cref="ChebyshevInterpolation"/> class from the values at the
    /// nodes.</summary>
    /// <param name="y">The values at the nodes, one per node; at least 2.</param>
    /// <param name="pointsType">The nodes' kind.</param>
    /// <exception cref="ArgumentException">There are fewer than 2 values.</exception>
    public ChebyshevInterpolation(
        ReadOnlySpan<double> y, ChebyshevInterpolationPointsType pointsType = ChebyshevInterpolationPointsType.SecondKind)
        : base(NodesOf(y.Length, pointsType), y, 2)
    {
        this.pointsType = pointsType;
        impl = new LagrangeInterpolation(X, Y);
    }

    /// <summary>Initializes a new instance of the <see cref="ChebyshevInterpolation"/> class from a function sampled at
    /// the nodes.</summary>
    /// <param name="n">The number of nodes; at least 2.</param>
    /// <param name="f">The function.</param>
    /// <param name="pointsType">The nodes' kind.</param>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is below 2.</exception>
    public ChebyshevInterpolation(
        int n, Func<double, double> f, ChebyshevInterpolationPointsType pointsType = ChebyshevInterpolationPointsType.SecondKind)
        : this(Apply(NodesOf(n, pointsType), f), pointsType)
    {
    }

    private ChebyshevInterpolation(
        ReadOnlySpan<double> x, ReadOnlySpan<double> y, ChebyshevInterpolationPointsType pointsType, LagrangeInterpolation impl)
        : base(x, y, 2)
    {
        this.pointsType = pointsType;
        this.impl = impl;
    }

    /// <summary>The Chebyshev nodes of a kind, in increasing order.</summary>
    /// <param name="n">The number of nodes.</param>
    /// <param name="pointsType">The nodes' kind.</param>
    /// <returns>The nodes; of the second kind, a single node is NaN, as QuantLib's 0/0.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public static Vector Nodes(int n, ChebyshevInterpolationPointsType pointsType) => new(NodesOf(n, pointsType));

    /// <summary>The interpolation's nodes.</summary>
    /// <returns>The nodes.</returns>
    public Vector Nodes() => new(X);

    /// <summary>
    /// The interpolation of other values at the same nodes: QuantLib's <c>updateY</c>, a copy sharing the barycentric
    /// weights, the receiver unchanged.
    /// </summary>
    /// <param name="y">The values at the nodes, one per node.</param>
    /// <returns>The interpolation of the new values.</returns>
    /// <exception cref="ArgumentException">The number of values isn't the number of nodes.</exception>
    public ChebyshevInterpolation WithY(ReadOnlySpan<double> y)
    {
        if (!(y.Length == Y.Length))
        {
            throw new ArgumentException("interpolation override has the wrong length", nameof(y));
        }

        return new ChebyshevInterpolation(X, y, pointsType, impl.WithY(y));
    }

    // The Lagrange interpolation's barycentric weights, which WithY shares.
    internal ReadOnlySpan<double> Weights() => impl.Weights();

    /// <inheritdoc/>
    protected override double ValueCore(double x) => impl.Evaluate(x, true);

    /// <inheritdoc/>
    protected override double DerivativeCore(double x) => impl.Derivative(x, true);

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x) => impl.Primitive(x, true);

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) => impl.SecondDerivative(x, true);

    private static double[] NodesOf(int n, ChebyshevInterpolationPointsType pointsType)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        var t = new double[n];
        switch (pointsType)
        {
            case ChebyshevInterpolationPointsType.FirstKind:
                for (int i = 0; i < n; ++i)
                {
                    t[i] = -Math.Cos((i + 0.5) * Math.PI / n);
                }

                break;
            case ChebyshevInterpolationPointsType.SecondKind:
                for (int i = 0; i < n; ++i)
                {
                    t[i] = -Math.Cos(i * Math.PI / (n - 1));
                }

                break;
            default:
                throw new NanoQLException("unknonw Chebyshev interpolation points type");
        }

        return t;
    }

    private static double[] Apply(double[] x, Func<double, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        var t = new double[x.Length];
        for (int i = 0; i < x.Length; ++i)
        {
            t[i] = f(x[i]);
        }

        return t;
    }
}
