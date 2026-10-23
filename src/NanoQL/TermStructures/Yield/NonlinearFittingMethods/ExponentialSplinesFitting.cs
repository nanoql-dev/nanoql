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
/// The exponential-splines fitting method: a discount function d(t) = Σ_{i=1}^{9} c_i e^(−κ i t), the constants c_i
/// and κ determined by the fit.
/// </summary>
/// <remarks>
/// <para>
/// See Li, B., E. DeWetering, G. Lucas, R. Brenner and A. Shapiro (2001): "Merrill Lynch Exponential Spline Model."
/// Merrill Lynch Working Paper. κ can be given a fixed value, which takes it out of the optimization. Convergence may
/// be slow. Constrained at zero, the first coefficient is 1 less the others' sum, so d(0) = 1. QuantLib's constructor
/// without the optimization method is the first constructor with <c>l2</c> named.
/// </para>
/// <para>
/// With κ fixed, QuantLib's discount function sums one term fewer than the coefficients it fits
/// (<c>nonlinearfittingmethods.cpp:84</c>, <c>:91</c>), right when the last parameter is κ: the last coefficient is
/// dead. Here every coefficient is summed when κ is fixed, the discount function of the method with κ free at the same
/// coefficients and κ; with κ free it is QuantLib's. A documented divergence.
/// </para>
/// </remarks>
public sealed class ExponentialSplinesFitting : FittedBondDiscountCurveFittingMethod
{
    private readonly int numCoeffs;
    private readonly double? fixedKappa;

    /// <summary>Initializes a new instance of the <see cref="ExponentialSplinesFitting"/> class.</summary>
    /// <param name="constrainAtZero">Whether the discount function is constrained to 1 at t = 0.</param>
    /// <param name="weights">The bonds' weights, or <see langword="null"/> or empty for their inverse durations.</param>
    /// <param name="optimizationMethod">The optimization method, or <see langword="null"/> for a <see cref="Simplex"/>.</param>
    /// <param name="l2">The L2 penalties, one per parameter, or <see langword="null"/> or empty for none.</param>
    /// <param name="minCutoffTime">The time before which the discount extrapolates with a flat forward rate.</param>
    /// <param name="maxCutoffTime">The time after which the discount extrapolates with a flat forward rate.</param>
    /// <param name="numCoeffs">The number of coefficients, 9 in the method's paper.</param>
    /// <param name="fixedKappa">κ, fixed, or <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>) to fit it.</param>
    /// <param name="constraint">The optimization's constraint, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The method has no unconstrained coefficient to fit.</exception>
    public ExponentialSplinesFitting(
        bool constrainAtZero = true,
        IEnumerable<double>? weights = null,
        OptimizationMethod? optimizationMethod = null,
        IEnumerable<double>? l2 = null,
        double minCutoffTime = 0.0,
        double maxCutoffTime = double.MaxValue,
        int numCoeffs = 9,
        double? fixedKappa = null,
        Constraint? constraint = null)
        : base(constrainAtZero, weights, optimizationMethod, l2, minCutoffTime, maxCutoffTime, constraint)
    {
        this.numCoeffs = numCoeffs;
        this.fixedKappa = fixedKappa;
        if (!(Size > 0))
        {
            throw new ArgumentException("At least 1 unconstrained coefficient required", nameof(numCoeffs));
        }
    }

    /// <summary>Initializes a new instance of the <see cref="ExponentialSplinesFitting"/> class with a fixed κ.</summary>
    /// <param name="constrainAtZero">Whether the discount function is constrained to 1 at t = 0.</param>
    /// <param name="numCoeffs">The number of coefficients.</param>
    /// <param name="fixedKappa">κ, fixed.</param>
    /// <param name="weights">The bonds' weights, or <see langword="null"/> or empty for their inverse durations.</param>
    /// <param name="constraint">The optimization's constraint, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The method has no unconstrained coefficient to fit.</exception>
    public ExponentialSplinesFitting(
        bool constrainAtZero,
        int numCoeffs,
        double fixedKappa,
        IEnumerable<double>? weights = null,
        Constraint? constraint = null)
        : this(constrainAtZero, weights, null, null, 0.0, double.MaxValue, numCoeffs, fixedKappa, constraint)
    {
    }

    /// <inheritdoc/>
    /// <remarks>The coefficients, one fewer when constrained at zero, and κ unless it is fixed.</remarks>
    public override int Size
    {
        get
        {
            int N = ConstrainAtZero ? numCoeffs : numCoeffs + 1;
            return fixedKappa is not null ? N - 1 : N; // One fewer optimization parameters if kappa is fixed
        }
    }

    /// <inheritdoc/>
    protected override double DiscountFunction(ReadOnlySpan<double> x, double t)
    {
        double d = 0.0;
        int N = Size;

        // Use the fixed kappa if given, otherwise take kappa from the passed x[] array; every parameter is a coefficient
        // when kappa is fixed
        double kappa = fixedKappa ?? x[N - 1];
        int terms = fixedKappa is not null ? N : N - 1;
        double coeff = 0;
        if (!ConstrainAtZero)
        {
            for (int i = 0; i < terms; ++i)
            {
                d += x[i] * Math.Exp(-kappa * (i + 1) * t);
            }
        }
        else
        {
            //  notation:
            //  d(t) = coeff* exp(-kappa*1*t) + x[0]* exp(-kappa*2*t) +
            //  x[1]* exp(-kappa*3*t) + ..+ x[7]* exp(-kappa*9*t)
            for (int i = 0; i < terms; i++)
            {
                d += x[i] * Math.Exp(-kappa * (i + 2) * t);
                coeff += x[i];
            }

            coeff = 1.0 - coeff;
            d += coeff * Math.Exp(-kappa * t);
        }

        return d;
    }
}
