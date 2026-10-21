// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmdividendhandler.hpp ql/methods/finitedifferences/utilities/fdmdividendhandler.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The cash-dividend condition of a finite-difference grid in the logarithm of the underlying: at a dividend's time,
/// each value moves to the value at the underlying less the dividend, by linear interpolation along the equity
/// direction, floored at the grid's lowest underlying.
/// </summary>
/// <remarks>
/// A working type (the S-eq plan's SEQR1): the interpolation runs on buffers the handler holds, by
/// <see cref="NanoQL.Numerics.LinearInterpolation"/>'s arithmetic, so applying it allocates nothing, where QuantLib
/// copies the values at every step and builds an interpolation at each dividend.
/// <para>
/// Every line along the equity direction moves. QuantLib walks, for each other direction, only the lines through the
/// other directions' first points (<c>fdmdividendhandler.cpp:84-103</c>), which covers a two-dimensional grid but in
/// three or more dimensions leaves the rest as they were: on a 3 × 5 × 4 grid, 6 of the 12 lines. Each line here moves
/// as QuantLib's one-dimensional handler moves it, which is QuantLib's check; in one and two dimensions the values are
/// QuantLib's. A documented divergence.
/// </para>
/// </remarks>
public sealed class FdmDividendHandler : StepCondition
{
    private readonly double[] x;
    private readonly double[] dividendTimes;
    private readonly ImmutableArray<Date> dividendDates;
    private readonly double[] dividends;
    private readonly FdmMesher mesher;
    private readonly int equityDirection;

    // the interpolation's buffers: the values copied, a line of them, and the slopes
    private readonly double[] aCopy;
    private readonly double[] line;
    private readonly double[] slopes;

    /// <summary>Initializes a new instance of the <see cref="FdmDividendHandler"/> class.</summary>
    /// <param name="schedule">The dividends.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="referenceDate">The date of time 0.</param>
    /// <param name="dayCounter">The day counter measuring the dividends' times.</param>
    /// <param name="equityDirection">The direction of the underlying's logarithm.</param>
    /// <param name="context">The evaluation context the dividends' amounts are read in.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is <c>default(ImmutableArray)</c> or holds a null dividend, or the reference date is
    /// <c>default(Date)</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmDividendHandler(
        ImmutableArray<Dividend> schedule,
        FdmMesher mesher,
        Date referenceDate,
        DayCounter dayCounter,
        int equityDirection,
        EvaluationContext context)
    {
        if (schedule.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a dividend schedule; an empty one is []", nameof(schedule));
        }

        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        ArgumentOutOfRangeException.ThrowIfNegative(equityDirection);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(equityDirection, mesher.Layout.Dim.Length);
        if (schedule.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", nameof(schedule));
        }

        x = new double[mesher.Layout.Dim[equityDirection]];
        this.mesher = mesher;
        this.equityDirection = equityDirection;

        dividends = new double[schedule.Length];
        var dates = ImmutableArray.CreateBuilder<Date>(schedule.Length);
        dividendTimes = new double[schedule.Length];
        for (int i = 0; i < schedule.Length; ++i)
        {
            dividends[i] = schedule[i].Amount(context);
            dates.Add(schedule[i].Date);
            dividendTimes[i] = dayCounter.YearFraction(referenceDate, schedule[i].Date);
        }

        dividendDates = dates.MoveToImmutable();

        NanoQL.Numerics.Vector tmp = mesher.Locations(equityDirection);
        int spacing = mesher.Layout.Spacing[equityDirection];
        for (int i = 0; i < x.Length; ++i)
        {
            x[i] = Math.Exp(tmp[i * spacing]);
        }

        aCopy = new double[mesher.Layout.Size];
        line = new double[x.Length];
        slopes = new double[x.Length];
    }

    /// <summary>Gets the dividends' times.</summary>
    public ImmutableArray<double> DividendTimes => [.. dividendTimes];

    /// <summary>Gets the dividends' dates.</summary>
    public ImmutableArray<Date> DividendDates => dividendDates;

    /// <summary>Gets the dividends' amounts.</summary>
    public ImmutableArray<double> Dividends => [.. dividends];

    /// <inheritdoc/>
    public override void ApplyTo(Span<double> a, double t)
    {
        // std::find, by ==
        int found = -1;
        for (int i = 0; i < dividendTimes.Length; ++i)
        {
            if (dividendTimes[i] == t)
            {
                found = i;
                break;
            }
        }

        if (found < 0)
        {
            return;
        }

        double dividend = dividends[found];
        a.CopyTo(aCopy);

        if (mesher.Layout.Dim.Length == 1)
        {
            SetSlopes(aCopy);
            for (int k = 0; k < x.Length; ++k)
            {
                a[k] = Interpolate(aCopy, Max(x[0], x[k] - dividend));
            }
        }
        else
        {
            // every line along the equity direction, each starting at a point whose equity coordinate is 0; QuantLib
            // walks only those through the other directions' first points
            FdmLinearOpLayout layout = mesher.Layout;
            int xSpacing = layout.Spacing[equityDirection];
            int xDim = layout.Dim[equityDirection];

            for (int start = 0; start < layout.Size; ++start)
            {
                if ((start / xSpacing) % xDim != 0)
                {
                    continue;
                }

                for (int k = 0; k < x.Length; ++k)
                {
                    line[k] = aCopy[start + (k * xSpacing)];
                }

                SetSlopes(line);
                for (int k = 0; k < x.Length; ++k)
                {
                    a[start + (k * xSpacing)] = Interpolate(line, Max(x[0], x[k] - dividend));
                }
            }
        }
    }

    // LinearInterpolation's update(): the slope of each interval
    private void SetSlopes(ReadOnlySpan<double> y)
    {
        for (int i = 1; i < x.Length; ++i)
        {
            double dx = x[i] - x[i - 1];
            slopes[i - 1] = (y[i] - y[i - 1]) / dx;
        }
    }

    // LinearInterpolation's value(x), extrapolating: the interval located as Interpolation::locate does
    private double Interpolate(ReadOnlySpan<double> y, double at)
    {
        int i;
        if (at < x[0])
        {
            i = 0;
        }
        else if (at > x[^1])
        {
            i = x.Length - 2;
        }
        else
        {
            // std::upper_bound over all but the last abscissa
            int first = 0, count = x.Length - 1;
            while (count > 0)
            {
                int step = count / 2;
                int it = first + step;
                if (!(at < x[it]))
                {
                    first = it + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }

            i = first - 1;
        }

        return y[i] + ((at - x[i]) * slopes[i]);
    }

    // std::max(a, b)
    private static double Max(double a, double b) => a < b ? b : a;
}
