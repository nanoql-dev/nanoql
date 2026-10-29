// SPDX-FileCopyrightText: Copyright (C) 2008 Frank Hövermann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/extendedblackvariancecurve.hpp ql/experimental/volatility/extendedblackvariancecurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A Black variance curve on volatilities at dates: the variance interpolated in time (linearly by default) up to the
/// last date, the last date's volatility flat beyond it. QuantLib's extension of <see cref="BlackVarianceCurve"/> to
/// quoted volatilities.
/// </summary>
/// <remarks>
/// Quotes are values (the playbook's <c>Quote</c> row), so the curve is computed at construction; QuantLib's
/// <c>update()</c> isn't ported. <see cref="SetInterpolation{TInterpolator}"/> returns a copy on the new interpolation,
/// where QuantLib changes the curve. Unlike the landed curve, QuantLib's experimental one doesn't floor the interpolated
/// variance at 0 nor choose its extrapolation. QuantLib's acyclic visitor isn't ported.
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedBlackVarianceCurve : BlackVarianceTermStructure
{
    private readonly Date maxDate;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> variances;

    // Written only at construction and on a fresh copy (SetInterpolation).
    private Interpolation varianceCurve;

    /// <summary>Initializes a new instance of the <see cref="ExtendedBlackVarianceCurve"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The dates, after the reference date and increasing.</param>
    /// <param name="volatilities">The volatilities, one per date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="forceMonotoneVariance">Whether the variances must be non-decreasing.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, there are no dates, the sizes differ ("size
    /// mismatch between dates and volatilities"), the first date isn't after the reference date, the dates aren't
    /// increasing, or a variance decreases where monotone variances are forced.</exception>
    public ExtendedBlackVarianceCurve(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<double> volatilities,
        DayCounter dayCounter,
        bool forceMonotoneVariance = true)
        : base(referenceDate, null, BusinessDayConvention.Following, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(volatilities);
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
        if (!(d.Length == vols.Length))
        {
            throw new ArgumentException("size mismatch between dates and volatilities", nameof(volatilities));
        }

        if (!(d[0] > referenceDate))
        {
            throw new ArgumentException("cannot have dates_[0] <= referenceDate", nameof(dates));
        }

        var t = new double[d.Length + 1];
        t[0] = 0.0;
        for (int j = 1; j <= d.Length; ++j)
        {
            t[j] = TimeFromReference(d[j - 1]);
            if (!(t[j] > t[j - 1]))
            {
                throw new ArgumentException("dates must be sorted unique!", nameof(dates));
            }
        }

        // QuantLib's setVariances
        var v = new double[d.Length + 1];
        v[0] = 0.0;
        for (int j = 1; j <= vols.Length; j++)
        {
            double sigma = vols[j - 1];
            v[j] = t[j] * sigma * sigma;
            if (!(v[j] >= v[j - 1] || !forceMonotoneVariance))
            {
                throw new ArgumentException("variance must be non-decreasing", nameof(volatilities));
            }
        }

        times = [.. t];
        variances = [.. v];
        varianceCurve = default(Linear).Interpolate(times.AsSpan(), variances.AsSpan());
    }

    /// <inheritdoc/>
    public override Date MaxDate => maxDate;

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>A copy of the curve interpolating its variances with another interpolator.</summary>
    /// <typeparam name="TInterpolator">The interpolator's type.</typeparam>
    /// <param name="interpolator">The interpolator.</param>
    /// <returns>The copy.</returns>
    public ExtendedBlackVarianceCurve SetInterpolation<TInterpolator>(TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator
    {
        Interpolation curve = interpolator.Interpolate(times.AsSpan(), variances.AsSpan());
        var copy = (ExtendedBlackVarianceCurve)MemberwiseClone();
        copy.varianceCurve = curve;
        return copy;
    }

    /// <inheritdoc/>
    protected override double BlackVarianceImpl(double t, double strike) =>
        t <= times[^1]
            ? varianceCurve.Evaluate(t, true)
            : varianceCurve.Evaluate(times[^1], true) * t / times[^1];
}
