// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/fixedlocalvolsurface.hpp ql/termstructures/volatility/equityfx/fixedlocalvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A local volatility surface on fixed values: a matrix of local volatilities, one column per time and one row per
/// strike, each time's strikes its own; interpolated in strike at each time, linearly by default, and linearly in time
/// between them, clamped to the first and last times.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: its matrix and strikes are copied when it is built, where QuantLib's surface shares the caller's
/// matrix. <see cref="WithInterpolation{TInterpolator}"/> returns a copy re-interpolated in strike, QuantLib's
/// <c>setInterpolation</c>.
/// </para>
/// <para>
/// A strike outside a time's strikes is held at the nearest one wherever the constant extrapolation is asked for,
/// at a node time as between nodes, where QuantLib's surface clamps between nodes alone and at a node, and at every time
/// before the first, which clamps onto it, extrapolates by the interpolator (<c>fixedlocalvolsurface.cpp:135-137</c>):
/// at the first node, a strike below the strikes read the interpolator's line extended, and a nanosecond later the
/// clamped volatility. nanoql's value at a node is QuantLib's own limit as the time tends to the node. A documented
/// divergence.
/// </para>
/// </remarks>
public sealed class FixedLocalVolSurface : LocalVolTermStructure
{
    private readonly Date maxDate;
    private readonly ImmutableArray<double> times;
    private readonly Matrix localVolMatrix;
    private readonly ImmutableArray<ImmutableArray<double>> strikes;
    private readonly FixedLocalVolSurfaceExtrapolation lowerExtrapolation;
    private readonly FixedLocalVolSurfaceExtrapolation upperExtrapolation;

    // Written only at construction and on a fresh copy (WithInterpolation).
    private ImmutableArray<Interpolation> localVolInterpol;

    /// <summary>Initializes a new instance of the <see cref="FixedLocalVolSurface"/> class on dates, every date on the
    /// same strikes.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The dates, sorted, the first not before the reference date.</param>
    /// <param name="strikes">The strikes, sorted, one per matrix row.</param>
    /// <param name="localVolMatrix">The local volatilities, one row per strike and one column per date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="lowerExtrapolation">How the surface extrapolates below a time's strikes.</param>
    /// <param name="upperExtrapolation">How the surface extrapolates above them.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An extrapolation isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// No dates are given, where QuantLib reads <c>dates.back()</c> of an empty list; the first date is before the
    /// reference date; or the surface's checks fail; with QuantLib's messages.
    /// </exception>
    public FixedLocalVolSurface(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<double> strikes,
        Matrix localVolMatrix,
        DayCounter dayCounter,
        FixedLocalVolSurfaceExtrapolation lowerExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation,
        FixedLocalVolSurfaceExtrapolation upperExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation)
        : base(referenceDate, new NullCalendar(), BusinessDayConvention.Following, dayCounter)
    {
        Date[] ds = NonEmpty(dates, nameof(dates));
        maxDate = ds[^1];
        if (!(ds[0] >= referenceDate))
        {
            throw new ArgumentException("cannot have dates[0] < referenceDate", nameof(dates));
        }

        times = [.. ds.Select(TimeFromReference)];
        this.strikes = Shared(strikes, ds.Length);
        this.localVolMatrix = Copied(localVolMatrix);
        this.lowerExtrapolation = Checked(lowerExtrapolation, nameof(lowerExtrapolation));
        this.upperExtrapolation = Checked(upperExtrapolation, nameof(upperExtrapolation));

        CheckSurface();
        localVolInterpol = Interpolations(default(Linear));
    }

    /// <summary>Initializes a new instance of the <see cref="FixedLocalVolSurface"/> class on times, every time on the
    /// same strikes.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="times">The times, sorted and not negative.</param>
    /// <param name="strikes">The strikes, sorted, one per matrix row.</param>
    /// <param name="localVolMatrix">The local volatilities, one row per strike and one column per time.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="lowerExtrapolation">How the surface extrapolates below a time's strikes.</param>
    /// <param name="upperExtrapolation">How the surface extrapolates above them.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An extrapolation isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// No times are given, where QuantLib reads <c>times.back()</c> of an empty list; the first time is negative; or the
    /// surface's checks fail; with QuantLib's messages.
    /// </exception>
    public FixedLocalVolSurface(
        Date referenceDate,
        IEnumerable<double> times,
        IEnumerable<double> strikes,
        Matrix localVolMatrix,
        DayCounter dayCounter,
        FixedLocalVolSurfaceExtrapolation lowerExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation,
        FixedLocalVolSurfaceExtrapolation upperExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation)
        : base(referenceDate, new NullCalendar(), BusinessDayConvention.Following, dayCounter)
    {
        double[] ts = NonEmpty(times, nameof(times));
        maxDate = YearFractionToDateFunctions.YearFractionToDate(dayCounter, referenceDate, ts[^1]);
        this.times = [.. ts];
        if (!(this.times[0] >= 0))
        {
            throw new ArgumentException("cannot have times[0] < 0", nameof(times));
        }

        this.strikes = Shared(strikes, ts.Length);
        this.localVolMatrix = Copied(localVolMatrix);
        this.lowerExtrapolation = Checked(lowerExtrapolation, nameof(lowerExtrapolation));
        this.upperExtrapolation = Checked(upperExtrapolation, nameof(upperExtrapolation));

        CheckSurface();
        localVolInterpol = Interpolations(default(Linear));
    }

    /// <summary>Initializes a new instance of the <see cref="FixedLocalVolSurface"/> class on times, each time on its
    /// own strikes.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="times">The times, sorted and not negative.</param>
    /// <param name="strikes">Each time's strikes, sorted, one per matrix row.</param>
    /// <param name="localVolMatrix">The local volatilities, one row per strike and one column per time.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="lowerExtrapolation">How the surface extrapolates below a time's strikes.</param>
    /// <param name="upperExtrapolation">How the surface extrapolates above them.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An extrapolation isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// No times are given; the first time is negative; the times and strike vectors differ in number; or the surface's
    /// checks fail; with QuantLib's messages.
    /// </exception>
    public FixedLocalVolSurface(
        Date referenceDate,
        IEnumerable<double> times,
        IEnumerable<IEnumerable<double>> strikes,
        Matrix localVolMatrix,
        DayCounter dayCounter,
        FixedLocalVolSurfaceExtrapolation lowerExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation,
        FixedLocalVolSurfaceExtrapolation upperExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation)
        : base(referenceDate, new NullCalendar(), BusinessDayConvention.Following, dayCounter)
    {
        double[] ts = NonEmpty(times, nameof(times));
        ArgumentNullException.ThrowIfNull(strikes);
        maxDate = YearFractionToDateFunctions.YearFractionToDate(dayCounter, referenceDate, ts[^1]);
        this.times = [.. ts];
        if (!(this.times[0] >= 0))
        {
            throw new ArgumentException("cannot have times[0] < 0", nameof(times));
        }

        ImmutableArray<ImmutableArray<double>> ks = [.. strikes.Select(k => (ImmutableArray<double>)[.. k ?? throw new ArgumentNullException(nameof(strikes))])];
        if (!(ts.Length == ks.Length))
        {
            throw new ArgumentException("need strikes for every time step", nameof(strikes));
        }

        this.strikes = ks;
        this.localVolMatrix = Copied(localVolMatrix);
        this.lowerExtrapolation = Checked(lowerExtrapolation, nameof(lowerExtrapolation));
        this.upperExtrapolation = Checked(upperExtrapolation, nameof(upperExtrapolation));

        CheckSurface();
        localVolInterpol = Interpolations(default(Linear));
    }

    /// <summary>Gets the last time's date.</summary>
    public override Date MaxDate => maxDate;

    /// <summary>Gets the last time.</summary>
    public override double MaxTime => times[^1];

    /// <summary>The last time's lowest strike.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => strikes[^1][0];

    /// <summary>The last time's highest strike.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => strikes[^1][^1];

    /// <summary>A copy of the surface interpolated in strike, at each time, by another interpolator: QuantLib's
    /// <c>setInterpolation</c>.</summary>
    /// <typeparam name="TInterpolator">The interpolator.</typeparam>
    /// <param name="interpolator">The interpolator's settings.</param>
    /// <returns>A new surface; this one is unchanged.</returns>
    /// <exception cref="ArgumentException">The interpolator can't interpolate a time's strikes.</exception>
    public FixedLocalVolSurface WithInterpolation<TInterpolator>(TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator
    {
        ImmutableArray<Interpolation> interpolations = Interpolations(interpolator);
        var copy = (FixedLocalVolSurface)MemberwiseClone();
        copy.localVolInterpol = interpolations;
        return copy;
    }

    /// <summary>The local volatility at a time, clamped to the surface's times, and a strike.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The local volatility.</returns>
    protected override double LocalVolImpl(double t, double strike)
    {
        t = Math.Min(times[^1], Math.Max(t, times[0]));

        int idx = LowerBound(t);

        if (ComparisonFunctions.CloseEnough(t, times[idx]))
        {
            if (strikes[idx][0] < strikes[idx][^1])
            {
                // the constant extrapolation at a node too
                return localVolInterpol[idx].Evaluate(Clamped(strike, strikes[idx]), true);
            }
            else
            {
                return localVolMatrix[localVolMatrix.Rows / 2, idx];
            }
        }
        else
        {
            double earlierStrike = Clamped(strike, strikes[idx - 1]), laterStrike = Clamped(strike, strikes[idx]);

            double earlyVol = strikes[idx - 1][0] < strikes[idx - 1][^1]
                ? localVolInterpol[idx - 1].Evaluate(earlierStrike, true)
                : localVolMatrix[localVolMatrix.Rows / 2, idx - 1];
            double laterVol = localVolInterpol[idx].Evaluate(laterStrike, true);

            return earlyVol + ((laterVol - earlyVol) / (times[idx] - times[idx - 1]) * (t - times[idx - 1]));
        }
    }

    private static T[] NonEmpty<T>(IEnumerable<T> values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        T[] array = [.. values];
        if (array.Length == 0)
        {
            throw new ArgumentException("at least one time is required", paramName);
        }

        return array;
    }

    private static ImmutableArray<ImmutableArray<double>> Shared(IEnumerable<double> strikes, int n)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        ImmutableArray<double> ks = [.. strikes];
        return [.. Enumerable.Repeat(ks, n)];
    }

    private static Matrix Copied(Matrix localVolMatrix)
    {
        ArgumentNullException.ThrowIfNull(localVolMatrix);
        return localVolMatrix.Clone();
    }

    private static FixedLocalVolSurfaceExtrapolation Checked(FixedLocalVolSurfaceExtrapolation e, string paramName)
    {
        EnumArguments.Check(e, paramName);
        return e;
    }

    // a strike held at a time's nearest strike where the constant extrapolation is asked for
    private double Clamped(double strike, ImmutableArray<double> ks)
    {
        if (lowerExtrapolation == FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation && strike < ks[0])
        {
            return ks[0];
        }

        if (upperExtrapolation == FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation && strike > ks[^1])
        {
            return ks[^1];
        }

        return strike;
    }

    private void CheckSurface()
    {
        if (!(times.Length == localVolMatrix.Columns))
        {
            throw new ArgumentException("mismatch between date vector and vol matrix colums", "localVolMatrix");
        }

        foreach (ImmutableArray<double> strike in strikes)
        {
            if (!(strike.Length == localVolMatrix.Rows))
            {
                throw new ArgumentException("mismatch between money-strike vector and vol matrix rows", "localVolMatrix");
            }
        }

        for (int j = 1; j < times.Length; j++)
        {
            if (!(times[j] > times[j - 1]))
            {
                throw new ArgumentException("dates must be sorted unique!", "times");
            }
        }

        foreach (ImmutableArray<double> strike in strikes)
        {
            for (int j = 1; j < strike.Length; j++)
            {
                if (!(strike[j] >= strike[j - 1]))
                {
                    throw new ArgumentException("strikes must be sorted", "strikes");
                }
            }
        }
    }

    private ImmutableArray<Interpolation> Interpolations<TInterpolator>(TInterpolator interpolator)
        where TInterpolator : struct, IInterpolator
    {
        var result = ImmutableArray.CreateBuilder<Interpolation>(times.Length);
        var column = new double[localVolMatrix.Rows];
        for (int j = 0; j < times.Length; ++j)
        {
            for (int i = 0; i < column.Length; ++i)
            {
                column[i] = localVolMatrix[i, j];
            }

            result.Add(interpolator.Interpolate(strikes[j].AsSpan(), column));
        }

        return result.MoveToImmutable();
    }

    // std::lower_bound: the first time not below t
    private int LowerBound(double t)
    {
        int lo = 0, hi = times.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (times[mid] < t)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
