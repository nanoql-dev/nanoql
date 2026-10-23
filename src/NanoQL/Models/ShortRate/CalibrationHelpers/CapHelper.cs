// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/calibrationhelpers/caphelper.hpp ql/models/shortrate/calibrationhelpers/caphelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>A calibration helper for at-the-money caps: a cap on the index, struck at the fair rate of a swap on it.</summary>
/// <remarks>
/// <para>
/// Immutable (the S-sr plan's SSD8). The constructor builds what QuantLib's helper builds at its first calculation,
/// under the context it is given: the floating leg on the index from the curve's reference date (or a period of the
/// index's tenor after it, without the first caplet) to the length after it, with no fixing days; a fixed leg on the
/// fixed-leg frequency, unadjusted, at a dummy 4%; their swap's fair rate on the term structure, without the settlement
/// date's flows; the cap on the floating leg at that rate; and the market value. QuantLib's helper also builds a dummy
/// index it never reads, which this one leaves out.
/// </para>
/// <para>
/// The model value is the pricing engine's, set by <see cref="SetPricingEngine{TModel}"/>, which takes the model being
/// calibrated as its last argument; with no engine, pricing a cap that hasn't expired refuses with QuantLib's "null
/// pricing engine".
/// </para>
/// </remarks>
public sealed class CapHelper : BlackCalibrationHelper
{
    private readonly YieldTermStructure termStructure;

    // Written by SetPricingEngine on a fresh copy only.
    private Func<CapFloor, EvaluationContext, ICalibratedModel, InstrumentResults>? engine;

    /// <summary>Initializes a new instance of the <see cref="CapHelper"/> class.</summary>
    /// <param name="length">The cap's length from the curve's reference date.</param>
    /// <param name="volatility">The quoted volatility.</param>
    /// <param name="index">The floating leg's index.</param>
    /// <param name="fixedLegFrequency">The frequency of the fixed leg whose swap gives the at-the-money rate.</param>
    /// <param name="fixedLegDayCounter">That fixed leg's day counter.</param>
    /// <param name="includeFirstSwaplet">Whether the cap starts on the curve's reference date, its first caplet fixed then.</param>
    /// <param name="termStructure">The curve the swap and the Black price discount on.</param>
    /// <param name="context">The evaluation context, under which the legs and the market value are computed.</param>
    /// <param name="errorType">How the helper measures its error.</param>
    /// <param name="type">The volatility's type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentNullException">The index, the day counter, the term structure or the context is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The legs or the swap can't be built or priced.</exception>
    public CapHelper(
        Period length,
        double volatility,
        IborIndex index,
        Frequency fixedLegFrequency,
        DayCounter fixedLegDayCounter,
        bool includeFirstSwaplet,
        YieldTermStructure termStructure,
        EvaluationContext context,
        BlackCalibrationHelperCalibrationErrorType errorType = BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
        : base(volatility, errorType, EntryChecked(type, index, fixedLegDayCounter, termStructure, context), shift)
    {
        this.termStructure = termStructure;
        Cap = Build(length, index, fixedLegFrequency, fixedLegDayCounter, includeFirstSwaplet, termStructure, context);
        ComputeMarketValue(context);
    }

    /// <summary>Gets the at-the-money cap.</summary>
    public Cap Cap { get; }

    /// <summary>A copy of the helper whose model value is the given engine's price: QuantLib's <c>setPricingEngine</c>.</summary>
    /// <typeparam name="TModel">The model type the engine prices with.</typeparam>
    /// <param name="engine">The engine: the cap, the context and the model being calibrated; <see langword="null"/> for none.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    /// <remarks>
    /// The engine is kept for any model: pricing the copy against a model that isn't a <typeparamref name="TModel"/>
    /// throws <see cref="InvalidCastException"/>.
    /// </remarks>
    public CapHelper SetPricingEngine<TModel>(Func<CapFloor, EvaluationContext, TModel, InstrumentResults>? engine)
        where TModel : ICalibratedModel
    {
        var copy = (CapHelper)MemberwiseClone();
        copy.engine = engine is null ? null : (c, x, m) => engine(c, x, (TModel)m);
        return copy;
    }

    /// <summary>The cap's price according to a model, through the pricing engine.</summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The model value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="InvalidCastException">The engine prices with another model type.</exception>
    /// <exception cref="NanoQLException">No engine is set and the cap hasn't expired, or the engine fails.</exception>
    public override double ModelValue(ICalibratedModel model, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (engine is null)
        {
            // QuantLib's instrument reads its expiry before it asks for its engine.
            if (Cap.IsExpired(context))
            {
                return Npv(Cap.SetupExpired());
            }

            throw new NanoQLException("null pricing engine");
        }

        return Npv(engine(Cap, context, model));
    }

    /// <summary>
    /// The cap's Black price at a volatility: a <see cref="BlackCapFloorEngine"/> on the volatility with the helper's
    /// shift for a shifted lognormal type, a <see cref="BachelierCapFloorEngine"/> for a normal one, each on the term
    /// structure with an Actual/365 (Fixed) volatility.
    /// </summary>
    /// <param name="volatility">The volatility.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The engine fails.</exception>
    public override double BlackPrice(double volatility, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        InstrumentResults results = VolatilityType == VolatilityType.ShiftedLognormal
            ? BlackCapFloorEngine.Calculate(Cap, context, termStructure, volatility, new Actual365Fixed(), Shift)
            : BachelierCapFloorEngine.Calculate(Cap, context, termStructure, volatility, new Actual365Fixed());
        return Npv(results);
    }

    /// <summary>
    /// The times a lattice for the cap must stop at: the mandatory times of its <see cref="DiscretizedCapFloor"/>, on
    /// the term structure's reference date and day counter.
    /// </summary>
    /// <param name="context">The evaluation context, whose coupons' fixings and forecasts the cap's arguments read.</param>
    /// <returns>The times, not guaranteed to be sorted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public override ImmutableArray<double> Times(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new DiscretizedCapFloor(Cap, termStructure.ReferenceDate, termStructure.DayCounter, context).MandatoryTimes();
    }

    // The checks on entry, in the base's argument so that they run before the base's: the context first, then the
    // index and the day counter (QuantLib dereferences them at its first calculation), the curve, and the volatility
    // type with the text of QuantLib's Black price, which refuses it there.
    private static VolatilityType EntryChecked(
        VolatilityType type, IborIndex index, DayCounter fixedLegDayCounter, YieldTermStructure termStructure, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(fixedLegDayCounter);
        ArgumentNullException.ThrowIfNull(termStructure);
        if (type is not (VolatilityType.ShiftedLognormal or VolatilityType.Normal))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, $"unknown volatility type: {type}");
        }

        return type;
    }

    // QuantLib's performCalculations: the legs, their swap's fair rate and the cap.
    private static Cap Build(
        Period length,
        IborIndex index,
        Frequency fixedLegFrequency,
        DayCounter fixedLegDayCounter,
        bool includeFirstSwaplet,
        YieldTermStructure termStructure,
        EvaluationContext context)
    {
        Period indexTenor = index.Tenor;
        double fixedRate = 0.04; // dummy value
        Date startDate, maturity;
        if (includeFirstSwaplet)
        {
            startDate = termStructure.ReferenceDate;
            maturity = termStructure.ReferenceDate + length;
        }
        else
        {
            startDate = termStructure.ReferenceDate + indexTenor;
            maturity = termStructure.ReferenceDate + length;
        }

        var floatSchedule = new Schedule(
            startDate,
            maturity,
            index.Tenor,
            index.FixingCalendar,
            index.BusinessDayConvention,
            index.BusinessDayConvention,
            DateGenerationRule.Forward,
            false,
            context);
        ImmutableArray<CashFlow> floatingLeg = new IborLeg(floatSchedule, index)
            .WithNotionals(1.0)
            .WithPaymentAdjustment(index.BusinessDayConvention)
            .WithFixingDays(0)
            .ToLeg(context);

        var fixedSchedule = new Schedule(
            startDate,
            maturity,
            new Period(fixedLegFrequency),
            index.FixingCalendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            false,
            context);
        ImmutableArray<CashFlow> fixedLeg = new FixedRateLeg(fixedSchedule)
            .WithNotionals(1.0)
            .WithCouponRates(fixedRate, fixedLegDayCounter)
            .WithPaymentAdjustment(index.BusinessDayConvention)
            .ToLeg();

        var swap = new Swap(floatingLeg, fixedLeg);
        SwapResults results = DiscountingSwapEngine.Calculate(swap, context, termStructure, false);
        double npv = results.NPV ?? throw new NanoQLException("NPV not provided");
        double bps = results.LegBPS[1] ?? throw new NanoQLException("result not available");
        double fairRate = fixedRate - (npv / (bps / 1.0e-4));
        return new Cap(floatingLeg, [fairRate]);
    }

    // Instrument::NPV's check.
    private static double Npv(InstrumentResults results) => results.NPV ?? throw new NanoQLException("NPV not provided");
}
