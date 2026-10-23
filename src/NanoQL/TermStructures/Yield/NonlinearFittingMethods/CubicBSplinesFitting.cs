// SPDX-FileCopyrightText: Copyright (C) 2007 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2010 Alessandro Roveda
// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/nonlinearfittingmethods.hpp ql/termstructures/yield/nonlinearfittingmethods.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The cubic B-splines fitting method: a discount function d(t) = Σ_{i=0}^{n} c_i N_{i,3}(t) over a set of cubic
/// B-splines.
/// </summary>
/// <remarks>
/// <para>
/// See McCulloch, J. 1971, "Measuring the Term Structure of Interest Rates." Journal of Business, 44: 19-31, and
/// McCulloch, J. 1975, "The tax adjusted yield curve." Journal of Finance, XXX811-30. "The results are extremely
/// sensitive to the number and location of the knot points, and there is no optimal way of selecting them." James, J.
/// and N. Webber, "Interest Rate Modelling", John Wiley, 2000, pp. 440.
/// </para>
/// <para>
/// Constrained at zero, the coefficient of the second basis function, N_{1,3}, is solved for so that d(0) = 1; a small
/// but nonzero N_{1,3}(0) may make the problem ill-conditioned. QuantLib's constructor without the optimization method
/// is the first constructor with <c>l2</c> named. The B-splines read 0 over a zero knot span (<see cref="BSpline"/>), so
/// a repeated knot fits where QuantLib's divides by zero and throws.
/// </para>
/// </remarks>
public sealed class CubicBSplinesFitting : FittedBondDiscountCurveFittingMethod
{
    private readonly BSpline splines;
    private readonly int size;

    // N_th basis function coefficient to solve for when d(0)=1
    private readonly int N;

    /// <summary>Initializes a new instance of the <see cref="CubicBSplinesFitting"/> class.</summary>
    /// <param name="knotVector">The knots, at least 8, nondecreasing.</param>
    /// <param name="constrainAtZero">Whether the discount function is constrained to 1 at t = 0.</param>
    /// <param name="weights">The bonds' weights, or <see langword="null"/> or empty for their inverse durations.</param>
    /// <param name="optimizationMethod">The optimization method, or <see langword="null"/> for a <see cref="Simplex"/>.</param>
    /// <param name="l2">The L2 penalties, one per parameter, or <see langword="null"/> or empty for none.</param>
    /// <param name="minCutoffTime">The time before which the discount extrapolates with a flat forward rate.</param>
    /// <param name="maxCutoffTime">The time after which the discount extrapolates with a flat forward rate.</param>
    /// <param name="constraint">The optimization's constraint, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="knotVector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The B-splines refuse the knots, there are fewer than 8 knots, or, constrained at zero, the second basis function
    /// is zero at t = 0.
    /// </exception>
    public CubicBSplinesFitting(
        IEnumerable<double> knotVector,
        bool constrainAtZero = true,
        IEnumerable<double>? weights = null,
        OptimizationMethod? optimizationMethod = null,
        IEnumerable<double>? l2 = null,
        double minCutoffTime = 0.0,
        double maxCutoffTime = double.MaxValue,
        Constraint? constraint = null)
        : base(constrainAtZero, weights, optimizationMethod, l2, minCutoffTime, maxCutoffTime, constraint)
    {
        ArgumentNullException.ThrowIfNull(knotVector);
        double[] knots = [.. knotVector];
        splines = new BSpline(3, knots.Length - 5, knots);
        if (!(knots.Length >= 8))
        {
            throw new ArgumentException("At least 8 knots are required", nameof(knotVector));
        }

        int basisFunctions = knots.Length - 4;
        if (constrainAtZero)
        {
            size = basisFunctions - 1;

            // Note: A small but nonzero N_th basis function at t=0 may lead to an ill conditioned problem
            N = 1;
            if (!(Math.Abs(splines.Evaluate(N, 0.0)) > NumericLimits.QL_EPSILON))
            {
                throw new ArgumentException("N_th cubic B-spline must be nonzero at t=0", nameof(knotVector));
            }
        }
        else
        {
            size = basisFunctions;
            N = 0;
        }
    }

    /// <inheritdoc/>
    /// <remarks>The basis functions' coefficients, one fewer when constrained at zero.</remarks>
    public override int Size => size;

    /// <summary>The i-th cubic B-spline basis function at a time.</summary>
    /// <param name="i">The basis function's index.</param>
    /// <param name="t">The time.</param>
    /// <returns>N_{i,3}(t).</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or past the last basis function.</exception>
    public double BasisFunction(int i, double t) => splines.Evaluate(i, t);

    /// <inheritdoc/>
    protected override double DiscountFunction(ReadOnlySpan<double> x, double t)
    {
        double d = 0.0;
        if (!ConstrainAtZero)
        {
            for (int i = 0; i < size; ++i)
            {
                d += x[i] * splines.Evaluate(i, t);
            }
        }
        else
        {
            const double T = 0.0;
            double sum = 0.0;
            for (int i = 0; i < size; ++i)
            {
                if (i < N)
                {
                    d += x[i] * splines.Evaluate(i, t);
                    sum += x[i] * splines.Evaluate(i, T);
                }
                else
                {
                    d += x[i] * splines.Evaluate(i + 1, t);
                    sum += x[i] * splines.Evaluate(i + 1, T);
                }
            }

            double coeff = 1.0 - sum;
            coeff /= splines.Evaluate(N, T);
            d += coeff * splines.Evaluate(N, t);
        }

        return d;
    }
}
