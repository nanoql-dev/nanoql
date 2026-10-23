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
/// The Nelson-Siegel fitting method: a discount function d(t) = e^(−r t), the zero rate
/// r = c_0 + (c_1 + c_2)(1 − e^(−κt))/(κt) − c_2 e^(−κt).
/// </summary>
/// <remarks>
/// See Nelson, C. and A. Siegel (1985): "Parsimonious modeling of yield curves for US Treasury bills." NBER Working
/// Paper Series, no 1594. The parameters are c_0, c_1, c_2 and κ; the method is constrained at zero. QuantLib's
/// constructor without the optimization method is the first constructor with <c>l2</c> named.
/// </remarks>
public sealed class NelsonSiegelFitting : FittedBondDiscountCurveFittingMethod
{
    /// <summary>Initializes a new instance of the <see cref="NelsonSiegelFitting"/> class.</summary>
    /// <param name="weights">The bonds' weights, or <see langword="null"/> or empty for their inverse durations.</param>
    /// <param name="optimizationMethod">The optimization method, or <see langword="null"/> for a <see cref="Simplex"/>.</param>
    /// <param name="l2">The L2 penalties, one per parameter, or <see langword="null"/> or empty for none.</param>
    /// <param name="minCutoffTime">The time before which the discount extrapolates with a flat forward rate.</param>
    /// <param name="maxCutoffTime">The time after which the discount extrapolates with a flat forward rate.</param>
    /// <param name="constraint">The optimization's constraint, or <see langword="null"/> for none.</param>
    public NelsonSiegelFitting(
        IEnumerable<double>? weights = null,
        OptimizationMethod? optimizationMethod = null,
        IEnumerable<double>? l2 = null,
        double minCutoffTime = 0.0,
        double maxCutoffTime = double.MaxValue,
        Constraint? constraint = null)
        : base(true, weights, optimizationMethod, l2, minCutoffTime, maxCutoffTime, constraint)
    {
    }

    /// <inheritdoc/>
    /// <remarks>Four: c_0, c_1, c_2 and κ.</remarks>
    public override int Size => 4;

    /// <inheritdoc/>
    protected override double DiscountFunction(ReadOnlySpan<double> x, double t)
    {
        double kappa = x[Size - 1];
        double zeroRate = x[0] + ((x[1] + x[2])
                                  * (1.0 - Math.Exp(-kappa * t))
                                  / ((kappa + NumericLimits.QL_EPSILON) * (t + NumericLimits.QL_EPSILON)))
                          - (x[2] * Math.Exp(-kappa * t));
        double d = Math.Exp(-zeroRate * t);
        return d;
    }
}
