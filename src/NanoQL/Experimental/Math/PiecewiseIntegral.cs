// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/piecewiseintegral.hpp ql/experimental/math/piecewiseintegral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The integral of a piecewise well-behaved function, each piece integrated by a given integrator, optionally over
/// intervals that strictly exclude the critical points: QuantLib's <c>PiecewiseIntegral</c>.
/// </summary>
/// <remarks>
/// <para>
/// The critical points are sorted, and points close to each other (<c>close_enough</c>) merged. Avoiding them, each
/// piece's ends are nudged inward by a factor of 1 + ε, as QuantLib's are at points above 0. At a point at or below 0
/// QuantLib's multiplication moves the end the wrong way, or not at all: a step at −1 integrates to 3.5 over [−3, 3],
/// exactly 4, and a step at 0 to 3.75, exactly 3. nanoql's nudge is mirrored below 0, c·(1 + ε) to the left and
/// c/(1 + ε) to the right, and at 0 is the next double each way, C's <c>nextafter</c>, at each of QuantLib's five
/// nudges (<c>Divergences/PiecewiseIntegralNudgeTests.cs</c>).
/// </para>
/// <para>
/// Its result counts no evaluations and carries no error estimate, QuantLib's own state after a call; each piece's
/// are the given integrator's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class PiecewiseIntegral : Integrator
{
    private readonly Integrator integrator;
    private readonly ImmutableArray<double> criticalPoints;
    private readonly double eps;

    /// <summary>Initializes a new instance of the <see cref="PiecewiseIntegral"/> class.</summary>
    /// <param name="integrator">The integrator of each piece.</param>
    /// <param name="criticalPoints">The critical points, in any order.</param>
    /// <param name="avoidCriticalPoints">Whether each piece strictly excludes the critical points.</param>
    public PiecewiseIntegral(Integrator integrator, IEnumerable<double> criticalPoints, bool avoidCriticalPoints = true)
        : base(1.0, 1)
    {
        ArgumentNullException.ThrowIfNull(integrator);
        ArgumentNullException.ThrowIfNull(criticalPoints);
        this.integrator = integrator;
        eps = avoidCriticalPoints ? 1.0 + NumericLimits.QL_EPSILON : 1.0;

        // std::sort, then std::unique on close_enough: each run of close points keeps its first
        List<double> sorted = [.. criticalPoints];
        sorted.Sort();
        var unique = new List<double>(sorted.Count);
        foreach (double x in sorted)
        {
            if (unique.Count == 0 || !ComparisonFunctions.CloseEnough(unique[^1], x))
            {
                unique.Add(x);
            }
        }

        this.criticalPoints = [.. unique];
    }

    /// <inheritdoc/>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        // std::lower_bound: the first critical point not below a, and not below b
        int a0 = LowerBound(a);
        int b0 = LowerBound(b);

        if (a0 == criticalPoints.Length)
        {
            double start = a;
            if (!criticalPoints.IsEmpty && ComparisonFunctions.CloseEnough(a, criticalPoints[^1]))
            {
                start = Right(a);
            }

            return new IntegrationResult(IntegrateH(f, start, b), 0);
        }

        double res = 0.0;

        if (!ComparisonFunctions.CloseEnough(a, criticalPoints[a0]))
        {
            res += IntegrateH(f, a, Math.Min(Left(criticalPoints[a0]), b));
        }

        if (b0 == criticalPoints.Length)
        {
            --b0;
            if (!ComparisonFunctions.CloseEnough(criticalPoints[b0], b))
            {
                res += IntegrateH(f, Right(criticalPoints[b0]), b);
            }
        }

        for (int x = a0; x < b0; ++x)
        {
            res += IntegrateH(f, Right(criticalPoints[x]), Math.Min(Left(criticalPoints[x + 1]), b));
        }

        return new IntegrationResult(res, 0);
    }

    private double IntegrateH(Func<double, double> f, double a, double b) =>
        !ComparisonFunctions.CloseEnough(a, b) ? integrator.Integrate(f, a, b) : 0.0;

    // A piece's end just right of a point: QuantLib's c·(1 + ε) above 0, mirrored below, the next double at 0.
    private double Right(double c)
    {
        if (eps == 1.0)
        {
            return c;
        }

        return c > 0.0 ? c * eps : c < 0.0 ? c / eps : Math.BitIncrement(0.0);
    }

    // A piece's end just left of a point: QuantLib's c/(1 + ε) above 0, mirrored below, the next double at 0.
    private double Left(double c)
    {
        if (eps == 1.0)
        {
            return c;
        }

        return c > 0.0 ? c / eps : c < 0.0 ? c * eps : Math.BitDecrement(0.0);
    }

    // std::lower_bound's halving over the critical points
    private int LowerBound(double x)
    {
        int first = 0, count = criticalPoints.Length;
        while (count > 0)
        {
            int half = count / 2;
            int mid = first + half;
            if (criticalPoints[mid] < x)
            {
                first = mid + 1;
                count -= half + 1;
            }
            else
            {
                count = half;
            }
        }

        return first;
    }
}
