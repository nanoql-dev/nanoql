// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2014 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/concentrating1dmesher.hpp ql/methods/finitedifferences/meshers/concentrating1dmesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A one-dimensional mesher whose points concentrate around critical points: around one point by a sinh transform,
/// or around several by the solution of an ordinary differential equation.
/// </summary>
/// <remarks>Immutable.</remarks>
public sealed class Concentrating1dMesher : Fdm1dMesher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Concentrating1dMesher"/> class: its points concentrating around
    /// one point, or equally spaced without one.
    /// </summary>
    /// <param name="start">The first point.</param>
    /// <param name="end">The last point.</param>
    /// <param name="size">The number of points, at least 2.</param>
    /// <param name="cPoints">
    /// The critical point and its density, relative to the mesher's length; <see langword="null"/> for none, QuantLib's
    /// <c>Null&lt;Real&gt;()</c>.
    /// </param>
    /// <param name="requireCPoint">Whether the critical point must be one of the mesher's points.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="end"/> isn't above <paramref name="start"/>; the critical point is outside them, or given
    /// without a density; the density isn't positive; the point is required but not given; or
    /// <paramref name="size"/> is 0 or 1, where QuantLib writes past an empty mesher's arrays and leaves a one-point
    /// mesher's distances at its null value.
    /// </exception>
    public Concentrating1dMesher(
        double start,
        double end,
        int size,
        (double? Point, double? Density) cPoints = default,
        bool requireCPoint = false)
        : base(size)
    {
        if (!(end > start))
        {
            throw new ArgumentException("end must be larger than start", nameof(end));
        }

        double? cPoint = cPoints.Point;
        double? density = cPoints.Density is double d ? d * (end - start) : null;

        if (!(cPoint is null || (cPoint >= start && cPoint <= end)))
        {
            throw new ArgumentException("cPoint must be between start and end", nameof(cPoints));
        }

        if (!(density is null || density > 0.0))
        {
            throw new ArgumentException("density > 0 required", nameof(cPoints));
        }

        if (!(cPoint is null || density is not null))
        {
            throw new ArgumentException("density must be given if cPoint is given", nameof(cPoints));
        }

        if (!(!requireCPoint || cPoint is not null))
        {
            throw new ArgumentException("cPoint is required in grid but not given", nameof(requireCPoint));
        }

        // QuantLib's size-1 wraps at 0, and its loop writes an empty vector; at 1, dplus and dminus keep Null<Real>()
        RequireTwoPoints(size, nameof(size));

        double dx = 1.0 / (size - 1);

        if (cPoint is double c && density is double den)
        {
            LinearInterpolation? transform = null;
            double c1 = Math.Asinh((start - c) / den);
            double c2 = Math.Asinh((end - c) / den);
            if (requireCPoint)
            {
                var u = new List<double> { 0.0 };
                var z = new List<double> { 0.0 };
                if (!ComparisonFunctions.Close(c, start) && !ComparisonFunctions.Close(c, end))
                {
                    double z0 = -c1 / (c2 - c1);
                    double u0 = Math.Max(
                        Math.Min((long)Math.Round(z0 * (size - 1), MidpointRounding.AwayFromZero), (long)size - 2),
                        1L)
                        / (double)(size - 1);
                    u.Add(u0);
                    z.Add(z0);
                }

                u.Add(1.0);
                z.Add(1.0);
                transform = new LinearInterpolation([.. u], [.. z]);
            }

            for (int i = 1; i < size - 1; ++i)
            {
                double li = requireCPoint ? transform!.Evaluate(i * dx) : i * dx;
                locations[i] = c + (den * Math.Sinh((c1 * (1.0 - li)) + (c2 * li)));
            }
        }
        else
        {
            for (int i = 1; i < size - 1; ++i)
            {
                locations[i] = start + (i * dx * (end - start));
            }
        }

        locations[0] = start;
        locations[^1] = end;

        for (int i = 0; i < size - 1; ++i)
        {
            dplus[i] = dminus[i + 1] = locations[i + 1] - locations[i];
        }

        dplus[^1] = dminus[0] = NullReal;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Concentrating1dMesher"/> class: its points concentrating around
    /// several critical points, each with its density, the mesher's points the solution of an ordinary differential
    /// equation.
    /// </summary>
    /// <param name="start">The first point.</param>
    /// <param name="end">The last point.</param>
    /// <param name="size">The number of points, at least 2.</param>
    /// <param name="cPoints">
    /// The critical points: each its location, its density relative to the mesher's length, and whether it must be one
    /// of the mesher's points.
    /// </param>
    /// <param name="tol">The tolerance of the differential equation's solver and of the scaling's root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cPoints"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="end"/> isn't above <paramref name="start"/>, or <paramref name="size"/> is 0 or 1, where
    /// QuantLib writes past an empty array and leaves a one-point mesher's distances at its null value.
    /// </exception>
    /// <exception cref="NanoQLException">The solver or a root search fails.</exception>
    public Concentrating1dMesher(
        double start,
        double end,
        int size,
        IEnumerable<(double Point, double Density, bool Required)> cPoints,
        double tol = 1e-8)
        : base(size)
    {
        ArgumentNullException.ThrowIfNull(cPoints);
        if (!(end > start))
        {
            throw new ArgumentException("end must be larger than start", nameof(end));
        }

        // QuantLib's x[0] = 0.0 writes an empty array at 0; at 1, dplus and dminus keep Null<Real>()
        RequireTwoPoints(size, nameof(size));

        (double Point, double Density, bool Required)[] cps = [.. cPoints];
        double[] points = new double[cps.Length], betas = new double[cps.Length];
        for (int i = 0; i < cps.Length; ++i)
        {
            points[i] = cps[i].Point;
            double b = cps[i].Density * (end - start);
            betas[i] = b * b;
        }

        // get scaling factor a so that y(1) = end
        double aInit = 0.0;
        for (int i = 0; i < points.Length; ++i)
        {
            double c1 = Math.Asinh((start - points[i]) / betas[i]);
            double c2 = Math.Asinh((end - points[i]) / betas[i]);
            aInit += (c2 - c1) / points.Length;
        }

        var rk = new AdaptiveRungeKutta<double>(tol);
        double Solve(double a, double y0, double x0, double x1) =>
            rk.Evaluate((x, y) => Jac(a, y, points, betas), y0, x0, x1);

        double scale = new Brent().Solve(x => Solve(x, start, 0.0, 1.0) - end, tol, aInit, 0.1 * aInit);

        // solve ODE for all grid points
        double[] xs = new double[size], ys = new double[size];
        xs[0] = 0.0;
        ys[0] = start;
        double dx = 1.0 / (size - 1);
        for (int i = 1; i < size; ++i)
        {
            xs[i] = i * dx;
            ys[i] = Solve(scale, ys[i - 1], xs[i - 1], xs[i]);
        }

        // eliminate numerical noise and ensure y(1) = end
        double dy = ys[^1] - end;
        for (int i = 1; i < size; ++i)
        {
            ys[i] -= i * dx * dy;
        }

        var odeSolution = new LinearInterpolation(xs, ys);

        // ensure required points are part of the grid
        var w = new List<(double First, double Second)> { (0.0, 0.0) };

        for (int i = 0; i < points.Length; ++i)
        {
            if (cps[i].Required && points[i] > start && points[i] < end)
            {
                int j = LowerBound(ys, points[i]);
                double p = points[i];
                double e = new Brent().Solve(x => odeSolution.Evaluate(x, true) - p, NumericLimits.QL_EPSILON, xs[j], 0.5 / size);

                w.Add((Math.Min(xs[size - 2], xs[j]), e));
            }
        }

        w.Add((1.0, 1.0));

        // std::sort on pairs, then std::unique keeping the first of each run whose first members are close
        w.Sort((p, q) => p.First < q.First ? -1 : q.First < p.First ? 1 : p.Second < q.Second ? -1 : q.Second < p.Second ? 1 : 0);
        var unique = new List<(double First, double Second)>(w.Count);
        foreach ((double First, double Second) p in w)
        {
            if (unique.Count == 0 || !ComparisonFunctions.CloseEnough(unique[^1].First, p.First, 1000))
            {
                unique.Add(p);
            }
        }

        double[] us = new double[unique.Count], zs = new double[unique.Count];
        for (int i = 0; i < unique.Count; ++i)
        {
            us[i] = unique[i].First;
            zs[i] = unique[i].Second;
        }

        var transform = new LinearInterpolation(us, zs);

        for (int i = 0; i < size; ++i)
        {
            locations[i] = odeSolution.Evaluate(transform.Evaluate(i * dx));
        }

        for (int i = 0; i < size - 1; ++i)
        {
            dplus[i] = dminus[i + 1] = locations[i + 1] - locations[i];
        }

        dplus[^1] = dminus[0] = NullReal;
    }

    // QuantLib's OdeIntegrationFct::jac
    private static double Jac(double a, double y, double[] points, double[] betas)
    {
        double s = 0.0;
        for (int i = 0; i < points.Length; ++i)
        {
            double d = y - points[i];
            s += 1.0 / (betas[i] + (d * d));
        }

        return a / Math.Sqrt(s);
    }

    // std::lower_bound: the first index whose value isn't below x
    private static int LowerBound(double[] values, double x)
    {
        int first = 0, count = values.Length;
        while (count > 0)
        {
            int step = count / 2;
            int it = first + step;
            if (values[it] < x)
            {
                first = it + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }
}
