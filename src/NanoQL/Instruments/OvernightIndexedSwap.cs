// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2026 Sergio Araujo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/overnightindexedswap.hpp ql/instruments/overnightindexedswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>An overnight-indexed swap: a fixed leg against compounded or averaged overnight rates.</summary>
/// <remarks>
/// <para>
/// The constructors read no evaluation date: the overnight coupons' value dates are always the full daily list, so
/// <c>telescopicValueDates</c> keeps only its effect on averaged coupons, whose pricer it switches to the averaging
/// approximation, and a telescopic swap gives QuantLib's full-schedule results.
/// </para>
/// <para>
/// The payment lag, adjustment and calendar apply to both legs; the overnight coupons accrue on the index's day
/// counter.
/// </para>
/// </remarks>
public sealed class OvernightIndexedSwap : FixedVsFloatingSwap
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OvernightIndexedSwap"/> class with one nominal and one schedule for
    /// both legs.
    /// </summary>
    /// <param name="type">Whether the fixed rate is paid or received.</param>
    /// <param name="nominal">The nominal of both legs.</param>
    /// <param name="schedule">The schedule of both legs.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDC">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="spread">The overnight leg's spread.</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentAdjustment">The payment dates' business-day convention.</param>
    /// <param name="paymentCalendar">
    /// The payment calendar; each schedule's calendar when <see langword="null"/>.
    /// </param>
    /// <param name="telescopicValueDates">QuantLib's telescopic value dates, which on averaged coupons select the
    /// averaging pricer's approximation.</param>
    /// <param name="averagingMethod">Whether the overnight rates are compounded or averaged.</param>
    /// <param name="lookbackDays">The lookback days; the index's fixing days when <see langword="null"/>.</param>
    /// <param name="lockoutDays">The lockout days.</param>
    /// <param name="applyObservationShift">Whether the observation shift applies.</param>
    /// <param name="roundingPrecision">
    /// The decimal places the overnight coupons' rates are rounded to before their amounts are computed, or
    /// <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument but a day counter or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum isn't a member of its type; the lookback or the lockout is
    /// negative; or the rounding precision is outside [0, 16].</exception>
    /// <exception cref="NanoQLException">
    /// A leg can't be built from the arguments, an empty schedule among them.
    /// </exception>
    public OvernightIndexedSwap(
        SwapType type,
        double nominal,
        Schedule schedule,
        double fixedRate,
        DayCounter? fixedDC,
        OvernightIndex overnightIndex,
        double spread = 0.0,
        int paymentLag = 0,
        BusinessDayConvention paymentAdjustment = BusinessDayConvention.Following,
        Calendar? paymentCalendar = null,
        bool telescopicValueDates = false,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        int? lookbackDays = null,
        int lockoutDays = 0,
        bool applyObservationShift = false,
        int? roundingPrecision = null)
        : this(
            ([nominal], [nominal]),
            (nameof(schedule), nameof(schedule)),
            type,
            schedule,
            fixedRate,
            fixedDC,
            schedule,
            overnightIndex,
            spread,
            paymentLag,
            paymentAdjustment,
            paymentCalendar,
            telescopicValueDates,
            averagingMethod,
            lookbackDays,
            lockoutDays,
            applyObservationShift,
            roundingPrecision)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OvernightIndexedSwap"/> class with one list of nominals and one
    /// schedule for both legs.
    /// </summary>
    /// <param name="type">Whether the fixed rate is paid or received.</param>
    /// <param name="nominals">The nominals of both legs; the last one repeats past the end.</param>
    /// <param name="schedule">The schedule of both legs.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDC">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="spread">The overnight leg's spread.</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentAdjustment">The payment dates' business-day convention.</param>
    /// <param name="paymentCalendar">
    /// The payment calendar; each schedule's calendar when <see langword="null"/>.
    /// </param>
    /// <param name="telescopicValueDates">QuantLib's telescopic value dates, which on averaged coupons select the
    /// averaging pricer's approximation.</param>
    /// <param name="averagingMethod">Whether the overnight rates are compounded or averaged.</param>
    /// <param name="lookbackDays">The lookback days; the index's fixing days when <see langword="null"/>.</param>
    /// <param name="lockoutDays">The lockout days.</param>
    /// <param name="applyObservationShift">Whether the observation shift applies.</param>
    /// <param name="roundingPrecision">
    /// The decimal places the overnight coupons' rates are rounded to before their amounts are computed, or
    /// <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument but a day counter or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum isn't a member of its type; the lookback or the lockout is
    /// negative; or the rounding precision is outside [0, 16].</exception>
    /// <exception cref="NanoQLException">
    /// A leg can't be built from the arguments, an empty schedule among them.
    /// </exception>
    public OvernightIndexedSwap(
        SwapType type,
        IEnumerable<double> nominals,
        Schedule schedule,
        double fixedRate,
        DayCounter? fixedDC,
        OvernightIndex overnightIndex,
        double spread = 0.0,
        int paymentLag = 0,
        BusinessDayConvention paymentAdjustment = BusinessDayConvention.Following,
        Calendar? paymentCalendar = null,
        bool telescopicValueDates = false,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        int? lookbackDays = null,
        int lockoutDays = 0,
        bool applyObservationShift = false,
        int? roundingPrecision = null)
        : this(
            Same(nominals),
            (nameof(schedule), nameof(schedule)),
            type,
            schedule,
            fixedRate,
            fixedDC,
            schedule,
            overnightIndex,
            spread,
            paymentLag,
            paymentAdjustment,
            paymentCalendar,
            telescopicValueDates,
            averagingMethod,
            lookbackDays,
            lockoutDays,
            applyObservationShift,
            roundingPrecision)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OvernightIndexedSwap"/> class with one nominal for both legs and a
    /// schedule for each.
    /// </summary>
    /// <param name="type">Whether the fixed rate is paid or received.</param>
    /// <param name="nominal">The nominal of both legs.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDC">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="overnightSchedule">The overnight leg's schedule.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="spread">The overnight leg's spread.</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentAdjustment">The payment dates' business-day convention.</param>
    /// <param name="paymentCalendar">
    /// The payment calendar; each schedule's calendar when <see langword="null"/>.
    /// </param>
    /// <param name="telescopicValueDates">QuantLib's telescopic value dates, which on averaged coupons select the
    /// averaging pricer's approximation.</param>
    /// <param name="averagingMethod">Whether the overnight rates are compounded or averaged.</param>
    /// <param name="lookbackDays">The lookback days; the index's fixing days when <see langword="null"/>.</param>
    /// <param name="lockoutDays">The lockout days.</param>
    /// <param name="applyObservationShift">Whether the observation shift applies.</param>
    /// <param name="roundingPrecision">
    /// The decimal places the overnight coupons' rates are rounded to before their amounts are computed, or
    /// <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument but a day counter or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum isn't a member of its type; the lookback or the lockout is
    /// negative; or the rounding precision is outside [0, 16].</exception>
    /// <exception cref="NanoQLException">
    /// A leg can't be built from the arguments, an empty schedule among them.
    /// </exception>
    public OvernightIndexedSwap(
        SwapType type,
        double nominal,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter? fixedDC,
        Schedule overnightSchedule,
        OvernightIndex overnightIndex,
        double spread = 0.0,
        int paymentLag = 0,
        BusinessDayConvention paymentAdjustment = BusinessDayConvention.Following,
        Calendar? paymentCalendar = null,
        bool telescopicValueDates = false,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        int? lookbackDays = null,
        int lockoutDays = 0,
        bool applyObservationShift = false,
        int? roundingPrecision = null)
        : this(
            ([nominal], [nominal]),
            (nameof(fixedSchedule), nameof(overnightSchedule)),
            type,
            fixedSchedule,
            fixedRate,
            fixedDC,
            overnightSchedule,
            overnightIndex,
            spread,
            paymentLag,
            paymentAdjustment,
            paymentCalendar,
            telescopicValueDates,
            averagingMethod,
            lookbackDays,
            lockoutDays,
            applyObservationShift,
            roundingPrecision)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OvernightIndexedSwap"/> class with nominals and a schedule for each
    /// leg.
    /// </summary>
    /// <param name="type">Whether the fixed rate is paid or received.</param>
    /// <param name="fixedNominals">The fixed coupons' nominals; the last one repeats past the end.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDC">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="overnightNominals">The overnight coupons' nominals; the last one repeats past the end.</param>
    /// <param name="overnightSchedule">The overnight leg's schedule.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="spread">The overnight leg's spread.</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentAdjustment">The payment dates' business-day convention.</param>
    /// <param name="paymentCalendar">
    /// The payment calendar; each schedule's calendar when <see langword="null"/>.
    /// </param>
    /// <param name="telescopicValueDates">QuantLib's telescopic value dates, which on averaged coupons select the
    /// averaging pricer's approximation.</param>
    /// <param name="averagingMethod">Whether the overnight rates are compounded or averaged.</param>
    /// <param name="lookbackDays">The lookback days; the index's fixing days when <see langword="null"/>.</param>
    /// <param name="lockoutDays">The lockout days.</param>
    /// <param name="applyObservationShift">Whether the observation shift applies.</param>
    /// <param name="roundingPrecision">
    /// The decimal places the overnight coupons' rates are rounded to before their amounts are computed, or
    /// <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument but a day counter or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum isn't a member of its type; the lookback or the lockout is
    /// negative; or the rounding precision is outside [0, 16].</exception>
    /// <exception cref="NanoQLException">
    /// A leg can't be built from the arguments, an empty schedule among them.
    /// </exception>
    public OvernightIndexedSwap(
        SwapType type,
        IEnumerable<double> fixedNominals,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter? fixedDC,
        IEnumerable<double> overnightNominals,
        Schedule overnightSchedule,
        OvernightIndex overnightIndex,
        double spread = 0.0,
        int paymentLag = 0,
        BusinessDayConvention paymentAdjustment = BusinessDayConvention.Following,
        Calendar? paymentCalendar = null,
        bool telescopicValueDates = false,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        int? lookbackDays = null,
        int lockoutDays = 0,
        bool applyObservationShift = false,
        int? roundingPrecision = null)
        : this(
            (Copy(fixedNominals), Copy(overnightNominals)),
            (nameof(fixedSchedule), nameof(overnightSchedule)),
            type,
            fixedSchedule,
            fixedRate,
            fixedDC,
            overnightSchedule,
            overnightIndex,
            spread,
            paymentLag,
            paymentAdjustment,
            paymentCalendar,
            telescopicValueDates,
            averagingMethod,
            lookbackDays,
            lockoutDays,
            applyObservationShift,
            roundingPrecision)
    {
    }

    // The fourth constructor's body, which the others delegate to, as QuantLib's do: each list of nominals copied once,
    // the checks on entry run first, and the overnight leg built by the base once it has checked its terms and built
    // the fixed leg, as QuantLib's constructor body builds it. The schedules' names are those of the calling
    // constructor's parameters, which a refusal of either schedule gives.
    private OvernightIndexedSwap(
        (ImmutableArray<double> Fixed, ImmutableArray<double> Overnight) nominals,
        (string Fixed, string Overnight) scheduleNames,
        SwapType type,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter? fixedDC,
        Schedule overnightSchedule,
        OvernightIndex overnightIndex,
        double spread,
        int paymentLag,
        BusinessDayConvention paymentAdjustment,
        Calendar? paymentCalendar,
        bool telescopicValueDates,
        RateAveragingType averagingMethod,
        int? lookbackDays,
        int lockoutDays,
        bool applyObservationShift,
        int? roundingPrecision)
        : base(
            EntryChecked(type, overnightSchedule, scheduleNames.Overnight, overnightIndex, lookbackDays, lockoutDays),
            nominals.Fixed,
            fixedSchedule,
            fixedRate,
            fixedDC,
            nominals.Overnight,
            overnightSchedule,
            overnightIndex,
            spread,
            null,
            paymentAdjustment,
            paymentLag,
            paymentCalendar,
            () => OvernightLegOf(
                nominals.Overnight,
                overnightSchedule,
                overnightIndex,
                spread,
                paymentLag,
                paymentAdjustment,
                paymentCalendar,
                telescopicValueDates,
                averagingMethod,
                lookbackDays,
                lockoutDays,
                applyObservationShift,
                roundingPrecision),
            scheduleNames.Fixed,
            nameof(paymentAdjustment))
    {
        OvernightIndex = overnightIndex;
        TelescopicValueDates = telescopicValueDates;
        AveragingMethod = averagingMethod;
        LookbackDays = lookbackDays;
        LockoutDays = lockoutDays;
        ApplyObservationShift = applyObservationShift;
        RoundingPrecision = roundingPrecision;
    }

    /// <summary>Gets the overnight leg's nominals, as given.</summary>
    public ImmutableArray<double> OvernightNominals => FloatingNominals;

    /// <summary>Gets the overnight leg's schedule.</summary>
    public Schedule OvernightSchedule => FloatingSchedule;

    /// <summary>Gets the overnight index.</summary>
    public OvernightIndex OvernightIndex { get; }

    /// <summary>Gets the overnight leg.</summary>
    public ImmutableArray<CashFlow> OvernightLeg => FloatingLeg;

    /// <summary>Gets whether the swap was built with QuantLib's telescopic value dates.</summary>
    public bool TelescopicValueDates { get; }

    /// <summary>Gets whether the overnight rates are compounded or averaged.</summary>
    public RateAveragingType AveragingMethod { get; }

    /// <summary>Gets the lookback days, or <see langword="null"/> for the index's fixing days.</summary>
    public int? LookbackDays { get; }

    /// <summary>Gets the lockout days.</summary>
    public int LockoutDays { get; }

    /// <summary>Gets whether the observation shift applies.</summary>
    public bool ApplyObservationShift { get; }

    /// <summary>
    /// Gets the decimal places the overnight coupons' rates are rounded to, or <see langword="null"/>.
    /// </summary>
    public int? RoundingPrecision { get; }

    /// <summary>The higher of the two schedules' payment frequencies.</summary>
    /// <returns>The frequency.</returns>
    /// <exception cref="NanoQLException">A schedule has no tenor, or its tenor has no frequency.</exception>
    public Frequency PaymentFrequency()
    {
        Frequency fixedFrequency = FixedSchedule.Tenor().Frequency();
        Frequency overnightFrequency = FloatingSchedule.Tenor().Frequency();

        // std::max
        return fixedFrequency < overnightFrequency ? overnightFrequency : fixedFrequency;
    }

    /// <inheritdoc/>
    public override OvernightIndexedSwapResults SetupExpired() => new(base.SetupExpired());

    /// <inheritdoc/>
    public override OvernightIndexedSwapResults FetchResults(InstrumentResults results)
    {
        FixedVsFloatingSwapResults r = base.FetchResults(results);
        return r as OvernightIndexedSwapResults ?? new OvernightIndexedSwapResults(r);
    }

    private static ImmutableArray<double> Copy(
        IEnumerable<double> nominals,
        [CallerArgumentExpression(nameof(nominals))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(nominals, paramName);
        return [.. nominals];
    }

    private static (ImmutableArray<double> Fixed, ImmutableArray<double> Overnight) Same(IEnumerable<double> nominals)
    {
        ImmutableArray<double> copy = Copy(nominals);
        return (copy, copy);
    }

    // The checks on entry, in the first argument so that they run before the base's: the overnight schedule and index
    // under the calling constructor's names, and the lookback and lockout days, QuantLib's unsigned Natural.
    private static SwapType EntryChecked(
        SwapType type,
        Schedule overnightSchedule,
        string overnightScheduleName,
        OvernightIndex overnightIndex,
        int? lookbackDays,
        int lockoutDays)
    {
        ArgumentNullException.ThrowIfNull(overnightSchedule, overnightScheduleName);
        CheckFloatingIndex(overnightIndex, nameof(overnightIndex));
        if (lookbackDays is int lookback)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(lookback, nameof(lookbackDays));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(lockoutDays);
        return type;
    }

    // QuantLib's constructor body: the overnight leg, which the base builds once it has checked its terms and built the
    // fixed leg, with QuantLib's setters and its rounding check in QuantLib's order.
    private static ImmutableArray<CashFlow> OvernightLegOf(
        ImmutableArray<double> overnightNominals,
        Schedule overnightSchedule,
        OvernightIndex overnightIndex,
        double spread,
        int paymentLag,
        BusinessDayConvention paymentAdjustment,
        Calendar? paymentCalendar,
        bool telescopicValueDates,
        RateAveragingType averagingMethod,
        int? lookbackDays,
        int lockoutDays,
        bool applyObservationShift,
        int? roundingPrecision)
    {
        // An empty overnight schedule is refused by the leg's conversion, after its notional check, as QuantLib's leg
        // reaches it.
        OvernightLeg leg = new OvernightLeg(overnightSchedule, overnightIndex)
            .WithNotionals(overnightNominals)
            .WithSpreads(spread)
            .WithTelescopicValueDates(telescopicValueDates)
            .WithPaymentLag(paymentLag)
            .WithPaymentAdjustment(paymentAdjustment)
            .WithPaymentCalendar(paymentCalendar ?? overnightSchedule.Calendar)
            .WithAveragingMethod(averagingMethod)
            .WithLookbackDays(lookbackDays)
            .WithLockoutDays(lockoutDays)
            .WithObservationShift(applyObservationShift);
        if (roundingPrecision is int precision)
        {
            Rounding.CheckPrecision(precision, nameof(roundingPrecision));
            leg = leg.WithRoundingPrecision(precision);
        }

        return leg.ToLeg();
    }
}
