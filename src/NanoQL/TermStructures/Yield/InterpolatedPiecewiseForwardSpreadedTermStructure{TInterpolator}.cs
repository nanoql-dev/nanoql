// SPDX-FileCopyrightText: Copyright (C) 2006 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008, 2014 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Robert Philipp
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewiseforwardspreadedtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A yield curve with spreads added to the instantaneous forward rate of another curve, the original, the spread at
/// any time interpolated between the spreads given at dates and held flat before the first and after the last.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator of the spreads.</typeparam>
/// <remarks>
/// <para>
/// QuantLib's curve derives from <c>RelativeDerivedYieldTermStructure&lt;ZeroYieldStructure&gt;</c>, a class template
/// over its own base, which C# can't express; it implements the template's members itself, as
/// <see cref="ZeroSpreadedTermStructure"/> does. Its reference date and day counter are the original's, read when it
/// is built, and so is its extrapolation flag; its calendar and settlement days are the original's, read when they are
/// asked for, and its maximum date is the earlier of the original's and the last spread's date. The spreads' times are
/// counted from the reference date when the curve is built. Spreads or an original that change make a new curve:
/// QuantLib's curve follows its quotes and its handle. QuantLib's deprecated constructor taking a day counter, which
/// it ignores, isn't ported.
/// </para>
/// <para>
/// The zero rate at a time is the original's, continuously compounded, plus the spread's average from the reference
/// date to that time. QuantLib integrates the interpolation from the first spread's time
/// (<c>piecewiseforwardspreadedtermstructure.hpp:152-166</c>), leaving out the flat spread it applies itself before
/// that time, and reads the interpolation extrapolated backwards before it: a 1% spread from year 1 over a flat 2% curve
/// gives zero rates of 1.0% at six months and 2.75% at four years, where the curve's own forwards are 3% throughout.
/// Here the spread is integrated from the reference date, flat before the first spread's time, so the zero rates are
/// the forwards' averages; with the first spread at the reference date it is QuantLib's integral and bits. A documented
/// divergence.
/// </para>
/// </remarks>
public sealed class InterpolatedPiecewiseForwardSpreadedTermStructure<TInterpolator> : ZeroYieldStructure
    where TInterpolator : struct, IInterpolator
{
    private readonly YieldTermStructure originalCurve;
    private readonly ImmutableArray<Date> dates;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> spreadValues;
    private readonly Interpolation interpolator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpolatedPiecewiseForwardSpreadedTermStructure{TInterpolator}"/>
    /// class.
    /// </summary>
    /// <param name="originalCurve">The curve the spreads are added to.</param>
    /// <param name="spreads">The spreads on the instantaneous forward rate, one per date.</param>
    /// <param name="dates">The spreads' dates, sorted.</param>
    /// <param name="factory">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="originalCurve"/>, <paramref name="spreads"/> or <paramref name="dates"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// No spread is given, the spreads and the dates differ in number, a date is <c>default(Date)</c>, or the
    /// interpolation refuses the spreads.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the spreads.</exception>
    public InterpolatedPiecewiseForwardSpreadedTermStructure(
        YieldTermStructure originalCurve,
        IEnumerable<double> spreads,
        IEnumerable<Date> dates,
        TInterpolator factory = default)
        : base(Original(originalCurve).ReferenceDate, null, originalCurve.DayCounter)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        ArgumentNullException.ThrowIfNull(dates);
        this.originalCurve = originalCurve;
        spreadValues = [.. spreads];
        this.dates = [.. dates];
        foreach (Date d in this.dates)
        {
            Date.ThrowIfDefault(d, nameof(dates));
        }

        if (spreadValues.IsEmpty)
        {
            throw new ArgumentException("no spreads given", nameof(spreads));
        }

        if (!(spreadValues.Length == this.dates.Length))
        {
            throw new ArgumentException("spread and date vector have different sizes", nameof(dates));
        }

        Factory = factory;

        // updateInterpolation
        var t = ImmutableArray.CreateBuilder<double>(this.dates.Length);
        foreach (Date d in this.dates)
        {
            t.Add(TimeFromReference(d));
        }

        times = t.MoveToImmutable();
        interpolator = factory.Interpolate(times.AsSpan(), spreadValues.AsSpan());
        AllowsExtrapolation = originalCurve.AllowsExtrapolation;
    }

    /// <summary>Gets the interpolator the curve was built with.</summary>
    public TInterpolator Factory { get; }

    /// <summary>Gets the original curve's calendar, read when asked for.</summary>
    public override Calendar? Calendar => originalCurve.Calendar;

    /// <summary>Gets the original curve's settlement days, read when asked for.</summary>
    public override int? SettlementDays => originalCurve.SettlementDays;

    /// <summary>Gets the earlier of the original curve's maximum date and the last spread's date.</summary>
    public override Date MaxDate
    {
        get
        {
            Date originalMax = originalCurve.MaxDate;
            Date last = dates[^1];
            return last < originalMax ? last : originalMax;
        }
    }

    /// <summary>
    /// The original's continuously compounded zero rate at a time, plus the spread's average from the reference date.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The zero yield.</returns>
    protected override double ZeroYieldImpl(double t)
    {
        double spreadPrimitive = CalcSpreadPrimitive(t);
        InterestRate zeroRate = originalCurve.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true);
        return zeroRate.Rate + spreadPrimitive;
    }

    // The spread at a time: flat before the first date and after the last, interpolated between.
    private double CalcSpread(double t)
    {
        if (t <= times[0])
        {
            return spreadValues[0];
        }
        else if (t >= times[^1])
        {
            return spreadValues[^1];
        }
        else
        {
            return interpolator.Evaluate(t, true);
        }
    }

    // The spread's average from the reference date to a time, its integral from 0 over the time; the spread itself
    // before the first date, where it is flat.
    private double CalcSpreadPrimitive(double t)
    {
        if (t == 0.0)
        {
            return CalcSpread(0.0);
        }

        if (t <= times[0])
        {
            return spreadValues[0];
        }

        // QuantLib's integral from the first time, less the antiderivative's value at the reference date: the flat
        // spread's integral up to the first time added first, as QuantLib's own curve with that spread prepended at the
        // reference date adds it
        double start = SpreadIntegralAtZero();
        double integral;
        if (t <= times[^1])
        {
            integral = interpolator.Primitive(t, true) - start;
        }
        else
        {
            integral = (interpolator.Primitive(times[^1], true) - start) + (spreadValues[^1] * (t - times[^1]));
        }

        return integral / t;
    }

    // The value at the reference date of the spread's antiderivative that is the interpolation's primitive from the
    // first time, continued with the flat spreads before the first time and after the last: −s_0·t_0 for a first time
    // after the reference date, 0 for one at it.
    private double SpreadIntegralAtZero()
    {
        if (0.0 <= times[0])
        {
            return spreadValues[0] * (0.0 - times[0]);
        }
        else if (0.0 <= times[^1])
        {
            return interpolator.Primitive(0.0, true);
        }
        else
        {
            return interpolator.Primitive(times[^1], true) + (spreadValues[^1] * (0.0 - times[^1]));
        }
    }

    // The original, refused when null before the base constructor reads it.
    private static YieldTermStructure Original(YieldTermStructure originalCurve)
    {
        ArgumentNullException.ThrowIfNull(originalCurve);
        return originalCurve;
    }
}
