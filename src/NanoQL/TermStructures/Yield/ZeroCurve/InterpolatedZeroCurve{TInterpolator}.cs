// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/zerocurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A yield term structure based on the interpolation of zero rates.</summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// The curve holds continuously compounded zero rates: rates given with another compounding are converted to their
/// continuous equivalents at the nodes' times when it is built, and its data are those equivalents. Past its last node
/// it extrapolates with a flat instantaneous forward rate. Its formula and maximum date are the
/// <see cref="ZeroYield"/> traits', which a <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/> on
/// those traits shares. Unsealed for its named classes, which add constructors only. QuantLib's protected constructors,
/// through which its piecewise curve derives, aren't ported: no class derives through them here.
/// </remarks>
public class InterpolatedZeroCurve<TInterpolator> : ZeroYieldStructure, IInterpolatedCurve<ZeroYield, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpolatedZeroCurve{TInterpolator}"/> class from its nodes; the
    /// first date is the reference date.
    /// </summary>
    /// <param name="dates">The nodes' dates, sorted.</param>
    /// <param name="yields">The zero rates at the nodes, with the given compounding and frequency.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <param name="compounding">The rates' compounding.</param>
    /// <param name="frequency">
    /// The rates' compounding frequency, read only when the compounding isn't continuous, as QuantLib reads it.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dates"/>, <paramref name="yields"/> or <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="compounding"/> isn't a member of its enum, or the compounding isn't continuous and
    /// <paramref name="frequency"/> isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// There are fewer dates than the interpolation needs, the counts of dates and rates differ, the dates aren't
    /// sorted or two of them give the same time, the jumps and their dates differ in number, a compounded rate's
    /// frequency is <see cref="Frequency.Once"/> or <see cref="Frequency.NoFrequency"/>, a rate's compound factor isn't
    /// positive, or the interpolation refuses the nodes.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The compounding is one that QuantLib can't convert directly, or the interpolation refuses the nodes.
    /// </exception>
    public InterpolatedZeroCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> yields,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator interpolator = default,
        Compounding compounding = Compounding.Continuous,
        Frequency frequency = Frequency.Annual)
        : this(
            InterpolatedCurve<TInterpolator>.Inputs(dates, yields, dayCounter, nameof(yields)),
            calendar,
            jumps,
            jumpDates,
            interpolator,
            CheckCompounding(compounding),
            frequency)
    {
    }

    private InterpolatedZeroCurve(
        (ImmutableArray<Date> Dates, ImmutableArray<double> Data, DayCounter DayCounter) inputs,
        Calendar? calendar,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        TInterpolator interpolator,
        Compounding compounding,
        Frequency frequency)
        : base(inputs.Dates[0], calendar, inputs.DayCounter, null, jumps, jumpDates)
    {
        // initialize
        dates = inputs.Dates;
        double[] data = [.. inputs.Data];
        if (!(dates.Length >= TInterpolator.RequiredPoints))
        {
            throw new ArgumentException("not enough input dates given", nameof(dates));
        }

        if (!(data.Length == dates.Length))
        {
            throw new ArgumentException("dates/data count mismatch", "yields");
        }

        double[] times = InterpolatedCurve<TInterpolator>.SetupTimes(dates.AsSpan(), dates[0], inputs.DayCounter);

        if (compounding != Compounding.Continuous)
        {
            // adjusting zero rates to match continuous compounding
            CheckFrequency(compounding, frequency);

            // The first time is 0.0, so we can't use it.
            // We fall back to about one day.
            double dt = 1.0 / 365;
            var r = new InterestRate(data[0], inputs.DayCounter, compounding, frequency);
            data[0] = r.EquivalentRate(Compounding.Continuous, Frequency.NoFrequency, dt).Rate;

            for (int i = 1; i < dates.Length; ++i)
            {
                r = new InterestRate(data[i], inputs.DayCounter, compounding, frequency);
                data[i] = r.EquivalentRate(Compounding.Continuous, Frequency.NoFrequency, times[i]).Rate;
            }
        }

        curve = InterpolatedCurve<TInterpolator>.FromTimes(times, data, interpolator);
    }

    /// <inheritdoc/>
    public sealed override Date MaxDate => ZeroCurveFormula.MaxDate(dates.AsSpan(), curve.MaxDate);

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    /// <remarks>The continuously compounded zero rates, whatever compounding the curve was given.</remarks>
    public ImmutableArray<double> Data() => curve.Data();

    /// <summary>The continuously compounded zero rates at the curve's nodes, its data.</summary>
    /// <returns>The zero rates.</returns>
    public ImmutableArray<double> ZeroRates() => curve.Data();

    /// <inheritdoc/>
    public ImmutableArray<KeyValuePair<Date, double>> Nodes()
    {
        ImmutableArray<double> data = curve.Data();
        var nodes = ImmutableArray.CreateBuilder<KeyValuePair<Date, double>>(dates.Length);
        for (int i = 0; i < dates.Length; ++i)
        {
            nodes.Add(new KeyValuePair<Date, double>(dates[i], data[i]));
        }

        return nodes.MoveToImmutable();
    }

    /// <inheritdoc/>
    protected sealed override double ZeroYieldImpl(double t) =>
        ZeroCurveFormula.ZeroYieldImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);

    // The compounding, computed with at construction, is checked on entry (the playbook's enum row).
    private static Compounding CheckCompounding(Compounding compounding)
    {
        EnumArguments.Check(compounding, nameof(compounding));
        return compounding;
    }

    // InterestRate's checks of the frequency, which QuantLib reads only to convert compounded rates, naming the curve's
    // own parameter.
    private static void CheckFrequency(Compounding compounding, Frequency frequency)
    {
        EnumArguments.Check(frequency, nameof(frequency));
        bool compounded =
            compounding is Compounding.Compounded or Compounding.SimpleThenCompounded or Compounding.CompoundedThenSimple;
        if (compounded && !(frequency != Frequency.Once && frequency != Frequency.NoFrequency))
        {
            throw new ArgumentException("frequency not allowed for this interest rate", nameof(frequency));
        }
    }
}
