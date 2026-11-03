// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepconditions/fdmarithmeticaveragecondition.hpp ql/methods/finitedifferences/stepconditions/fdmarithmeticaveragecondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The step condition of a discrete arithmetic average on a two-dimensional mesher of the log spot and the log average:
/// at each fixing time the average moves to its new value, the value function interpolated along the average's
/// direction by a monotonic natural cubic spline.
/// </summary>
/// <remarks>
/// <para>
/// A working type: it holds its buffers, so a step allocates nothing, its spline QuantLib's
/// <c>MonotonicCubicNaturalSpline</c> computed into them with the same arithmetic. QuantLib's unused running-accumulator
/// argument is kept, its signature's.
/// </para>
/// <para>
/// A documented divergence. With several fixings on one time, the new average weighs them by the count after the
/// duplicates, n/i for n fixings ending the i-th, where QuantLib's weights read the count up to the first of them,
/// +4.4% against an exact Monte Carlo on two fixings on one date (<c>Divergences/AsianDuplicateFixingsTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmArithmeticAverageCondition : StepCondition
{
    private readonly double[] x;
    private readonly double[] averages;
    private readonly ImmutableArray<double> averageTimes;
    private readonly int pastFixings;
    private readonly FdmMesher mesher;
    private readonly int equityDirection;
    private readonly double[] copy;
    private readonly double[] column;
    private readonly Spline spline;

    /// <summary>Initializes a new instance of the <see cref="FdmArithmeticAverageCondition"/> class.</summary>
    /// <param name="averageTimes">The fixing times.</param>
    /// <param name="runningAccumulator">QuantLib's unused running-accumulator argument.</param>
    /// <param name="pastFixings">The number of past fixings.</param>
    /// <param name="mesher">The two-dimensional mesher, of the log spot and the log average.</param>
    /// <param name="equityDirection">The spot's direction, 0 or 1.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pastFixings"/> is negative.</exception>
    /// <exception cref="NanoQLException">The mesher isn't two-dimensional, or the direction isn't 0 or 1.</exception>
    public FdmArithmeticAverageCondition(
        IEnumerable<double> averageTimes,
        double runningAccumulator,
        int pastFixings,
        FdmMesher mesher,
        int equityDirection)
    {
        ArgumentNullException.ThrowIfNull(averageTimes);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentOutOfRangeException.ThrowIfNegative(pastFixings);
        _ = runningAccumulator;
        if (!(mesher.Layout.Dim.Length == 2))
        {
            throw new NanoQLException("2D allowed only");
        }

        if (!(equityDirection == 0 || equityDirection == 1))
        {
            throw new NanoQLException("equityDirection has to be 0 or 1");
        }

        this.averageTimes = [.. averageTimes];
        this.pastFixings = pastFixings;
        this.mesher = mesher;
        this.equityDirection = equityDirection;

        x = new double[mesher.Layout.Dim[equityDirection]];
        averages = new double[mesher.Layout.Dim[equityDirection == 0 ? 1 : 0]];
        int xSpacing = mesher.Layout.Spacing[equityDirection];
        Vector tmp = mesher.Locations(equityDirection);
        for (int i = 0; i < x.Length; ++i)
        {
            x[i] = Math.Exp(tmp[i * xSpacing]);
        }

        int averageDirection = equityDirection == 0 ? 1 : 0;
        int aSpacing = mesher.Layout.Spacing[averageDirection];
        tmp = mesher.Locations(averageDirection);
        for (int i = 0; i < averages.Length; ++i)
        {
            averages[i] = Math.Exp(tmp[i * aSpacing]);
        }

        copy = new double[mesher.Layout.Size];
        column = new double[averages.Length];
        spline = new Spline(averages);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The array's size isn't the mesher's.</exception>
    public override void ApplyTo(Span<double> a, double t)
    {
        if (!(mesher.Layout.Size == a.Length))
        {
            throw new NanoQLException("inconsistent array dimensions");
        }

        int first = averageTimes.IndexOf(t);
        if (first < 0)
        {
            return;
        }

        int nTimes = 0;
        foreach (double time in averageTimes)
        {
            if (time == t)
            {
                ++nTimes;
            }
        }

        a.CopyTo(copy);

        // the count of fixings to the last on this time, where QuantLib's reads the first's
        int iT = first + nTimes + pastFixings;
        int averageDirection = equityDirection == 0 ? 1 : 0;
        int xSpacing = mesher.Layout.Spacing[equityDirection];
        int aSpacing = mesher.Layout.Spacing[averageDirection];
        for (int i = 0; i < x.Length; ++i)
        {
            for (int j = 0; j < averages.Length; ++j)
            {
                column[j] = copy[(i * xSpacing) + (j * aSpacing)];
            }

            spline.Update(column);
            for (int j = 0; j < averages.Length; ++j)
            {
                a[(i * xSpacing) + (j * aSpacing)] = spline.Value(
                    ((iT - nTimes) / (double)iT * averages[j]) + (nTimes / (double)iT * x[i]));
            }
        }
    }

    // QuantLib's MonotonicCubicNaturalSpline on fixed abscissas, its coefficients computed into held buffers by
    // CubicInterpolation's arithmetic (the Spline derivatives, a natural second-derivative condition at both ends,
    // Hyman's filter), evaluated with extrapolation
    private sealed class Spline
    {
        private readonly double[] xs;
        private readonly double[] dx;
        private readonly double[] s;
        private readonly double[] lower;
        private readonly double[] diagonal;
        private readonly double[] upper;
        private readonly double[] temp;
        private readonly Vector tmp;
        private readonly bool[] adjustments;
        private readonly double[] ys;
        private readonly double[] a;
        private readonly double[] b;
        private readonly double[] c;

        public Spline(double[] xs)
        {
            int n = xs.Length;
            this.xs = xs;
            dx = new double[n - 1];
            for (int i = 0; i < n - 1; ++i)
            {
                dx[i] = xs[i + 1] - xs[i];
            }

            s = new double[n - 1];
            lower = new double[n - 1];
            diagonal = new double[n];
            upper = new double[n - 1];
            temp = new double[n];
            tmp = new Vector(n);
            adjustments = new bool[n];
            ys = new double[n];
            a = new double[n - 1];
            b = new double[n - 1];
            c = new double[n - 1];
        }

        public void Update(ReadOnlySpan<double> values)
        {
            int n = xs.Length;
            values.CopyTo(ys);
            for (int i = 0; i < n - 1; ++i)
            {
                s[i] = (ys[i + 1] - ys[i]) / dx[i];
            }

            for (int i = 1; i < n - 1; ++i)
            {
                lower[i - 1] = dx[i];
                diagonal[i] = 2.0 * (dx[i] + dx[i - 1]);
                upper[i] = dx[i - 1];
                tmp[i] = 3.0 * ((dx[i] * s[i - 1]) + (dx[i - 1] * s[i]));
            }

            // the natural conditions, a second derivative of 0 at either end
            diagonal[0] = 2.0;
            upper[0] = 1.0;
            tmp[0] = (3.0 * s[0]) - (0.0 * dx[0] / 2.0);
            lower[n - 2] = 1.0;
            diagonal[n - 1] = 2.0;
            tmp[n - 1] = (3.0 * s[n - 2]) + (0.0 * dx[n - 2] / 2.0);

            // TridiagonalOperator.SolveFor in place
            double bet = diagonal[0];
            if (ComparisonFunctions.Close(bet, 0.0))
            {
                throw new NanoQLException($"diagonal's first element ({bet}) cannot be close to zero");
            }

            tmp[0] = tmp[0] / bet;
            for (int j = 1; j <= n - 1; ++j)
            {
                temp[j] = upper[j - 1] / bet;
                bet = diagonal[j] - (lower[j - 1] * temp[j]);
                if (ComparisonFunctions.Close(bet, 0.0))
                {
                    throw new NanoQLException("division by zero");
                }

                tmp[j] = (tmp[j] - (lower[j - 1] * tmp[j - 1])) / bet;
            }

            for (int j = n - 2; j > 0; --j)
            {
                tmp[j] -= temp[j + 1] * tmp[j + 1];
            }

            tmp[0] -= temp[1] * tmp[1];

            Array.Clear(adjustments);
            CubicInterpolation.HymanFilter(n, dx, s, tmp, adjustments);
            for (int i = 0; i < n - 1; ++i)
            {
                a[i] = tmp[i];
                b[i] = ((3.0 * s[i]) - tmp[i + 1] - (2.0 * tmp[i])) / dx[i];
                c[i] = (tmp[i + 1] + tmp[i] - (2.0 * s[i])) / (dx[i] * dx[i]);
            }
        }

        public double Value(double x)
        {
            int j = x < xs[0] ? 0 : x > xs[^1] ? xs.Length - 2 : Interpolation.UpperBound(xs.AsSpan(0, xs.Length - 1), x) - 1;
            double d = x - xs[j];
            return ys[j] + (d * (a[j] + (d * (b[j] + (d * c[j])))));
        }
    }
}
