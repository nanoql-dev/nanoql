// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/lagrangeinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Barycentric Lagrange interpolation (J.-P. Berrut and L. N. Trefethen, "Barycentric Lagrange interpolation", SIAM
/// Review 46(3), 2004): the polynomial through every point, evaluated by the barycentric formula.
/// </summary>
/// <remarks>
/// <para>
/// It copies its inputs, as every <see cref="Interpolation"/> does; its barycentric weights depend on the x values
/// alone. The x values must not repeat, which QuantLib checks only under its extra safety checks.
/// </para>
/// <para>
/// At a node the value is the node's y. QuantLib finds the node by a binary search for the first x not below
/// x − 10ε|x| and returns its y when that x is less than 10ε|x| above (<c>lagrangeinterpolation.hpp:114-120</c>): at
/// x = 0 the band is empty, so a node at zero falls through to the barycentric sum, which divides by zero, and on
/// descending x values (QuantLib's own Chebyshev-points test gives them) the search never lands on a node, so every
/// node's value is NaN. Here the band is closed and the nodes are scanned in order: on ascending x values the node is
/// QuantLib's, and the value off the nodes is QuantLib's on any order. Documented divergences (the S-van plan's VR4
/// (xxviii) and the run's own find, <c>Divergences/LagrangeZeroNodeTests.cs</c>).
/// </para>
/// </remarks>
public sealed class LagrangeInterpolation : Interpolation
{
    private readonly double[] lambda;

    /// <summary>Initializes a new instance of the <see cref="LagrangeInterpolation"/> class.</summary>
    /// <param name="x">The x values, distinct; at least 2.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there are fewer than 2 points.</exception>
    public LagrangeInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(x, y, 2)
    {
        ReadOnlySpan<double> xs = X;
        int n = xs.Length;
        lambda = new double[n];

        // QuantLib's update().
        double cM1 = 4.0 / (xs[n - 1] - xs[0]);
        for (int i = 0; i < n; ++i)
        {
            lambda[i] = 1.0;

            double xI = xs[i];
            for (int j = 0; j < n; ++j)
            {
                if (i != j)
                {
                    lambda[i] *= cM1 * (xI - xs[j]);
                }
            }

            lambda[i] = 1.0 / lambda[i];
        }
    }

    private LagrangeInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y, double[] lambda)
        : base(x, y, 2) => this.lambda = lambda;

    /// <summary>
    /// The interpolation of other y values on the same x values, at <paramref name="x"/>: QuantLib's
    /// <c>value(y, x)</c>, with no range check.
    /// </summary>
    /// <param name="y">The y values, one per x value; QuantLib reads the first n.</param>
    /// <param name="x">The point.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException">There are fewer y values than x values, where QuantLib reads past
    /// them.</exception>
    public double Value(ReadOnlySpan<double> y, double x)
    {
        if (!(y.Length >= lambda.Length))
        {
            throw new ArgumentException($"{y.Length} y values given for {lambda.Length} x values", nameof(y));
        }

        return ValueOf(y, x);
    }

    // The same x values and weights with other y values: ChebyshevInterpolation's WithY.
    internal LagrangeInterpolation WithY(ReadOnlySpan<double> y) => new(X, y, lambda);

    // The barycentric weights, which WithY shares.
    internal ReadOnlySpan<double> Weights() => lambda;

    /// <inheritdoc/>
    protected override double ValueCore(double x) => ValueOf(Y, x);

    /// <inheritdoc/>
    protected override double DerivativeCore(double x)
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        int n = lambda.Length;
        double nn = 0.0, d = 0.0, nd = 0.0, dd = 0.0;
        for (int i = 0; i < n; ++i)
        {
            double xI = xs[i];

            if (ComparisonFunctions.CloseEnough(x, xI))
            {
                double p = 0.0;
                for (int j = 0; j < n; ++j)
                {
                    if (i != j)
                    {
                        p += lambda[j] / (x - xs[j]) * (ys[j] - ys[i]);
                    }
                }

                return p / lambda[i];
            }

            double alpha = lambda[i] / (x - xI);
            double alphad = -alpha / (x - xI);
            nn += alpha * ys[i];
            d += alpha;
            nd += alphad * ys[i];
            dd += alphad;
        }

        return ((nd * d) - (nn * dd)) / (d * d);
    }

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x) =>
        throw new NanoQLException("LagrangeInterpolation primitive is not implemented");

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) =>
        throw new NanoQLException("LagrangeInterpolation secondDerivative is not implemented");

    private double ValueOf(ReadOnlySpan<double> y, double x)
    {
        ReadOnlySpan<double> xs = X;
        int n = lambda.Length;
        double eps = 10 * NumericLimits.QL_EPSILON * Math.Abs(x);

        // QuantLib's lower_bound of x − eps and its check below x + eps, closed and in storage order (VR4 (xxviii))
        for (int i = 0; i < n; ++i)
        {
            if (!(xs[i] < x - eps) && xs[i] - x <= eps)
            {
                return y[i];
            }
        }

        double nn = 0.0, d = 0.0;
        for (int i = 0; i < n; ++i)
        {
            double alpha = lambda[i] / (x - xs[i]);
            nn += alpha * y[i];
            d += alpha;
        }

        return nn / d;
    }
}
