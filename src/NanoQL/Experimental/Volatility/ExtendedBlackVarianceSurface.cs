// SPDX-FileCopyrightText: Copyright (C) 2008 Frank Hövermann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/extendedblackvariancesurface.hpp ql/experimental/volatility/extendedblackvariancesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A Black variance surface on volatilities at dates and strikes: the variance interpolated in time and strike
/// (bilinearly by default) up to the last date, the last date's volatility flat beyond it. QuantLib's extension of
/// <see cref="BlackVarianceSurface"/> to quoted volatilities.
/// </summary>
/// <remarks>
/// Quotes are values (the playbook's <c>Quote</c> row), given strike by strike, each strike's at every date, as QuantLib
/// lays out its quotes; the surface is computed at construction and QuantLib's <c>update()</c> isn't ported.
/// <see cref="SetInterpolation{TInterpolator}"/> returns a copy on the new interpolation, where QuantLib changes the
/// surface. Unlike the landed surface, QuantLib's experimental one doesn't floor the interpolated variance at 0, and its
/// first date must be after the reference date. QuantLib holds a reference to its caller's quotes, which the values
/// replace. QuantLib's acyclic visitor isn't ported.
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedBlackVarianceSurface : BlackVarianceTermStructure
{
    private readonly Date maxDate;
    private readonly ImmutableArray<double> strikes;
    private readonly ImmutableArray<double> times;
    private readonly Matrix variances;
    private readonly ExtendedBlackVarianceSurfaceExtrapolation lowerExtrapolation;
    private readonly ExtendedBlackVarianceSurfaceExtrapolation upperExtrapolation;

    // Written only at construction and on a fresh copy (SetInterpolation).
    private Interpolation2D varianceSurface;

    /// <summary>Initializes a new instance of the <see cref="ExtendedBlackVarianceSurface"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="dates">The dates, after the reference date and increasing.</param>
    /// <param name="strikes">The strikes, increasing.</param>
    /// <param name="volatilities">The volatilities, strike by strike, each strike's at every date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="lowerExtrapolation">How the surface extrapolates below its lowest strike.</param>
    /// <param name="upperExtrapolation">How the surface extrapolates above its highest strike.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An extrapolation isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, there are no dates, the sizes differ, the
    /// first date isn't after the reference date, the dates aren't increasing, or a strike's variance decreases.</exception>
    public ExtendedBlackVarianceSurface(
        Date referenceDate,
        Calendar? calendar,
        IEnumerable<Date> dates,
        IEnumerable<double> strikes,
        IEnumerable<double> volatilities,
        DayCounter dayCounter,
        ExtendedBlackVarianceSurfaceExtrapolation lowerExtrapolation =
            ExtendedBlackVarianceSurfaceExtrapolation.InterpolatorDefaultExtrapolation,
        ExtendedBlackVarianceSurfaceExtrapolation upperExtrapolation =
            ExtendedBlackVarianceSurfaceExtrapolation.InterpolatorDefaultExtrapolation)
        : base(referenceDate, calendar, BusinessDayConvention.Following, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(volatilities);
        EnumArguments.Check(lowerExtrapolation, nameof(lowerExtrapolation));
        EnumArguments.Check(upperExtrapolation, nameof(upperExtrapolation));
        ImmutableArray<Date> d = [.. dates];
        ImmutableArray<double> vols = [.. volatilities];
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
        if (!(d.Length * this.strikes.Length == vols.Length))
        {
            throw new ArgumentException(
                "size mismatch between date vector and vol matrix columns and/or between money-strike vector and vol "
                + "matrix rows",
                nameof(volatilities));
        }

        if (!(d[0] > referenceDate))
        {
            throw new ArgumentException("cannot have dates_[0] <= referenceDate_", nameof(dates));
        }

        var t = new double[d.Length + 1];
        t[0] = 0.0;
        for (int j = 1; j <= d.Length; j++)
        {
            t[j] = TimeFromReference(d[j - 1]);
            if (!(t[j] > t[j - 1]))
            {
                throw new ArgumentException("dates must be sorted unique", nameof(dates));
            }
        }

        times = [.. t];

        // QuantLib's setVariances
        int n = d.Length;
        variances = new Matrix(this.strikes.Length, n + 1);
        for (int i = 0; i < this.strikes.Length; i++)
        {
            variances[i, 0] = 0.0;
        }

        for (int j = 1; j < times.Length; j++)
        {
            for (int i = 0; i < this.strikes.Length; i++)
            {
                double sigma = vols[(i * n) + j - 1];
                variances[i, j] = times[j] * sigma * sigma;
                if (!(variances[i, j] >= variances[i, j - 1]))
                {
                    throw new ArgumentException("variance must be non-decreasing", nameof(volatilities));
                }
            }
        }

        varianceSurface = default(Bilinear).Interpolate(times.AsSpan(), this.strikes.AsSpan(), variances);
    }

    /// <inheritdoc/>
    public override Date MaxDate => maxDate;

    /// <inheritdoc/>
    public override double MinStrike() => strikes[0];

    /// <inheritdoc/>
    public override double MaxStrike() => strikes[^1];

    /// <summary>A copy of the surface interpolating its variances with another interpolator.</summary>
    /// <typeparam name="TInterpolator">The interpolator's type.</typeparam>
    /// <param name="interpolator">The interpolator.</param>
    /// <returns>The copy.</returns>
    public ExtendedBlackVarianceSurface SetInterpolation<TInterpolator>(TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator2D
    {
        Interpolation2D surface = interpolator.Interpolate(times.AsSpan(), strikes.AsSpan(), variances.Clone());
        var copy = (ExtendedBlackVarianceSurface)MemberwiseClone();
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

        if (strike < strikes[0] && lowerExtrapolation == ExtendedBlackVarianceSurfaceExtrapolation.ConstantExtrapolation)
        {
            strike = strikes[0];
        }

        if (strike > strikes[^1] && upperExtrapolation == ExtendedBlackVarianceSurfaceExtrapolation.ConstantExtrapolation)
        {
            strike = strikes[^1];
        }

        return t <= times[^1]
            ? varianceSurface.Evaluate(t, strike, true)
            : varianceSurface.Evaluate(times[^1], strike, true) * t / times[^1];
    }
}
