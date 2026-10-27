// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/defaulttermstructure.hpp ql/termstructures/defaulttermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The default-probability term structure: the base of the credit curves that give survival and default
/// probabilities, default densities and hazard rates.
/// </summary>
/// <remarks>
/// <para>
/// An extension point. Every built structure has a fixed reference date (D15): QuantLib's constructor that takes
/// neither a reference date nor settlement days is the reference-date form, and the settlement-days form takes the
/// reference date its concrete class's <c>Create</c> computed from the evaluation context.
/// </para>
/// <para>
/// QuantLib's jumps are quotes; here they are values (PR6.8), each multiplying the survival probabilities past its
/// date, and each checked to lie in (0, 1] when a probability reads it, with QuantLib's "invalid … jump value".
/// </para>
/// </remarks>
public abstract class DefaultProbabilityTermStructure : TermStructure
{
    private readonly ImmutableArray<double> jumps;
    private readonly ImmutableArray<Date> jumpDates;
    private readonly ImmutableArray<double> jumpTimes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultProbabilityTermStructure"/> class with a fixed reference
    /// date.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar used for reference and option date calculation, if any.</param>
    /// <param name="dc">The day counter used for date/time conversion.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <param name="jumps">
    /// The survival-probability jumps, each applied to the probabilities past its date; none when
    /// <see langword="null"/>.
    /// </param>
    /// <param name="jumpDates">
    /// The jumps' dates, one per jump; when <see langword="null"/> or empty, the jumps fall on 31 December of the
    /// reference date's year and of the years after it, one per jump.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Jump dates are given and their number differs from the jumps', or one of them is <c>default(Date)</c>.
    /// </exception>
    protected DefaultProbabilityTermStructure(
        Date referenceDate,
        Calendar? cal,
        DayCounter dc,
        int? settlementDays = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
        : base(referenceDate, cal, dc, settlementDays)
    {
        this.jumps = jumps is null ? [] : [.. jumps];
        ImmutableArray<Date> dates = jumpDates is null ? [] : [.. jumpDates];
        foreach (Date d in dates)
        {
            Date.ThrowIfDefault(d, nameof(jumpDates));
        }

        // setJumps
        int nJumps = this.jumps.Length;
        if (dates.IsEmpty && nJumps != 0)
        {
            // turn of year dates
            int y = ReferenceDate.Year;
            var builder = ImmutableArray.CreateBuilder<Date>(nJumps);
            for (int i = 0; i < nJumps; ++i)
            {
                builder.Add(new Date(31, Month.December, y + i));
            }

            dates = builder.MoveToImmutable();
        }
        else if (!(dates.Length == nJumps))
        {
            // fixed dates
            throw new ArgumentException(
                $"mismatch between number of jumps ({nJumps}) and jump dates ({dates.Length})", nameof(jumpDates));
        }

        this.jumpDates = dates;
        var times = ImmutableArray.CreateBuilder<double>(nJumps);
        foreach (Date d in dates)
        {
            times.Add(TimeFromReference(d));
        }

        jumpTimes = times.MoveToImmutable();
    }

    /// <summary>The probability of survival from the reference date to a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of the curve's range, or a jump before it isn't in
    /// (0, 1].</exception>
    public double SurvivalProbability(Date d, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return SurvivalProbability(TimeFromReference(d), extrapolate);
    }

    /// <summary>
    /// The probability of survival from the reference date to a time, measured by the curve's day counter.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The time is out of the curve's range, or a jump before it isn't in
    /// (0, 1].</exception>
    public double SurvivalProbability(double t, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        if (!jumps.IsEmpty)
        {
            double jumpEffect = 1.0;
            for (int i = 0; i < jumps.Length; ++i)
            {
                if (jumpTimes[i] > 0 && jumpTimes[i] < t)
                {
                    double thisJump = jumps[i];
                    if (!(thisJump > 0.0 && thisJump <= 1.0))
                    {
                        throw new NanoQLException(
                            $"invalid {DataFormattersFunctions.Ordinal(i + 1)} jump value: "
                            + thisJump.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }

                    jumpEffect *= thisJump;
                }
            }

            return jumpEffect * SurvivalProbabilityImpl(t);
        }

        return SurvivalProbabilityImpl(t);
    }

    /// <summary>The probability of default from the reference date to a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of the curve's range, or a jump before it isn't in
    /// (0, 1].</exception>
    public double DefaultProbability(Date d, bool extrapolate = false) => 1.0 - SurvivalProbability(d, extrapolate);

    /// <summary>The probability of default from the reference date to a time.</summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The time is out of the curve's range, or a jump before it isn't in
    /// (0, 1].</exception>
    public double DefaultProbability(double t, bool extrapolate = false) => 1.0 - SurvivalProbability(t, extrapolate);

    /// <summary>The probability of default between two dates.</summary>
    /// <param name="d1">The initial date, which may precede the reference date: the probability of default before it
    /// is taken as null, so that a protection rolled to the next business day still starts at its date.</param>
    /// <param name="d2">The final date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or <paramref name="d1"/> is after
    /// <paramref name="d2"/>.</exception>
    /// <exception cref="NanoQLException">A date is out of the curve's range, or a jump isn't in (0, 1].</exception>
    public double DefaultProbability(Date d1, Date d2, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d1, nameof(d1));
        Date.ThrowIfDefault(d2, nameof(d2));
        if (!(d1 <= d2))
        {
            throw new ArgumentException($"initial date ({d1}) later than final date ({d2})", nameof(d1));
        }

        // We allow d1 to precede the reference date, which effectively assumes
        // that the default probability before the reference date is null. This
        // helps in cases where a coupon protection starts a couple of days before
        // the reference date due to date adjustments (for instance, when the
        // protection starts on a Saturday and the reference is rolled to the
        // following Monday).
        double p1 = d1 < ReferenceDate ? 0.0 : DefaultProbability(d1, extrapolate),
            p2 = DefaultProbability(d2, extrapolate);
        return p2 - p1;
    }

    // The survival the curve has accrued by a date: 1 before its reference date, where QuantLib's read throws ("negative
    // time"). No default before the reference date is QuantLib's own premise for its two-date default probability,
    // which excuses its first date; the CDS engines read every survival and default probability through these (the
    // S-core plan's CR7 (xxxiv)).
    internal double AccruedSurvivalProbability(Date d) => d < ReferenceDate ? 1.0 : SurvivalProbability(d);

    // The default probability accrued by a date: 0 before the reference date.
    internal double AccruedDefaultProbability(Date d) => d < ReferenceDate ? 0.0 : DefaultProbability(d);

    // The default probability between two dates, the second as the first is excused: 0 where it precedes the reference
    // date, the earlier one then preceding it too; QuantLib's two-date form otherwise.
    internal double AccruedDefaultProbability(Date d1, Date d2) => d2 < ReferenceDate ? 0.0 : DefaultProbability(d1, d2);

    /// <summary>The probability of default between two times.</summary>
    /// <param name="t1">The initial time, which may be negative: the probability of default before the reference date
    /// is taken as null.</param>
    /// <param name="t2">The final time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException"><paramref name="t1"/> is after <paramref name="t2"/>.</exception>
    /// <exception cref="NanoQLException">A time is out of the curve's range, or a jump isn't in (0, 1].</exception>
    public double DefaultProbability(double t1, double t2, bool extrapolate = false)
    {
        if (!(t1 <= t2))
        {
            throw new ArgumentException(
                $"initial time ({Show(t1)}) later than final time ({Show(t2)})", nameof(t1));
        }

        // We allow t1 to precede 0.0, which effectively assumes that the
        // default probability before the reference time is null. This helps in
        // cases where a coupon protection starts a couple of days before the
        // reference date due to date adjustments.
        double p1 = t1 < 0.0 ? 0.0 : DefaultProbability(t1, extrapolate),
            p2 = DefaultProbability(t2, extrapolate);
        return p2 - p1;
    }

    /// <summary>The default density at a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The density.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of the curve's range.</exception>
    public double DefaultDensity(Date d, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return DefaultDensity(TimeFromReference(d), extrapolate);
    }

    /// <summary>The default density at a time.</summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The density.</returns>
    /// <exception cref="NanoQLException">The time is out of the curve's range.</exception>
    public double DefaultDensity(double t, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        return DefaultDensityImpl(t);
    }

    /// <summary>The hazard rate at a date, with annual frequency and continuous compounding.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of the curve's range.</exception>
    public double HazardRate(Date d, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return HazardRate(TimeFromReference(d), extrapolate);
    }

    /// <summary>The hazard rate at a time, with annual frequency and continuous compounding.</summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The time is out of the curve's range.</exception>
    public double HazardRate(double t, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        return HazardRateImpl(t);
    }

    /// <summary>The dates of the survival-probability jumps, one per jump.</summary>
    /// <returns>The dates, empty for a curve without jumps.</returns>
    public virtual ImmutableArray<Date> JumpDates() => jumpDates;

    /// <summary>The times of the survival-probability jumps, measured from the reference date by the curve's day
    /// counter.</summary>
    /// <returns>The times, one per jump, empty for a curve without jumps.</returns>
    public virtual ImmutableArray<double> JumpTimes() => jumpTimes;

    /// <summary>
    /// The survival probability at a time, which derived curves calculate. Range checks have already been performed, so
    /// it must assume that extrapolation is required.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The probability.</returns>
    protected abstract double SurvivalProbabilityImpl(double t);

    /// <summary>
    /// The default density at a time, which derived curves calculate. Range checks have already been performed, so it
    /// must assume that extrapolation is required.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    protected abstract double DefaultDensityImpl(double t);

    /// <summary>
    /// The hazard rate at a time: by default the default density over the survival probability, 0 where the
    /// probability is. Derived curves may override it with a more efficient calculation.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The rate.</returns>
    protected virtual double HazardRateImpl(double t)
    {
        double s = SurvivalProbability(t, true);
        return s == 0.0 ? 0.0 : DefaultDensity(t, true) / s;
    }

    private static string Show(double t) => t.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
