// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2026 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvariancecurve.hpp ql/termstructures/volatility/equityfx/blackvariancecurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A Black volatility curve modelled as a variance curve: the market's at-the-money Black volatilities at a set of
/// dates, interpolated on their variances, linearly by default. For strike dependence, see
/// <see cref="BlackVarianceSurface"/>.
/// </summary>
/// <remarks>
/// <see cref="SetInterpolation{TInterpolator}"/> returns a copy re-interpolated with another interpolator; the curve it
/// is called on is unchanged, where QuantLib's re-interpolates the curve itself and notifies its observers.
/// </remarks>
public sealed class BlackVarianceCurve : BlackVarianceTermStructure
{
    private readonly Date maxDate;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> variances;
    private readonly BlackVolTimeExtrapolationType timeExtrapolationType;

    // Written only at construction and on a fresh copy (SetInterpolation).
    private Interpolation varianceCurve;

    /// <summary>Initializes a new instance of the <see cref="BlackVarianceCurve"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The dates of the volatilities, after the reference date and sorted.</param>
    /// <param name="blackVolCurve">The at-the-money Black volatility at each date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="forceMonotoneVariance">Whether the variances must be non-decreasing.</param>
    /// <param name="timeExtrapolationType">How the curve extrapolates beyond its last date.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dates"/>, <paramref name="blackVolCurve"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeExtrapolationType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">
    /// No dates are given; the dates and volatilities differ in number; the first date isn't after the reference date;
    /// the dates aren't sorted and unique; or a forced monotone variance decreases.
    /// </exception>
    /// <remarks>
    /// QuantLib reads <c>dates.back()</c> of an empty list before its checks (<c>blackvariancecurve.cpp:35</c>), which
    /// is undefined; nanoql refuses no dates first.
    /// </remarks>
    public BlackVarianceCurve(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<double> blackVolCurve,
        DayCounter dayCounter,
        bool forceMonotoneVariance = true,
        BlackVolTimeExtrapolationType timeExtrapolationType = BlackVolTimeExtrapolationType.FlatVolatility)
        : base(referenceDate, null, BusinessDayConvention.Following, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(blackVolCurve);
        EnumArguments.Check(timeExtrapolationType, nameof(timeExtrapolationType));
        ImmutableArray<Date> d = [.. dates];
        ImmutableArray<double> vols = [.. blackVolCurve];
        foreach (Date date in d)
        {
            Date.ThrowIfDefault(date, nameof(dates));
        }

        if (d.IsEmpty)
        {
            throw new ArgumentException("no dates given", nameof(dates));
        }

        maxDate = d[^1];
        this.timeExtrapolationType = timeExtrapolationType;

        if (!(d.Length == vols.Length))
        {
            throw new ArgumentException("mismatch between date vector and black vol vector", nameof(blackVolCurve));
        }

        // cannot have dates[0]==referenceDate, since the
        // value of the vol at dates[0] would be lost
        // (variance at referenceDate must be zero)
        if (!(d[0] > referenceDate))
        {
            throw new ArgumentException("cannot have dates[0] <= referenceDate", nameof(dates));
        }

        var v = new double[d.Length + 1];
        var t = new double[d.Length + 1];
        v[0] = 0.0;
        t[0] = 0.0;
        for (int j = 1; j <= vols.Length; j++)
        {
            t[j] = TimeFromReference(d[j - 1]);
            if (!(t[j] > t[j - 1]))
            {
                throw new ArgumentException("dates must be sorted unique!", nameof(dates));
            }

            v[j] = t[j] * vols[j - 1] * vols[j - 1];
            if (!(v[j] >= v[j - 1] || !forceMonotoneVariance))
            {
                throw new ArgumentException("variance must be non-decreasing", nameof(blackVolCurve));
            }
        }

        times = [.. t];
        variances = [.. v];

        // default: linear interpolation
        varianceCurve = default(Linear).Interpolate(times.AsSpan(), variances.AsSpan());
    }

    /// <inheritdoc/>
    public override Date MaxDate => maxDate;

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>A copy of the curve whose variances are interpolated by another interpolator.</summary>
    /// <typeparam name="TInterpolator">The interpolator.</typeparam>
    /// <param name="interpolator">The interpolator's settings.</param>
    /// <returns>A new curve; this one is unchanged.</returns>
    /// <exception cref="ArgumentException">The interpolator can't interpolate the curve's points.</exception>
    public BlackVarianceCurve SetInterpolation<TInterpolator>(TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator
    {
        Interpolation curve = interpolator.Interpolate(times.AsSpan(), variances.AsSpan());
        var copy = (BlackVarianceCurve)MemberwiseClone();
        copy.varianceCurve = curve;
        return copy;
    }

    /// <inheritdoc/>
    protected override double BlackVarianceImpl(double t, double strike)
    {
        if (t <= times[^1])
        {
            return Max0(varianceCurve.Evaluate(t, true));
        }
        else
        {
            Interpolation curve = varianceCurve;
            return BlackVolTimeExtrapolation.ExtrapolatedVariance(
                timeExtrapolationType, t, times.AsSpan(), x => curve.Evaluate(x, true));
        }
    }

    // std::max(x, 0.0), which keeps a NaN.
    private static double Max0(double x) => x < 0.0 ? 0.0 : x;
}
