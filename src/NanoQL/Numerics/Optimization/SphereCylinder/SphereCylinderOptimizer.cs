// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/spherecylinder.hpp ql/math/optimization/spherecylinder.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The point closest to a point Z of the intersection, in R³, of the sphere of radius r centred at the origin and the
/// vertical cylinder of radius s centred at (α, 0): by projection onto the cylinder, or by a golden-section search
/// along the intersection. The intersection may be empty.
/// </summary>
/// <remarks>
/// <para>Immutable.</para>
/// <para>
/// One departure from QuantLib (<c>Divergences/SphereCylinderClampTests.cs</c>): the cylinder's radius is clamped to 0
/// and the clamped radius kept, where QuantLib's constructor clamps it after storing it, so its intersection test reads
/// the clamped radius and its solves the unclamped one (<c>spherecylinder.cpp:87</c>).
/// </para>
/// </remarks>
public sealed class SphereCylinderOptimizer
{
    private readonly double r;
    private readonly double s;
    private readonly double alpha;
    private readonly double z1;
    private readonly double z2;
    private readonly double z3;
    private readonly double topValue;
    private readonly double bottomValue;
    private readonly bool nonEmpty;
    private readonly double zweight;

    /// <summary>Initializes a new instance of the <see cref="SphereCylinderOptimizer"/> class.</summary>
    /// <param name="r">The sphere's radius, positive.</param>
    /// <param name="s">The cylinder's radius; a negative one is 0.</param>
    /// <param name="alpha">The cylinder centre's first coordinate, positive.</param>
    /// <param name="z1">Z's first coordinate.</param>
    /// <param name="z2">Z's second coordinate.</param>
    /// <param name="z3">Z's third coordinate.</param>
    /// <param name="zweight">The third coordinate's weight in the distance.</param>
    /// <exception cref="ArgumentException">The radius or the centre's coordinate isn't positive.</exception>
    public SphereCylinderOptimizer(double r, double s, double alpha, double z1, double z2, double z3, double zweight = 1.0)
    {
        if (!(r > 0))
        {
            throw new ArgumentException("sphere must have positive radius", nameof(r));
        }

        // the clamped radius, kept, where QuantLib stores the radius given
        s = Math.Max(s, 0.0);
        if (!(alpha > 0))
        {
            throw new ArgumentException("cylinder centre must have positive coordinate", nameof(alpha));
        }

        this.r = r;
        this.s = s;
        this.alpha = alpha;
        this.z1 = z1;
        this.z2 = z2;
        this.z3 = z3;
        this.zweight = zweight;

        nonEmpty = Math.Abs(alpha - s) <= r;

        double cylinderInside = (r * r) - ((s + alpha) * (s + alpha));

        if (cylinderInside > 0.0)
        {
            topValue = alpha + s;
            bottomValue = alpha - s;
        }
        else
        {
            bottomValue = alpha - s;
            double tmp = (r * r) - ((s * s) + (alpha * alpha));

            if (tmp <= 0)
            {
                // max to left of maximum
                double topValue2 = Math.Sqrt((s * s) - (tmp * tmp / (4 * alpha * alpha)));
                topValue = alpha - Math.Sqrt((s * s) - (topValue2 * topValue2));
            }
            else
            {
                topValue = alpha + (tmp / (2.0 * alpha));
            }
        }
    }

    /// <summary>Whether the sphere and the cylinder intersect.</summary>
    /// <returns>Whether the intersection is non-empty.</returns>
    public bool IsIntersectionNonEmpty() => nonEmpty;

    /// <summary>The point of the intersection closest to Z, by a golden-section search from the projection.</summary>
    /// <param name="maxIterations">The search's iterations at most.</param>
    /// <param name="tolerance">The search's interval at which it stops.</param>
    /// <param name="y1">The point's first coordinate.</param>
    /// <param name="y2">The point's second coordinate.</param>
    /// <param name="y3">The point's third coordinate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    public void FindClosest(int maxIterations, double tolerance, out double y1, out double y2, out double y3)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        FindByProjection(out double x1, out _, out _);

        y1 = BrentMinimize(bottomValue, x1, topValue, tolerance, maxIterations);
        y2 = Math.Sqrt((s * s) - ((y1 - alpha) * (y1 - alpha)));
        y3 = Math.Sqrt((r * r) - (y1 * y1) - (y2 * y2));
    }

    /// <summary>Z's projection onto the cylinder, and onto the sphere where the projection lies outside it.</summary>
    /// <param name="y1">The point's first coordinate.</param>
    /// <param name="y2">The point's second coordinate.</param>
    /// <param name="y3">The point's third coordinate.</param>
    /// <returns>Whether the point is on the intersection.</returns>
    public bool FindByProjection(out double y1, out double y2, out double y3)
    {
        double z1moved = z1 - alpha;
        double distance = Math.Sqrt((z1moved * z1moved) + (z2 * z2));
        double scale = s / distance;
        double y1moved = z1moved * scale;
        y1 = alpha + y1moved;
        y2 = scale * z2;
        double residual = (r * r) - (y1 * y1) - (y2 * y2);
        if (residual >= 0.0)
        {
            y3 = Math.Sqrt(residual);
            return true;
        }

        // we are outside the sphere
        if (!IsIntersectionNonEmpty())
        {
            y3 = 0.0;
            return false;
        }

        // intersection is non-empty but projection point is outside sphere so take rightmost point
        y3 = 0.0;
        y1 = topValue;
        y2 = Math.Sqrt((r * r) - (y1 * y1));

        return true;
    }

    private double ObjectiveFunction(double x1)
    {
        double x2sq = (s * s) - ((x1 - alpha) * (x1 - alpha));

        // a negative number will be minuscule and a result of rounding error
        double x2 = x2sq >= 0.0 ? Math.Sqrt(x2sq) : 0.0;
        double x3 = Math.Sqrt((r * r) - (x1 * x1) - (x2 * x2));

        double err = 0.0;
        err += (x1 - z1) * (x1 - z1);
        err += (x2 - z2) * (x2 - z2);
        err += (x3 - z3) * (x3 - z3) * zweight;

        return err;
    }

    // QuantLib's anonymous BrentMinimize: a golden-section search, from the mid point where it is inside the bounds.
    private double BrentMinimize(double low, double mid, double high, double tolerance, int maxIt)
    {
        double w = 0.5 * (3.0 - Math.Sqrt(5.0));
        double x = (w * low) + ((1 - w) * high);
        if (mid > low && mid < high)
        {
            x = mid;
        }

        double midValue = ObjectiveFunction(x);

        int iterations = 0;
        while (high - low > tolerance && iterations < maxIt)
        {
            if (x - low > high - x)
            {
                // left interval is bigger
                double tentativeNewMid = (w * low) + ((1 - w) * x);
                double tentativeNewMidValue = ObjectiveFunction(tentativeNewMid);

                if (tentativeNewMidValue < midValue)
                {
                    // go left
                    high = x;
                    x = tentativeNewMid;
                    midValue = tentativeNewMidValue;
                }
                else
                {
                    // go right
                    low = tentativeNewMid;
                }
            }
            else
            {
                double tentativeNewMid = (w * x) + ((1 - w) * high);
                double tentativeNewMidValue = ObjectiveFunction(tentativeNewMid);

                if (tentativeNewMidValue < midValue)
                {
                    // go right
                    low = x;
                    x = tentativeNewMid;
                    midValue = tentativeNewMidValue;
                }
                else
                {
                    // go left
                    high = tentativeNewMid;
                }
            }

            ++iterations;
        }

        return x;
    }
}
