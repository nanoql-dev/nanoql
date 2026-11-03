// SPDX-FileCopyrightText: Copyright (C) 2026 Richard Amaya
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/piecewiseblackvariancesurface.hpp ql/termstructures/volatility/equityfx/piecewiseblackvariancesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A Black volatility surface built from smile sections, one per tenor: linear in total variance between tenors at a
/// strike, linear from zero to the first tenor and at the last tenor's volatility past it.
/// </summary>
/// <remarks>
/// Immutable, its sections held as given: QuantLib's surface observes them. A section's variance is read at the
/// section's own exercise time, as QuantLib reads it. Its strike range is unbounded; a strike outside a section's range
/// is refused unless the surface extrapolates.
/// </remarks>
public sealed class PiecewiseBlackVarianceSurface : BlackVarianceTermStructure
{
    private readonly Date maxDate;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<SmileSection> smileSections;

    /// <summary>Initializes a new instance of the <see cref="PiecewiseBlackVarianceSurface"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The tenors' dates, sorted and after the reference date.</param>
    /// <param name="smileSections">The smile sections, one per date.</param>
    /// <param name="dayCounter">The day counter; QuantLib's default, an empty one, refuses every time.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No dates are given; the dates and sections differ in number; the first date isn't after the reference date; the
    /// dates aren't sorted and unique; or a section is <see langword="null"/>; with QuantLib's messages.
    /// </exception>
    public PiecewiseBlackVarianceSurface(
        Date referenceDate, IEnumerable<Date> dates, IEnumerable<SmileSection> smileSections, DayCounter dayCounter)
        : base(referenceDate, null, BusinessDayConvention.Following, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(smileSections);
        Date[] ds = [.. dates];
        SmileSection[] sections = [.. smileSections];

        if (ds.Length == 0)
        {
            throw new ArgumentException("at least one date is required", nameof(dates));
        }

        if (ds.Length != sections.Length)
        {
            throw new ArgumentException(
                Invariant($"mismatch between {ds.Length} dates and {sections.Length} smile sections"), nameof(smileSections));
        }

        maxDate = ds[^1];
        var t = new double[ds.Length];

        t[0] = TimeFromReference(ds[0]);
        if (!(t[0] > 0.0))
        {
            throw new ArgumentException(
                Invariant($"first date ({ds[0]}) must be after reference date ({referenceDate})"), nameof(dates));
        }

        for (int i = 1; i < ds.Length; ++i)
        {
            t[i] = TimeFromReference(ds[i]);
            if (!(t[i] > t[i - 1]))
            {
                throw new ArgumentException(
                    Invariant(
                        $"dates must be sorted and unique, but date {ds[i]} (t={t[i]}) is not after date {ds[i - 1]} (t={t[i - 1]})"),
                    nameof(dates));
            }
        }

        for (int i = 0; i < sections.Length; ++i)
        {
            if (sections[i] is null)
            {
                throw new ArgumentException(Invariant($"null smile section at index {i}"), nameof(smileSections));
            }
        }

        times = [.. t];
        this.smileSections = [.. sections];
    }

    /// <summary>Initializes a new instance of the <see cref="PiecewiseBlackVarianceSurface"/> class on one tenor.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="date">The tenor's date, after the reference date.</param>
    /// <param name="smileSection">The smile section.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The date isn't after the reference date, or the section is
    /// <see langword="null"/>.</exception>
    public PiecewiseBlackVarianceSurface(Date referenceDate, Date date, SmileSection smileSection, DayCounter dayCounter)
        : this(referenceDate, [date], [smileSection], dayCounter)
    {
    }

    /// <summary>Gets the last tenor's date.</summary>
    public override Date MaxDate => maxDate;

    /// <summary>The lowest strike, unbounded: QuantLib's <c>QL_MIN_REAL</c>.</summary>
    /// <returns>−1.7976931348623157e308.</returns>
    public override double MinStrike() => -double.MaxValue;

    /// <summary>The highest strike, unbounded: QuantLib's <c>QL_MAX_REAL</c>.</summary>
    /// <returns>1.7976931348623157e308.</returns>
    public override double MaxStrike() => double.MaxValue;

    /// <summary>
    /// A surface from a rectangular grid of Black volatilities, rows by strike and columns by date: each column a
    /// linearly interpolated smile section with no at-the-money level, as QuantLib's migration path from
    /// <see cref="BlackVarianceSurface"/>.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The tenors' dates, sorted and after the reference date.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="blackVols">The volatilities, one row per strike and one column per date.</param>
    /// <param name="dc">The day counter.</param>
    /// <returns>The surface.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The matrix's size doesn't match the strikes and dates, a date isn't after the reference date, or the sections or
    /// the surface refuse their arguments; with QuantLib's messages.
    /// </exception>
    public static PiecewiseBlackVarianceSurface MakeFromGrid(
        Date referenceDate, IEnumerable<Date> dates, IEnumerable<double> strikes, Matrix blackVols, DayCounter dc)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(blackVols);
        ArgumentNullException.ThrowIfNull(dc);
        Date[] ds = [.. dates];
        double[] ks = [.. strikes];

        if (blackVols.Rows != ks.Length)
        {
            throw new ArgumentException(
                Invariant($"mismatch between {ks.Length} strikes and {blackVols.Rows} matrix rows"), nameof(blackVols));
        }

        if (blackVols.Columns != ds.Length)
        {
            throw new ArgumentException(
                Invariant($"mismatch between {ds.Length} dates and {blackVols.Columns} matrix columns"), nameof(blackVols));
        }

        var sections = new SmileSection[ds.Length];
        var stdDevs = new double[ks.Length];
        for (int j = 0; j < ds.Length; ++j)
        {
            double t = dc.YearFraction(referenceDate, ds[j]);
            if (!(t > 0.0))
            {
                throw new ArgumentException(
                    Invariant($"date {ds[j]} must be after reference date {referenceDate}"), nameof(dates));
            }

            double sqrtT = Math.Sqrt(t);
            for (int i = 0; i < ks.Length; ++i)
            {
                stdDevs[i] = blackVols[i, j] * sqrtT;
            }

            sections[j] = new LinearInterpolatedSmileSection(ds[j], ks, stdDevs, null, dc, referenceDate);
        }

        return new PiecewiseBlackVarianceSurface(referenceDate, ds, sections, dc);
    }

    /// <summary>The variance at a time and strike: linear in the tenors' variances, from zero before the first.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The strike is outside a section's range and the surface doesn't
    /// extrapolate.</exception>
    protected override double BlackVarianceImpl(double t, double strike)
    {
        if (t == 0.0)
        {
            return 0.0;
        }

        if (t <= times[0])
        {
            // linear interpolation from (0, 0) to first tenor
            double var1 = SectionVariance(0, strike);
            return var1 * t / times[0];
        }

        if (t >= times[^1])
        {
            // flat vol extrapolation beyond last tenor
            double varN = SectionVariance(smileSections.Length - 1, strike);
            return varN * t / times[^1];
        }

        // find enclosing interval
        int hi = UpperBound(t);
        int lo = hi - 1;

        double varLo = SectionVariance(lo, strike);
        double varHi = SectionVariance(hi, strike);
        double alpha = (t - times[lo]) / (times[hi] - times[lo]);

        return varLo + ((varHi - varLo) * alpha);
    }

    /// <summary>The section at a tenor's time (within QuantLib's <c>close_enough</c>), and otherwise the surface's
    /// adapter.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The smile section.</returns>
    protected override SmileSection SmileSectionImpl(double t)
    {
        int i = LowerBound(t);
        if (i != times.Length && ComparisonFunctions.CloseEnough(t, times[i]))
        {
            return smileSections[i];
        }

        return base.SmileSectionImpl(t);
    }

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private double SectionVariance(int i, double strike)
    {
        SmileSection s = smileSections[i];
        if (!(AllowsExtrapolation || (strike >= s.MinStrike() && strike <= s.MaxStrike())))
        {
            throw new NanoQLException(
                Invariant($"strike ({strike}) is outside the range of smile section {i} [{s.MinStrike()}, {s.MaxStrike()}]"));
        }

        return s.Variance(strike);
    }

    // std::upper_bound: the first time above t
    private int UpperBound(double t)
    {
        int lo = 0, hi = times.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (times[mid] > t)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
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
