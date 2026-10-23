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
/// The simple polynomial fitting method: a discount function d(t) = Σ_{i=0}^{degree} c_i t^i, a simple and crude, but
/// fast and robust, means of fitting a yield curve.
/// </summary>
/// <remarks>
/// Constrained at zero, c_0 is 1 and the parameters are the other coefficients. The powers are the Bernstein
/// polynomials B_{i,i}(t) = t^i (<see cref="BernsteinPolynomial"/>), <see cref="Math.Pow"/>'s as QuantLib's are
/// <c>std::pow</c>'s. QuantLib's constructor without the optimization method is the first constructor with <c>l2</c>
/// named.
/// </remarks>
public sealed class SimplePolynomialFitting : FittedBondDiscountCurveFittingMethod
{
    private readonly int size;

    /// <summary>Initializes a new instance of the <see cref="SimplePolynomialFitting"/> class.</summary>
    /// <param name="degree">The polynomial's degree, QuantLib's <c>Natural</c>.</param>
    /// <param name="constrainAtZero">Whether the discount function is constrained to 1 at t = 0.</param>
    /// <param name="weights">The bonds' weights, or <see langword="null"/> or empty for their inverse durations.</param>
    /// <param name="optimizationMethod">The optimization method, or <see langword="null"/> for a <see cref="Simplex"/>.</param>
    /// <param name="l2">The L2 penalties, one per parameter, or <see langword="null"/> or empty for none.</param>
    /// <param name="minCutoffTime">The time before which the discount extrapolates with a flat forward rate.</param>
    /// <param name="maxCutoffTime">The time after which the discount extrapolates with a flat forward rate.</param>
    /// <param name="constraint">The optimization's constraint, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="degree"/> is negative.</exception>
    public SimplePolynomialFitting(
        int degree,
        bool constrainAtZero = true,
        IEnumerable<double>? weights = null,
        OptimizationMethod? optimizationMethod = null,
        IEnumerable<double>? l2 = null,
        double minCutoffTime = 0.0,
        double maxCutoffTime = double.MaxValue,
        Constraint? constraint = null)
        : base(constrainAtZero, weights, optimizationMethod, l2, minCutoffTime, maxCutoffTime, constraint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        size = constrainAtZero ? degree : degree + 1;
    }

    /// <inheritdoc/>
    /// <remarks>The coefficients, the constant one left out when constrained at zero.</remarks>
    public override int Size => size;

    /// <inheritdoc/>
    protected override double DiscountFunction(ReadOnlySpan<double> x, double t)
    {
        double d = 0.0;
        if (!ConstrainAtZero)
        {
            for (int i = 0; i < size; ++i)
            {
                d += x[i] * BernsteinPolynomial.Get(i, i, t);
            }
        }
        else
        {
            d = 1.0;
            for (int i = 0; i < size; ++i)
            {
                d += x[i] * BernsteinPolynomial.Get(i + 1, i + 1, t);
            }
        }

        return d;
    }
}
