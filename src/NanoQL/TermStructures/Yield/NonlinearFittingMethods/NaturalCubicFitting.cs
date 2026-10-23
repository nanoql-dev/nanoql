// SPDX-FileCopyrightText: Copyright (C) 2007 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2010 Alessandro Roveda
// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/nonlinearfittingmethods.hpp ql/termstructures/yield/nonlinearfittingmethods.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The natural cubic spline fitting method: a discount function interpolated by a natural cubic spline, its second
/// derivative 0 at both ends, through nodal discount values d(t_i), the parameters.
/// </summary>
/// <remarks>
/// <para>
/// The knots are the given times and 0, sorted, times within 1e-14 of the one before taken as one. d(0) is 1, so the
/// parameters are the discounts at the other knots: QuantLib's doc says so "if constrainAtZero is true", but the
/// method always constrains at zero. Past the last knot the discount is held at the last node's, not extrapolated, as
/// QuantLib clamps the time to the knots' range, unless a maximum cut-off time is given. QuantLib's constructor without
/// the optimization method is the first constructor with <c>l2</c> named.
/// </para>
/// <para>QuantLib-Python doesn't bind the class: its vectors are the C++ harness's.</para>
/// </remarks>
public sealed class NaturalCubicFitting : FittedBondDiscountCurveFittingMethod
{
    private readonly ImmutableArray<double> knotTimes;
    private readonly int size;

    /// <summary>Initializes a new instance of the <see cref="NaturalCubicFitting"/> class.</summary>
    /// <param name="knotTimes">The knot times; 0 is added.</param>
    /// <param name="weights">The bonds' weights, or <see langword="null"/> or empty for their inverse durations.</param>
    /// <param name="optimizationMethod">The optimization method, or <see langword="null"/> for a <see cref="Simplex"/>.</param>
    /// <param name="l2">The L2 penalties, one per parameter, or <see langword="null"/> or empty for none.</param>
    /// <param name="minCutoffTime">The time before which the discount extrapolates with a flat forward rate.</param>
    /// <param name="maxCutoffTime">The time after which the discount extrapolates with a flat forward rate.</param>
    /// <param name="constraint">The optimization's constraint, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="knotTimes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Fewer than two distinct knot times remain with 0, or a knot spacing isn't finite.
    /// </exception>
    public NaturalCubicFitting(
        IEnumerable<double> knotTimes,
        IEnumerable<double>? weights = null,
        OptimizationMethod? optimizationMethod = null,
        IEnumerable<double>? l2 = null,
        double minCutoffTime = 0.0,
        double maxCutoffTime = double.MaxValue,
        Constraint? constraint = null)
        : base(true, weights, optimizationMethod, l2, minCutoffTime, maxCutoffTime, constraint)
    {
        ArgumentNullException.ThrowIfNull(knotTimes);
        List<double> knots = [.. knotTimes, 0.0];
        knots.Sort();

        // std::unique: a time within 1e-14 of the last one kept is dropped
        var unique = new List<double>(knots.Count);
        foreach (double k in knots)
        {
            if (unique.Count == 0 || !(Math.Abs(unique[^1] - k) <= 1e-14))
            {
                unique.Add(k);
            }
        }

        if (!(unique.Count >= 2))
        {
            throw new ArgumentException("NaturalCubicFitting: at least two knot times required", nameof(knotTimes));
        }

        int n = unique.Count;
        size = n - 1;
        for (int i = 0; i + 1 < n; ++i)
        {
            double h = unique[i + 1] - unique[i];
            if (!(h > 1e-14))
            {
                throw new ArgumentException(
                    "NaturalCubicFitting: knot times must be strictly increasing (non-zero spacing)", nameof(knotTimes));
            }

            if (!double.IsFinite(h))
            {
                throw new ArgumentException("NaturalCubicFitting: non-finite knot spacing", nameof(knotTimes));
            }
        }

        this.knotTimes = [.. unique];
    }

    /// <inheritdoc/>
    /// <remarks>The discounts at the knots but 0.</remarks>
    public override int Size => size;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The parameters aren't <see cref="Size"/> in number, or one isn't finite.</exception>
    protected override double DiscountFunction(ReadOnlySpan<double> x, double t)
    {
        int n = knotTimes.Length;
        int expected = Size;
        if (!(x.Length == expected))
        {
            throw new ArgumentException(
                $"NaturalCubicFitting::discountFunction(): parameter size mismatch: expected {expected} got {x.Length}",
                nameof(x));
        }

        var y = new double[n];
        y[0] = 1.0;
        for (int i = 1; i < n; ++i)
        {
            y[i] = x[i - 1];
        }

        for (int i = 0; i < n; ++i)
        {
            if (!double.IsFinite(y[i]))
            {
                throw new ArgumentException("NaturalCubicFitting::discountFunction(): non-finite nodal value", nameof(x));
            }
        }

        var spline = new CubicNaturalSpline(knotTimes.AsSpan(), y);
        return spline.Evaluate(Math.Clamp(t, knotTimes[0], knotTimes[^1]));
    }
}
