// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/segmentintegral.hpp ql/math/integrals/segmentintegral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The integral of a one-dimensional function over a number N of intervals, by the trapezoid formula
/// ∫ₐᵇ f dx = (½f(x₀) + f(x₁) + … + f(x_{N−1}) + ½f(x_N))·Δx, x₀ = a, x_N = b, xᵢ = a + iΔx, Δx = (b − a)/N.
/// </summary>
/// <remarks>
/// The interior points are reached by adding Δx to the previous one while below b − Δx/2, as QuantLib's loop does. As
/// QuantLib's, it counts no evaluation.
/// </remarks>
public sealed class SegmentIntegral : Integrator
{
    private readonly int intervals;

    /// <summary>Initializes a new instance of the <see cref="SegmentIntegral"/> class.</summary>
    /// <param name="intervals">The number of intervals, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="intervals"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="intervals"/> is 0.</exception>
    public SegmentIntegral(int intervals)
        : base(1.0, 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(intervals);
        if (!(intervals > 0))
        {
            throw new ArgumentException("at least 1 interval needed, 0 given", nameof(intervals));
        }

        this.intervals = intervals;
    }

    /// <inheritdoc/>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        if (ComparisonFunctions.CloseEnough(a, b))
        {
            return new IntegrationResult(0.0, 0);
        }

        double dx = (b - a) / intervals;
        double sum = 0.5 * (f(a) + f(b));
        double end = b - (0.5 * dx);
        for (double x = a + dx; x < end; x += dx)
        {
            sum += f(x);
        }

        return new IntegrationResult(sum * dx, 0);
    }
}
