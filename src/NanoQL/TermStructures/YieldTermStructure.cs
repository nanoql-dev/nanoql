// SPDX-FileCopyrightText: Copyright (C) 2004, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yieldtermstructure.hpp ql/termstructures/yieldtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructors taking a day counter only, for structures that manage their own reference date, which no curve ported so far uses

using System.Collections.Immutable;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The interest-rate term structure: the base of the curves that give discount factors, zero rates and forward rates.
/// </summary>
public abstract class YieldTermStructure : TermStructure
{
    // time interval used in finite differences
    private const double Dt = 0.0001;

    private readonly ImmutableArray<double> jumps;
    private readonly ImmutableArray<Date> jumpDates;
    private readonly ImmutableArray<double> jumpTimes;

    /// <summary>Initializes a new instance of the <see cref="YieldTermStructure"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar used for reference and option date calculation, if any.</param>
    /// <param name="dc">The day counter used for date/time conversion.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <param name="jumps">
    /// The discount-factor jumps, each applied to the discount factors past its date; none when <see langword="null"/>.
    /// </param>
    /// <param name="jumpDates">
    /// The jumps' dates, one per jump; when <see langword="null"/> or empty, the jumps fall on 31 December of the
    /// reference date's year and of the years after it, one per jump.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Jump dates are given and their number differs from the jumps', or one of them is <c>default(Date)</c>.
    /// </exception>
    protected YieldTermStructure(
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

    /// <summary>The discount factor from a date to the reference date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The discount factor.</returns>
    public double Discount(Date d, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return DiscountOn(d, extrapolate);
    }

    /// <summary>
    /// The discount factor from a time to the reference date, the time measured by the curve's day counter.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The discount factor.</returns>
    /// <exception cref="NanoQLException">A jump before the time isn't positive.</exception>
    public double Discount(double t, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        if (jumps.IsEmpty)
        {
            return DiscountImpl(t);
        }

        double jumpEffect = 1.0;
        for (int i = 0; i < jumps.Length; ++i)
        {
            if (jumpTimes[i] > 0 && jumpTimes[i] < t)
            {
                double thisJump = jumps[i];
                if (!(thisJump > 0.0))
                {
                    throw new NanoQLException(
                        $"invalid {DataFormattersFunctions.Ordinal(i + 1)} jump value: "
                        + thisJump.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                jumpEffect *= thisJump;
            }
        }

        return jumpEffect * DiscountImpl(t);
    }

    /// <summary>
    /// The implied zero-yield rate for a date, with the given day-counting rule; the time is calculated as a fraction of
    /// a year from the reference date.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <param name="resultDayCounter">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The zero rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ZeroRate(
        Date d, DayCounter resultDayCounter, Compounding comp, Frequency freq = Frequency.Annual, bool extrapolate = false)
    {
        ArgumentNullException.ThrowIfNull(resultDayCounter);
        Date.ThrowIfDefault(d, nameof(d));
        double t = TimeFromReference(d);
        if (t == 0)
        {
            double compound0 = 1.0 / Discount(Dt, extrapolate);

            // t has been calculated with a possibly different daycounter but the difference should not matter for very
            // small times
            return InterestRate.ImpliedRate(compound0, resultDayCounter, comp, freq, Dt);
        }

        double compound = 1.0 / DiscountOn(d, extrapolate);
        return InterestRate.ImpliedRate(compound, resultDayCounter, comp, freq, ReferenceDate, d);
    }

    /// <summary>The implied zero-yield rate for a time, measured by the curve's day counter.</summary>
    /// <param name="t">The time.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The zero rate, with the curve's day counter.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ZeroRate(double t, Compounding comp, Frequency freq = Frequency.Annual, bool extrapolate = false)
    {
        if (t == 0.0)
        {
            t = Dt;
        }

        double compound = 1.0 / Discount(t, extrapolate);
        return InterestRate.ImpliedRate(compound, DayCounter, comp, freq, t);
    }

    /// <summary>
    /// The forward rate between two dates, with the given day-counting rule; the instantaneous forward rate when the
    /// dates are equal.
    /// </summary>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="resultDayCounter">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The forward rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ForwardRate(
        Date d1,
        Date d2,
        DayCounter resultDayCounter,
        Compounding comp,
        Frequency freq = Frequency.Annual,
        bool extrapolate = false)
    {
        ArgumentNullException.ThrowIfNull(resultDayCounter);
        Date.ThrowIfDefault(d1, nameof(d1));
        Date.ThrowIfDefault(d2, nameof(d2));
        if (d1 == d2)
        {
            CheckRange(d1, extrapolate);
            double t1 = Math.Max(TimeFromReference(d1) - (Dt / 2.0), 0.0);
            double t2 = t1 + Dt;
            double compound0 = Discount(t1, true) / Discount(t2, true);

            // times have been calculated with a possibly different daycounter but the difference should not matter for
            // very small times
            return InterestRate.ImpliedRate(compound0, resultDayCounter, comp, freq, Dt);
        }

        if (!(d1 < d2))
        {
            throw new ArgumentException($"{d1} later than {d2}", nameof(d1));
        }

        double compound = Discount(d1, extrapolate) / Discount(d2, extrapolate);
        return InterestRate.ImpliedRate(compound, resultDayCounter, comp, freq, d1, d2);
    }

    /// <summary>The forward rate between a date and the date a period later. Dates are not adjusted for holidays.</summary>
    /// <param name="d">The start date.</param>
    /// <param name="p">The period.</param>
    /// <param name="resultDayCounter">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The forward rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ForwardRate(
        Date d,
        Period p,
        DayCounter resultDayCounter,
        Compounding comp,
        Frequency freq = Frequency.Annual,
        bool extrapolate = false)
    {
        ArgumentNullException.ThrowIfNull(resultDayCounter);
        Date.ThrowIfDefault(d, nameof(d));
        return ForwardRate(d, d + p, resultDayCounter, comp, freq, extrapolate);
    }

    /// <summary>
    /// The forward rate between two times, measured by the curve's day counter; the instantaneous forward rate when the
    /// times are equal.
    /// </summary>
    /// <param name="t1">The start time.</param>
    /// <param name="t2">The end time.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The forward rate, with the curve's day counter.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ForwardRate(
        double t1, double t2, Compounding comp, Frequency freq = Frequency.Annual, bool extrapolate = false)
    {
        double compound;
        if (t2 == t1)
        {
            CheckRange(t1, extrapolate);
            t1 = Math.Max(t1 - (Dt / 2.0), 0.0);
            t2 = t1 + Dt;
            compound = Discount(t1, true) / Discount(t2, true);
        }
        else
        {
            if (!(t2 > t1))
            {
                throw new ArgumentException(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"t1 ({t1}) >= t2 ({t2})"), nameof(t1));
            }

            compound = Discount(t1, extrapolate) / Discount(t2, extrapolate);
        }

        return InterestRate.ImpliedRate(compound, DayCounter, comp, freq, t2 - t1);
    }

    /// <summary>
    /// The continuously compounded forward rate between two times: <see cref="ForwardRate(double, double, Compounding,
    /// Frequency, bool)"/>'s rate, from the same compound factor by the same arithmetic, without building the
    /// <see cref="InterestRate"/>, so that a finite-difference operator's time step allocates nothing.
    /// </summary>
    /// <param name="t1">The start time.</param>
    /// <param name="t2">The end time, not before <paramref name="t1"/>.</param>
    /// <returns>The forward rate.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, the compound factor isn't positive, or the time isn't.
    /// </exception>
    internal double ContinuousForwardRate(double t1, double t2)
    {
        double compound;
        if (t2 == t1)
        {
            CheckRange(t1, false);
            double shifted = t1 - (Dt / 2.0);
            t1 = shifted < 0.0 ? 0.0 : shifted;
            t2 = t1 + Dt;
            compound = Discount(t1, true) / Discount(t2, true);
        }
        else
        {
            if (!(t2 > t1))
            {
                throw new ArgumentException(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"t1 ({t1}) >= t2 ({t2})"), nameof(t1));
            }

            compound = Discount(t1, false) / Discount(t2, false);
        }

        // InterestRate.ImpliedRate's checks and continuous branch
        double t = t2 - t1;
        if (!(compound > 0.0))
        {
            throw new ArgumentException("positive compound factor required", nameof(t1));
        }

        if (compound == 1.0)
        {
            if (!(t >= 0.0))
            {
                throw new ArgumentException(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"non negative time ({t}) required"),
                    nameof(t1));
            }

            return 0.0;
        }

        if (!(t > 0.0))
        {
            throw new ArgumentException(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"positive time ({t}) required"), nameof(t1));
        }

        return Math.Log(compound) / t;
    }

    // The discount the curve has accrued by a date, 1 before its reference date (TermStructure.AccrualTimeTo), where
    // QuantLib's read throws: Discount on and after that date.
    internal double AccruedDiscount(Date d) => d < ReferenceDate ? 1.0 : Discount(d);

    // The discount the curve has accrued at the instant a time on another structure's clock denotes, from its own
    // reference date (TermStructure.AccrualTime): QuantLib's read where the two share their times.
    internal double DiscountAt(TermStructure clock, double t, bool extrapolate = false) =>
        SharesTimes(clock) ? Discount(t, extrapolate) : Discount(AccrualTime(clock, t), extrapolate);

    // The continuously compounded forward rate between the instants two times on another structure's clock denote, per
    // unit of that clock's time: QuantLib's ContinuousForwardRate where the two share their times; otherwise the log
    // of the accrued discounts' ratio over the clock's interval (TermStructure.AccrualTime), a zero-length interval
    // widened as QuantLib's.
    internal double ContinuousForwardRateAt(TermStructure clock, double t1, double t2, bool extrapolate = false)
    {
        if (SharesTimes(clock))
        {
            return ContinuousForwardRate(t1, t2);
        }

        if (t2 == t1)
        {
            double shifted = t1 - (Dt / 2.0);
            t1 = shifted < 0.0 ? 0.0 : shifted;
            t2 = t1 + Dt;
        }
        else if (!(t2 > t1))
        {
            throw new ArgumentException(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"t1 ({t1}) >= t2 ({t2})"), nameof(t1));
        }

        double compound = Discount(AccrualTime(clock, t1), extrapolate) / Discount(AccrualTime(clock, t2), extrapolate);
        if (!(compound > 0.0))
        {
            throw new ArgumentException("positive compound factor required", nameof(t1));
        }

        return Math.Log(compound) / (t2 - t1);
    }

    // The discount factor at a date, which Discount(Date), ZeroRate(Date, …) and ForwardRate(Date, Date, …) read: the
    // discount at the date's time, QuantLib's read. A curve whose times can't carry its dates reads them by date (the
    // implied curve on a day counter whose year fractions don't add, the S-sr plan's SSR6 (viii)); every other curve
    // keeps this read and its bits.
    internal virtual double DiscountOn(Date d, bool extrapolate) => Discount(TimeFromReference(d), extrapolate);

    /// <summary>The dates of the discount-factor jumps, one per jump.</summary>
    /// <returns>The dates, empty for a curve without jumps.</returns>
    public virtual ImmutableArray<Date> JumpDates() => jumpDates;

    /// <summary>The times of the discount-factor jumps, measured from the reference date by the curve's day counter.</summary>
    /// <returns>The times, one per jump, empty for a curve without jumps.</returns>
    public virtual ImmutableArray<double> JumpTimes() => jumpTimes;

    /// <summary>
    /// The discount factor at a time, which derived curves calculate. Range checks have already been performed, so it
    /// must assume that extrapolation is required.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    protected abstract double DiscountImpl(double t);
}
