// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/calibrationhelpers/swaptionhelper.hpp ql/models/shortrate/calibrationhelpers/swaptionhelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>A calibration helper for interest-rate swaptions: a European swaption on a swap of the index, quoted by a volatility.</summary>
/// <remarks>
/// <para>
/// Immutable. Each constructor builds what QuantLib's helper builds at its first calculation, under the context it is
/// given: the exercise and start dates, the schedules, a swap at a zero rate whose fair rate is the forward (priced on
/// the term structure, without the settlement date's flows), the exercise rate (the strike, or the forward when none
/// is given) and the swap's type (a receiver, unless the strike is above the forward), the swap at that rate, the
/// European swaption, and the market value. So the at-par choice of the swaps' Ibor coupons is the context's at
/// construction, where QuantLib's reads its global switch whenever the helper recalculates.
/// </para>
/// <para>
/// An overnight index builds an overnight-indexed swap, which model-based engines treat as a vanilla swap: at best a
/// decent proxy, at worst simply wrong; use with caution. Its value dates are QuantLib's telescopic ones, which nanoql
/// builds in full.
/// </para>
/// <para>
/// The model value is the pricing engine's, set by <see cref="SetPricingEngine{TModel}"/>, which takes the model being
/// calibrated as its last argument; with no engine, pricing a swaption that hasn't expired refuses with QuantLib's "null
/// pricing engine". The settlement days' value is never read, as in QuantLib: any non-null value takes the start date
/// the index's fixing days after the exercise date, where null takes the index's value date.
/// </para>
/// </remarks>
public sealed class SwaptionHelper : BlackCalibrationHelper
{
    private readonly YieldTermStructure termStructure;

    // Written by SetPricingEngine on a fresh copy only.
    private Func<Swaption, EvaluationContext, ICalibratedModel, InstrumentResults>? engine;

    /// <summary>Initializes a new instance of the <see cref="SwaptionHelper"/> class, exercising a period after the curve's reference date.</summary>
    /// <param name="maturity">The time from the term structure's reference date to the exercise date.</param>
    /// <param name="length">The swap's length from its start date.</param>
    /// <param name="volatility">The quoted volatility.</param>
    /// <param name="index">The floating leg's index; an overnight index builds an overnight-indexed swap.</param>
    /// <param name="fixedLegTenor">The fixed leg's tenor.</param>
    /// <param name="fixedLegDayCounter">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="floatingLegDayCounter">
    /// The floating leg's day counter; the index's when <see langword="null"/>. An overnight-indexed swap doesn't read it.
    /// </param>
    /// <param name="termStructure">The curve the swaps and the Black price discount on.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor legs read and under which the market value is computed.</param>
    /// <param name="errorType">How the helper measures its error.</param>
    /// <param name="strike">The strike; <see langword="null"/> for the forward, at the money.</param>
    /// <param name="nominal">The swaps' nominal.</param>
    /// <param name="type">The volatility's type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <param name="settlementDays">Whether the start date is the index's fixing days after the exercise date, when not <see langword="null"/>; its value isn't read.</param>
    /// <param name="averagingMethod">The overnight-indexed swap's averaging method.</param>
    /// <exception cref="ArgumentNullException">The index, the term structure or the context is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="NanoQLException">The swaps can't be built or priced.</exception>
    public SwaptionHelper(
        Period maturity,
        Period length,
        double volatility,
        IborIndex index,
        Period fixedLegTenor,
        DayCounter? fixedLegDayCounter,
        DayCounter? floatingLegDayCounter,
        YieldTermStructure termStructure,
        EvaluationContext context,
        BlackCalibrationHelperCalibrationErrorType errorType = BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
        double? strike = null,
        double nominal = 1.0,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        int? settlementDays = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound)
        : base(volatility, errorType, EntryChecked(type, index, termStructure, context, errorType, settlementDays), shift)
    {
        this.termStructure = termStructure;
        (Underlying, Swaption) = Build(
            null, null, maturity, length, index, fixedLegTenor, fixedLegDayCounter, floatingLegDayCounter, termStructure,
            context, strike, nominal, settlementDays, averagingMethod);
        ComputeMarketValue(context);
    }

    /// <summary>Initializes a new instance of the <see cref="SwaptionHelper"/> class, exercising on a date.</summary>
    /// <param name="exerciseDate">The exercise date.</param>
    /// <param name="length">The swap's length from its start date.</param>
    /// <param name="volatility">The quoted volatility.</param>
    /// <param name="index">The floating leg's index; an overnight index builds an overnight-indexed swap.</param>
    /// <param name="fixedLegTenor">The fixed leg's tenor.</param>
    /// <param name="fixedLegDayCounter">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="floatingLegDayCounter">
    /// The floating leg's day counter; the index's when <see langword="null"/>. An overnight-indexed swap doesn't read it.
    /// </param>
    /// <param name="termStructure">The curve the swaps and the Black price discount on.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor legs read and under which the market value is computed.</param>
    /// <param name="errorType">How the helper measures its error.</param>
    /// <param name="strike">The strike; <see langword="null"/> for the forward, at the money.</param>
    /// <param name="nominal">The swaps' nominal.</param>
    /// <param name="type">The volatility's type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <param name="settlementDays">Whether the start date is the index's fixing days after the exercise date, when not <see langword="null"/>; its value isn't read.</param>
    /// <param name="averagingMethod">The overnight-indexed swap's averaging method.</param>
    /// <exception cref="ArgumentNullException">The index, the term structure or the context is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="exerciseDate"/> is <see langword="default"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="NanoQLException">The swaps can't be built or priced.</exception>
    public SwaptionHelper(
        Date exerciseDate,
        Period length,
        double volatility,
        IborIndex index,
        Period fixedLegTenor,
        DayCounter? fixedLegDayCounter,
        DayCounter? floatingLegDayCounter,
        YieldTermStructure termStructure,
        EvaluationContext context,
        BlackCalibrationHelperCalibrationErrorType errorType = BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
        double? strike = null,
        double nominal = 1.0,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        int? settlementDays = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound)
        : base(volatility, errorType, EntryChecked(type, index, termStructure, context, errorType, settlementDays), shift)
    {
        Date.ThrowIfDefault(exerciseDate, nameof(exerciseDate));
        this.termStructure = termStructure;
        (Underlying, Swaption) = Build(
            exerciseDate, null, new Period(0, TimeUnit.Days), length, index, fixedLegTenor, fixedLegDayCounter,
            floatingLegDayCounter, termStructure, context, strike, nominal, settlementDays, averagingMethod);
        ComputeMarketValue(context);
    }

    /// <summary>Initializes a new instance of the <see cref="SwaptionHelper"/> class, exercising on a date into a swap ending on a date.</summary>
    /// <param name="exerciseDate">The exercise date.</param>
    /// <param name="endDate">The swap's end date.</param>
    /// <param name="volatility">The quoted volatility.</param>
    /// <param name="index">The floating leg's index; an overnight index builds an overnight-indexed swap.</param>
    /// <param name="fixedLegTenor">The fixed leg's tenor.</param>
    /// <param name="fixedLegDayCounter">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="floatingLegDayCounter">
    /// The floating leg's day counter; the index's when <see langword="null"/>. An overnight-indexed swap doesn't read it.
    /// </param>
    /// <param name="termStructure">The curve the swaps and the Black price discount on.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor legs read and under which the market value is computed.</param>
    /// <param name="errorType">How the helper measures its error.</param>
    /// <param name="strike">The strike; <see langword="null"/> for the forward, at the money.</param>
    /// <param name="nominal">The swaps' nominal.</param>
    /// <param name="type">The volatility's type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <param name="settlementDays">Whether the start date is the index's fixing days after the exercise date, when not <see langword="null"/>; its value isn't read.</param>
    /// <param name="averagingMethod">The overnight-indexed swap's averaging method.</param>
    /// <exception cref="ArgumentNullException">The index, the term structure or the context is null.</exception>
    /// <exception cref="ArgumentException">A date is <see langword="default"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="NanoQLException">The swaps can't be built or priced.</exception>
    public SwaptionHelper(
        Date exerciseDate,
        Date endDate,
        double volatility,
        IborIndex index,
        Period fixedLegTenor,
        DayCounter? fixedLegDayCounter,
        DayCounter? floatingLegDayCounter,
        YieldTermStructure termStructure,
        EvaluationContext context,
        BlackCalibrationHelperCalibrationErrorType errorType = BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
        double? strike = null,
        double nominal = 1.0,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        int? settlementDays = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound)
        : base(volatility, errorType, EntryChecked(type, index, termStructure, context, errorType, settlementDays), shift)
    {
        Date.ThrowIfDefault(exerciseDate, nameof(exerciseDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        this.termStructure = termStructure;
        (Underlying, Swaption) = Build(
            exerciseDate, endDate, new Period(0, TimeUnit.Days), new Period(0, TimeUnit.Days), index, fixedLegTenor,
            fixedLegDayCounter, floatingLegDayCounter, termStructure, context, strike, nominal, settlementDays,
            averagingMethod);
        ComputeMarketValue(context);
    }

    /// <summary>Gets the underlying swap, at the exercise rate.</summary>
    public FixedVsFloatingSwap Underlying { get; }

    /// <summary>Gets the European swaption on the underlying swap, physically settled over the counter.</summary>
    public Swaption Swaption { get; }

    /// <summary>
    /// A copy of the helper whose model value is the given engine's price: QuantLib's <c>setPricingEngine</c>.
    /// </summary>
    /// <typeparam name="TModel">The model type the engine prices with.</typeparam>
    /// <param name="engine">
    /// The engine: the swaption, the context and the model being calibrated, in D14's order; <see langword="null"/>
    /// for none.
    /// </param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    /// <remarks>
    /// The engine is kept for any model: pricing the copy against a model that isn't a <typeparamref name="TModel"/>
    /// throws <see cref="InvalidCastException"/>, .NET's type for a failed cast.
    /// </remarks>
    public SwaptionHelper SetPricingEngine<TModel>(Func<Swaption, EvaluationContext, TModel, InstrumentResults>? engine)
        where TModel : ICalibratedModel
    {
        var copy = (SwaptionHelper)MemberwiseClone();
        copy.engine = engine is null ? null : (s, c, m) => engine(s, c, (TModel)m);
        return copy;
    }

    /// <summary>The swaption's price according to a model, through the pricing engine.</summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The model value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="InvalidCastException">The engine prices with another model type.</exception>
    /// <exception cref="NanoQLException">No engine is set and the swaption hasn't expired, or the engine fails.</exception>
    public override double ModelValue(ICalibratedModel model, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (engine is null)
        {
            // QuantLib's instrument reads its expiry before it asks for its engine.
            if (Swaption.IsExpired(context))
            {
                return Npv(Swaption.SetupExpired());
            }

            throw new NanoQLException("null pricing engine");
        }

        return Npv(engine(Swaption, context, model));
    }

    /// <summary>
    /// The swaption's Black price at a volatility: a <see cref="BlackSwaptionEngine"/> on the volatility with the
    /// helper's shift for a shifted lognormal type, a <see cref="BachelierSwaptionEngine"/> for a normal one, each on
    /// the term structure with an Actual/365 (Fixed) volatility.
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
            ? BlackSwaptionEngine.Calculate(Swaption, context, termStructure, volatility, new Actual365Fixed(), Shift)
            : BachelierSwaptionEngine.Calculate(Swaption, context, termStructure, volatility, new Actual365Fixed());
        return Npv(results);
    }

    /// <summary>
    /// The times a lattice for the swaption must stop at: the mandatory times of its <see cref="DiscretizedSwaption"/>,
    /// on the term structure's reference date and day counter.
    /// </summary>
    /// <param name="context">The evaluation context, whose at-par switch the discretized swaption's snapped swap reads.</param>
    /// <returns>The times, not guaranteed to be sorted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public override ImmutableArray<double> Times(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new DiscretizedSwaption(Swaption, termStructure.ReferenceDate, termStructure.DayCounter, context)
            .MandatoryTimes();
    }

    // The checks on entry, in the base's argument so that they run before the base's: the context first, then the
    // index (QuantLib dereferences a null one at its first calculation), the curve, the error type under this
    // constructor's name, the settlement days, and the volatility type with the text of QuantLib's Black price, which
    // refuses it there.
    private static VolatilityType EntryChecked(
        VolatilityType type,
        IborIndex index,
        YieldTermStructure termStructure,
        EvaluationContext context,
        BlackCalibrationHelperCalibrationErrorType errorType,
        int? settlementDays)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(termStructure);
        if (errorType is < BlackCalibrationHelperCalibrationErrorType.RelativePriceError
            or > BlackCalibrationHelperCalibrationErrorType.ImpliedVolError)
        {
            throw new ArgumentOutOfRangeException(nameof(errorType), errorType, "unknown Calibration Error Type");
        }

        if (settlementDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(settlementDays));
        }

        if (type is not (VolatilityType.ShiftedLognormal or VolatilityType.Normal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type), type, $"can not construct engine: Unknown volatility type ({(int)type})");
        }

        return type;
    }

    // QuantLib's performCalculations, but the market value.
    private static (FixedVsFloatingSwap Swap, Swaption Swaption) Build(
        Date? givenExerciseDate,
        Date? givenEndDate,
        Period maturity,
        Period length,
        IborIndex index,
        Period fixedLegTenor,
        DayCounter? fixedLegDayCounter,
        DayCounter? floatingLegDayCounter,
        YieldTermStructure termStructure,
        EvaluationContext context,
        double? strike,
        double nominal,
        int? settlementDays,
        RateAveragingType averagingMethod)
    {
        Calendar calendar = index.FixingCalendar;
        Date exerciseDate = givenExerciseDate
            ?? calendar.Advance(termStructure.ReferenceDate, maturity, index.BusinessDayConvention);
        Date startDate = settlementDays is null
            ? index.ValueDate(index.FixingCalendar.Adjust(exerciseDate))
            : calendar.Advance(exerciseDate, index.FixingDays, TimeUnit.Days, index.BusinessDayConvention);

        Date endDate = givenEndDate ?? calendar.Advance(startDate, length, index.BusinessDayConvention);
        var fixedSchedule = new Schedule(
            startDate,
            endDate,
            fixedLegTenor,
            calendar,
            index.BusinessDayConvention,
            index.BusinessDayConvention,
            DateGenerationRule.Forward,
            false,
            context);
        var floatSchedule = new Schedule(
            startDate,
            endDate,
            index.Tenor,
            calendar,
            index.BusinessDayConvention,
            index.BusinessDayConvention,
            DateGenerationRule.Forward,
            false,
            context);

        SwapType type = SwapType.Receiver;
        var exercise = new EuropeanExercise(exerciseDate);
        FixedVsFloatingSwap temp = MakeSwap(
            fixedSchedule, floatSchedule, 0.0, type, index, nominal, fixedLegDayCounter, floatingLegDayCounter,
            averagingMethod, context);
        double forward = DiscountingSwapEngine.Calculate(temp, context, termStructure, false).FairRate
            ?? throw new NanoQLException("result not available");
        double exerciseRate;
        if (strike is not double k)
        {
            exerciseRate = forward;
        }
        else
        {
            exerciseRate = k;
            type = k <= forward ? SwapType.Receiver : SwapType.Payer;
        }

        FixedVsFloatingSwap swap = MakeSwap(
            fixedSchedule, floatSchedule, exerciseRate, type, index, nominal, fixedLegDayCounter, floatingLegDayCounter,
            averagingMethod, context);
        return (swap, new Swaption(swap, exercise));
    }

    private static FixedVsFloatingSwap MakeSwap(
        Schedule fixedSchedule,
        Schedule floatSchedule,
        double exerciseRate,
        SwapType type,
        IborIndex index,
        double nominal,
        DayCounter? fixedLegDayCounter,
        DayCounter? floatingLegDayCounter,
        RateAveragingType averagingMethod,
        EvaluationContext context)
    {
        if (index is OvernightIndex onIndex)
        {
            return new OvernightIndexedSwap(
                type,
                nominal,
                fixedSchedule,
                exerciseRate,
                fixedLegDayCounter,
                floatSchedule,
                onIndex,
                0.0,
                0,
                BusinessDayConvention.Following,
                null,
                true,
                averagingMethod);
        }

        return new VanillaSwap(
            type,
            nominal,
            fixedSchedule,
            exerciseRate,
            fixedLegDayCounter,
            floatSchedule,
            index,
            0.0,
            floatingLegDayCounter,
            context);
    }

    // Instrument::NPV's check.
    private static double Npv(InstrumentResults results) => results.NPV ?? throw new NanoQLException("NPV not provided");
}
