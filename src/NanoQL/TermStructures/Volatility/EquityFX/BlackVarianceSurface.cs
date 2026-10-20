// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvariancesurface.hpp ql/termstructures/volatility/equityfx/blackvariancesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A Black volatility surface modelled as a variance surface: a matrix of the market's Black volatilities by strike
/// and date, interpolated on their variances, bilinearly by default.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SetInterpolation{TInterpolator}"/> returns a copy re-interpolated with another interpolator; the surface
/// it is called on is unchanged, where QuantLib's re-interpolates the surface itself and notifies its observers.
/// </para>
/// <para>
/// The variance is floored at zero. QuantLib extrapolates the bilinear variance linearly in strike with no floor, so
/// beyond the strikes it can read a negative variance and a NaN volatility, where its variance curve and its time
/// extrapolation floor theirs at zero (<c>blackvariancesurface.cpp:79-83</c>, <c>blackvariancecurve.cpp:68</c>); a
/// documented divergence. Inside the grid the interpolated variances are QuantLib's.
/// </para>
/// </remarks>
public sealed class BlackVarianceSurface : BlackVarianceTermStructure
{
    private readonly Date maxDate;
    private readonly ImmutableArray<double> strikes;
    private readonly ImmutableArray<double> times;

    // A private copy, never exposed.
    private readonly Matrix variances;
    private readonly BlackVarianceSurfaceExtrapolation lowerExtrapolation;
    private readonly BlackVarianceSurfaceExtrapolation upperExtrapolation;

    // Written only at construction and on a fresh copy (SetInterpolation).
    private Interpolation2D varianceSurface;

    /// <summary>Initializes a new instance of the <see cref="BlackVarianceSurface"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="dates">The dates of the matrix's columns, sorted, the first not before the reference date.</param>
    /// <param name="strikes">The strikes of the matrix's rows.</param>
    /// <param name="blackVolMatrix">The Black volatilities, one row per strike and one column per date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="lowerExtrapolation">How the surface extrapolates below its lowest strike.</param>
    /// <param name="upperExtrapolation">How the surface extrapolates above its highest strike.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dates"/>, <paramref name="strikes"/>, <paramref name="blackVolMatrix"/> or
    /// <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">An extrapolation isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// No dates are given; the matrix's size doesn't match the dates and strikes; the first date is before the
    /// reference date; the dates aren't sorted and unique; or fewer than two strikes are given.
    /// </exception>
    /// <remarks>
    /// QuantLib reads <c>dates.back()</c> of an empty list before its checks (<c>blackvariancesurface.cpp:36</c>),
    /// which is undefined; nanoql refuses no dates first.
    /// </remarks>
    public BlackVarianceSurface(
        Date referenceDate,
        Calendar? calendar,
        IEnumerable<Date> dates,
        IEnumerable<double> strikes,
        Matrix blackVolMatrix,
        DayCounter dayCounter,
        BlackVarianceSurfaceExtrapolation lowerExtrapolation =
            BlackVarianceSurfaceExtrapolation.InterpolatorDefaultExtrapolation,
        BlackVarianceSurfaceExtrapolation upperExtrapolation =
            BlackVarianceSurfaceExtrapolation.InterpolatorDefaultExtrapolation)
        : base(referenceDate, calendar, BusinessDayConvention.Following, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(blackVolMatrix);
        EnumArguments.Check(lowerExtrapolation, nameof(lowerExtrapolation));
        EnumArguments.Check(upperExtrapolation, nameof(upperExtrapolation));
        ImmutableArray<Date> d = [.. dates];
        foreach (Date date in d)
        {
            Date.ThrowIfDefault(date, nameof(dates));
        }

        if (d.IsEmpty)
        {
            throw new ArgumentException("no dates given", nameof(dates));
        }

        maxDate = d[^1];
        this.strikes = [.. strikes];
        this.lowerExtrapolation = lowerExtrapolation;
        this.upperExtrapolation = upperExtrapolation;

        if (!(d.Length == blackVolMatrix.Columns))
        {
            throw new ArgumentException("mismatch between date vector and vol matrix colums", nameof(blackVolMatrix));
        }

        if (!(this.strikes.Length == blackVolMatrix.Rows))
        {
            throw new ArgumentException(
                "mismatch between money-strike vector and vol matrix rows", nameof(blackVolMatrix));
        }

        if (!(d[0] >= referenceDate))
        {
            throw new ArgumentException("cannot have dates[0] < referenceDate", nameof(dates));
        }

        int j, i;
        var t = new double[d.Length + 1];
        t[0] = 0.0;
        variances = new Matrix(this.strikes.Length, d.Length + 1);
        for (i = 0; i < blackVolMatrix.Rows; i++)
        {
            variances[i, 0] = 0.0;
        }

        for (j = 1; j <= blackVolMatrix.Columns; j++)
        {
            t[j] = TimeFromReference(d[j - 1]);
            if (!(t[j] > t[j - 1]))
            {
                throw new ArgumentException("dates must be sorted unique!", nameof(dates));
            }

            for (i = 0; i < blackVolMatrix.Rows; i++)
            {
                variances[i, j] = t[j] * blackVolMatrix[i, j - 1] * blackVolMatrix[i, j - 1];
            }
        }

        times = [.. t];

        // default: bilinear interpolation
        varianceSurface = default(Bilinear).Interpolate(times.AsSpan(), this.strikes.AsSpan(), variances);
    }

    /// <inheritdoc/>
    public override Date MaxDate => maxDate;

    /// <inheritdoc/>
    public override double MinStrike() => strikes[0];

    /// <inheritdoc/>
    public override double MaxStrike() => strikes[^1];

    /// <summary>A copy of the surface whose variances are interpolated by another 2-D interpolator.</summary>
    /// <typeparam name="TInterpolator">The 2-D interpolator.</typeparam>
    /// <param name="interpolator">The interpolator's settings.</param>
    /// <returns>A new surface; this one is unchanged.</returns>
    /// <exception cref="ArgumentException">The interpolator can't interpolate the surface's points.</exception>
    public BlackVarianceSurface SetInterpolation<TInterpolator>(TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator2D
    {
        Interpolation2D surface = interpolator.Interpolate(times.AsSpan(), strikes.AsSpan(), variances.Clone());
        var copy = (BlackVarianceSurface)MemberwiseClone();
        copy.varianceSurface = surface;
        return copy;
    }

    /// <inheritdoc/>
    protected override double BlackVarianceImpl(double t, double strike)
    {
        if (t == 0.0)
        {
            return 0.0;
        }

        // enforce constant extrapolation when required
        if (strike < strikes[0] && lowerExtrapolation == BlackVarianceSurfaceExtrapolation.ConstantExtrapolation)
        {
            strike = strikes[0];
        }

        if (strike > strikes[^1] && upperExtrapolation == BlackVarianceSurfaceExtrapolation.ConstantExtrapolation)
        {
            strike = strikes[^1];
        }

        if (t <= times[^1])
        {
            return Max0(varianceSurface.Evaluate(t, strike, true));
        }
        else
        {
            // t>times_.back() || extrapolate
            return Max0(varianceSurface.Evaluate(times[^1], strike, true)) * t / times[^1];
        }
    }

    // std::max(x, 0.0), which keeps a NaN.
    private static double Max0(double x) => x < 0.0 ? 0.0 : x;
}
