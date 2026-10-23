// SPDX-FileCopyrightText: Copyright (C) 2006 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008, 2014 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Robert Philipp
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewisezerospreadedtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A yield curve with spreads added to the zero rate of another curve, the original, the spread at any time
/// interpolated between the spreads given at dates and held flat before the first and after the last.
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
/// QuantLib's curve follows its quotes and its handle.
/// </para>
/// <para>
/// The spread is added in the given compounding and frequency: the original's zero rate at a time in them, plus the
/// spread, converted to continuous compounding at that time. QuantLib's deprecated constructor taking a day counter,
/// which it ignores, isn't ported. Unsealed for its named class, which adds constructors only.
/// </para>
/// </remarks>
public class InterpolatedPiecewiseZeroSpreadedTermStructure<TInterpolator> : ZeroYieldStructure
    where TInterpolator : struct, IInterpolator
{
    private readonly YieldTermStructure originalCurve;
    private readonly ImmutableArray<Date> dates;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> spreadValues;
    private readonly Compounding comp;
    private readonly Frequency freq;
    private readonly Interpolation interpolator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpolatedPiecewiseZeroSpreadedTermStructure{TInterpolator}"/>
    /// class.
    /// </summary>
    /// <param name="originalCurve">The curve the spreads are added to.</param>
    /// <param name="spreads">The spreads, one per date.</param>
    /// <param name="dates">The spreads' dates, sorted.</param>
    /// <param name="comp">The compounding the spreads are added in.</param>
    /// <param name="freq">The frequency the spreads are added in, which a compounded rule needs.</param>
    /// <param name="factory">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="originalCurve"/>, <paramref name="spreads"/> or <paramref name="dates"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// No spread is given, the spreads and the dates differ in number, a date is <c>default(Date)</c>, or the
    /// interpolation refuses the spreads.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the spreads.</exception>
    public InterpolatedPiecewiseZeroSpreadedTermStructure(
        YieldTermStructure originalCurve,
        IEnumerable<double> spreads,
        IEnumerable<Date> dates,
        Compounding comp = Compounding.Continuous,
        Frequency freq = Frequency.NoFrequency,
        TInterpolator factory = default)
        : base(Original(originalCurve).ReferenceDate, null, originalCurve.DayCounter)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        ArgumentNullException.ThrowIfNull(dates);
        EnumArguments.Check(comp, nameof(comp));
        EnumArguments.Check(freq, nameof(freq));
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

        this.comp = comp;
        this.freq = freq;
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
    public sealed override Date MaxDate
    {
        get
        {
            Date originalMax = originalCurve.MaxDate;
            Date last = dates[^1];
            return last < originalMax ? last : originalMax;
        }
    }

    /// <summary>The spreaded zero yield rate at a time, continuously compounded.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The zero yield.</returns>
    /// <exception cref="ArgumentException">The compounding is compounded with no frequency or <see cref="Frequency.Once"/>.</exception>
    protected sealed override double ZeroYieldImpl(double t)
    {
        double spread = CalcSpread(t);
        InterestRate zeroRate = originalCurve.ZeroRate(t, comp, freq, true);
        var spreadedRate = new InterestRate(
            zeroRate.Rate + spread, zeroRate.DayCounter, zeroRate.Compounding, zeroRate.Frequency);
        return spreadedRate.EquivalentRate(Compounding.Continuous, Frequency.NoFrequency, t).Rate;
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

    // The original, refused when null before the base constructor reads it.
    private static YieldTermStructure Original(YieldTermStructure originalCurve)
    {
        ArgumentNullException.ThrowIfNull(originalCurve);
        return originalCurve;
    }
}
