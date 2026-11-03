// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/gaussian1dswaptionvolatility.hpp ql/termstructures/volatility/swaption/gaussian1dswaptionvolatility.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The swaption volatilities a Gaussian1d model implies: each option date and swap tenor's
/// <see cref="Gaussian1dSmileSection"/> on the index base cloned to the tenor.
/// </summary>
/// <remarks>
/// A section is built per call, as QuantLib's <c>smileSectionImpl</c> builds one, under the context given at
/// construction, which the structure keeps; its reference date is the model curve's. An option time is taken to the
/// date whose time it is, by QuantLib's Newton search over a date's fractional serial number, adjusted on the index's
/// fixing calendar; a swap length to the nearest whole month.
/// </remarks>
public sealed class Gaussian1dSwaptionVolatility : SwaptionVolatilityStructure
{
    private readonly SwapIndex indexBase;
    private readonly Gaussian1dModel model;
    private readonly EvaluationContext context;
    private readonly Func<Swaption, EvaluationContext, InstrumentResults>? engine;

    /// <summary>Initializes a new instance of the <see cref="Gaussian1dSwaptionVolatility"/> class.</summary>
    /// <param name="cal">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty
    /// calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="indexBase">The swap index each section clones to its tenor.</param>
    /// <param name="model">The model.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="context">The evaluation context, which the structure keeps for its sections.</param>
    /// <param name="swaptionEngine">The sections' swaption engine, or <see langword="null"/> for theirs.</param>
    /// <exception cref="ArgumentNullException">An argument but the calendar and the engine is null.</exception>
    public Gaussian1dSwaptionVolatility(
        Calendar? cal,
        BusinessDayConvention bdc,
        SwapIndex indexBase,
        Gaussian1dModel model,
        DayCounter dc,
        EvaluationContext context,
        Func<Swaption, EvaluationContext, InstrumentResults>? swaptionEngine = null)
        : base(ModelOf(model).TermStructure.ReferenceDate, cal, bdc, dc)
    {
        ArgumentNullException.ThrowIfNull(indexBase);
        ArgumentNullException.ThrowIfNull(context);
        this.indexBase = indexBase;
        this.model = model;
        this.context = context;
        engine = swaptionEngine;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <inheritdoc/>
    public override Period MaxSwapTenor { get; } = new(100, TimeUnit.Years);

    /// <inheritdoc/>
    public override double MinStrike() => 0.0;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(Date optionDate, Period swapTenor) =>
        new Gaussian1dSmileSection(optionDate, indexBase.Clone(swapTenor), model, DayCounter, context, engine);

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(double optionTime, double swapLength)
    {
        var hlp = new DateHelper(this, optionTime);
        var newton = new NewtonSafe();
        Date d = new((int)newton.Solve(hlp, 0.1, (365.25 * optionTime) + ReferenceDate.SerialNumber, 1.0));
        var tenor = new Period((int)new Rounding(0).Evaluate(swapLength * 12.0), TimeUnit.Months);
        d = indexBase.FixingCalendar.Adjust(d);
        return SmileSectionImpl(d, tenor);
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(Date optionDate, Period swapTenor, double strike) =>
        SmileSectionImpl(optionDate, swapTenor).Volatility(strike);

    /// <inheritdoc/>
    protected override double VolatilityImpl(double optionTime, double swapLength, double strike) =>
        SmileSectionImpl(optionTime, swapLength).Volatility(strike);

    private static Gaussian1dModel ModelOf(Gaussian1dModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model;
    }

    // A date's fractional serial number's time from the reference, less the target time, linear within the day; its
    // derivative by a forward difference, to avoid dates before the reference date.
    private readonly struct DateHelper(TermStructure ts, double t) : IDerivativeSolver1DFunction
    {
        public double Evaluate(double date)
        {
            var d1 = new Date((int)date);
            var d2 = new Date((int)date + 1);
            double t1 = ts.TimeFromReference(d1) - t;
            double t2 = ts.TimeFromReference(d2) - t;
            double h = date - (int)date;
            return (h * t2) + ((1.0 - h) * t1);
        }

        public double Derivative(double date) =>
            // use fwd difference to avoid dates before reference date
            (Evaluate(date + 1E-6) - Evaluate(date)) * 1E6;
    }
}
