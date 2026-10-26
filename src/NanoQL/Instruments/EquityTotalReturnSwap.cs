// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/equitytotalreturnswap.hpp ql/instruments/equitytotalreturnswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// An equity total return swap: the total return of an equity index, N·[I(T_M)/I(T_0) − 1], exchanged for floating
/// cash flows on an Ibor index, or on an overnight index compounded over each accrual period.
/// </summary>
/// <remarks>
/// Immutable. The equity leg is the first, its one cash flow paid the payment delay's days after the schedule's end on
/// the payment calendar, or on the schedule's own; the interest-rate leg is the second. The swap's type refers to the
/// equity leg: a payer pays the equity return and receives the floating leg.
/// </remarks>
public sealed class EquityTotalReturnSwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>Initializes a new instance of the <see cref="EquityTotalReturnSwap"/> class on an Ibor index.</summary>
    /// <param name="type">Whether the equity return is paid (payer) or received.</param>
    /// <param name="nominal">The swap's nominal.</param>
    /// <param name="schedule">The schedule of the interest-rate leg, whose start and end date the equity leg.</param>
    /// <param name="equityIndex">The equity index.</param>
    /// <param name="interestRateIndex">The Ibor index of the interest-rate leg.</param>
    /// <param name="dayCounter">The interest-rate leg's payment day counter.</param>
    /// <param name="margin">The margin over the interest-rate index.</param>
    /// <param name="context">The evaluation context, whose at-par switch the interest-rate leg's pricer reads.</param>
    /// <param name="gearing">The gearing of the interest-rate index.</param>
    /// <param name="paymentCalendar">The payment calendar, or <see langword="null"/> for the schedule's.</param>
    /// <param name="paymentConvention">The payment dates' convention.</param>
    /// <param name="paymentDelay">The payment delay, in days.</param>
    /// <exception cref="ArgumentNullException">An argument but the payment calendar is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or the convention isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The nominal is negative.</exception>
    /// <exception cref="NanoQLException">
    /// Neither the payment calendar nor the schedule has a calendar, or the interest-rate leg can't be built.
    /// </exception>
    public EquityTotalReturnSwap(
        SwapType type,
        double nominal,
        Schedule schedule,
        EquityIndex equityIndex,
        IborIndex interestRateIndex,
        DayCounter dayCounter,
        double margin,
        EvaluationContext context,
        double gearing = 1.0,
        Calendar? paymentCalendar = null,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Unadjusted,
        int paymentDelay = 0)
        : base(
            BuildLegs(
                type,
                nominal,
                schedule,
                equityIndex,
                interestRateIndex,
                dayCounter,
                paymentCalendar,
                paymentConvention,
                paymentDelay,
                () => new IborLeg(schedule, interestRateIndex)
                    .WithNotionals(nominal)
                    .WithPaymentDayCounter(dayCounter)
                    .WithSpreads(margin)
                    .WithGearings(gearing)
                    .WithPaymentCalendar(paymentCalendar)
                    .WithPaymentAdjustment(paymentConvention)
                    .WithPaymentLag(paymentDelay)
                    .ToLeg(context ?? throw new ArgumentNullException(nameof(context)))),
            [type == SwapType.Payer, type == SwapType.Receiver])
    {
        Type = type;
        Nominal = nominal;
        Schedule = schedule;
        EquityIndex = equityIndex;
        InterestRateIndex = interestRateIndex;
        DayCounter = dayCounter;
        Margin = margin;
        Gearing = gearing;
        PaymentCalendar = paymentCalendar;
        PaymentConvention = paymentConvention;
        PaymentDelay = paymentDelay;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EquityTotalReturnSwap"/> class on an overnight index, compounded
    /// over each accrual period.
    /// </summary>
    /// <param name="type">Whether the equity return is paid (payer) or received.</param>
    /// <param name="nominal">The swap's nominal.</param>
    /// <param name="schedule">The schedule of the interest-rate leg, whose start and end date the equity leg.</param>
    /// <param name="equityIndex">The equity index.</param>
    /// <param name="interestRateIndex">The overnight index of the interest-rate leg.</param>
    /// <param name="dayCounter">The interest-rate leg's payment day counter.</param>
    /// <param name="margin">The margin over the interest-rate index.</param>
    /// <param name="gearing">The gearing of the interest-rate index.</param>
    /// <param name="paymentCalendar">The payment calendar, or <see langword="null"/> for the schedule's.</param>
    /// <param name="paymentConvention">The payment dates' convention.</param>
    /// <param name="paymentDelay">The payment delay, in days.</param>
    /// <exception cref="ArgumentNullException">An argument but the payment calendar is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or the convention isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The nominal is negative.</exception>
    /// <exception cref="NanoQLException">
    /// Neither the payment calendar nor the schedule has a calendar, or the interest-rate leg can't be built.
    /// </exception>
    public EquityTotalReturnSwap(
        SwapType type,
        double nominal,
        Schedule schedule,
        EquityIndex equityIndex,
        OvernightIndex interestRateIndex,
        DayCounter dayCounter,
        double margin,
        double gearing = 1.0,
        Calendar? paymentCalendar = null,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Unadjusted,
        int paymentDelay = 0)
        : base(
            BuildLegs(
                type,
                nominal,
                schedule,
                equityIndex,
                interestRateIndex,
                dayCounter,
                paymentCalendar,
                paymentConvention,
                paymentDelay,
                () => new OvernightLeg(schedule, interestRateIndex)
                    .WithNotionals(nominal)
                    .WithPaymentDayCounter(dayCounter)
                    .WithSpreads(margin)
                    .WithGearings(gearing)
                    .WithPaymentCalendar(paymentCalendar)
                    .WithPaymentAdjustment(paymentConvention)
                    .WithPaymentLag(paymentDelay)
                    .ToLeg()),
            [type == SwapType.Payer, type == SwapType.Receiver])
    {
        Type = type;
        Nominal = nominal;
        Schedule = schedule;
        EquityIndex = equityIndex;
        InterestRateIndex = interestRateIndex;
        DayCounter = dayCounter;
        Margin = margin;
        Gearing = gearing;
        PaymentCalendar = paymentCalendar;
        PaymentConvention = paymentConvention;
        PaymentDelay = paymentDelay;
    }

    /// <summary>Gets whether the equity return is paid (payer) or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the swap's nominal.</summary>
    public double Nominal { get; }

    /// <summary>Gets the equity index.</summary>
    public EquityIndex EquityIndex { get; }

    /// <summary>Gets the interest-rate leg's index.</summary>
    public InterestRateIndex InterestRateIndex { get; }

    /// <summary>Gets the interest-rate leg's schedule.</summary>
    public Schedule Schedule { get; }

    /// <summary>Gets the interest-rate leg's payment day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets the margin over the interest-rate index.</summary>
    public double Margin { get; }

    /// <summary>Gets the gearing of the interest-rate index.</summary>
    public double Gearing { get; }

    /// <summary>Gets the payment calendar, or <see langword="null"/> for the schedule's.</summary>
    public Calendar? PaymentCalendar { get; }

    /// <summary>Gets the payment dates' convention.</summary>
    public BusinessDayConvention PaymentConvention { get; }

    /// <summary>Gets the payment delay, in days.</summary>
    public int PaymentDelay { get; }

    /// <summary>Gets the equity leg.</summary>
    public ImmutableArray<CashFlow> EquityLeg => Leg(0);

    /// <summary>Gets the interest-rate leg.</summary>
    public ImmutableArray<CashFlow> InterestRateLeg => Leg(1);

    /// <summary>The equity leg's NPV in a swap engine's results.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The NPV, signed by whether the leg is paid, or <see langword="null"/> where QuantLib refuses "result
    /// not available".</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public static double? EquityLegNPV(SwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Element(results.LegNPV, 0);
    }

    /// <summary>The interest-rate leg's NPV in a swap engine's results.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The NPV, signed by whether the leg is paid, or <see langword="null"/> where QuantLib refuses "result
    /// not available".</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public static double? InterestRateLegNPV(SwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Element(results.LegNPV, 1);
    }

    /// <summary>
    /// The margin that gives the swap a zero NPV in a swap engine's results: QuantLib's <c>fairMargin</c>,
    /// −(equity leg NPV + interest-rate leg NPV without its margin) / interest-rate leg BPS per unit of rate.
    /// </summary>
    /// <param name="results">The results.</param>
    /// <returns>The fair margin.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A leg's NPV or the interest-rate leg's BPS isn't in the results, QuantLib's
    /// "result not available".</exception>
    public double FairMargin(SwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);

        // Knowing that for the fair margin NPV = 0.0, where:
        // NPV = NPV Equity Leg + [NPV Floating Leg + margin * BPS / 10000]
        // hence,
        // fair margin = - [NPV Equity Leg + NPV Floating Leg] / BPS * 10000
        double interestLegBps = Available(Element(results.LegBPS, 1)) / BasisPoint;
        double exMarginInterestLegNpv = Available(InterestRateLegNPV(results)) - (Margin * interestLegBps);
        return -(Available(EquityLegNPV(results)) + exMarginInterestLegNpv) / interestLegBps;
    }

    private static ImmutableArray<CashFlow>[] BuildLegs(
        SwapType type,
        double nominal,
        Schedule schedule,
        EquityIndex equityIndex,
        InterestRateIndex interestRateIndex,
        DayCounter dayCounter,
        Calendar? paymentCalendar,
        BusinessDayConvention paymentConvention,
        int paymentDelay,
        Func<ImmutableArray<CashFlow>> interestLeg)
    {
        EnumArguments.Check(type, nameof(type));
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(equityIndex);
        ArgumentNullException.ThrowIfNull(interestRateIndex);
        ArgumentNullException.ThrowIfNull(dayCounter);
        if (nominal < 0.0)
        {
            throw new ArgumentException("Nominal cannot be negative", nameof(nominal));
        }

        // the equity cash flow, over the schedule's start and end
        Date startDate = schedule.StartDate();
        Date endDate = schedule.EndDate();
        Calendar cal = paymentCalendar ?? schedule.Calendar
            ?? throw new NanoQLException("Calendar in schedule cannot be empty");
        Date paymentDate = cal.Advance(endDate, paymentDelay, TimeUnit.Days, paymentConvention, schedule.EndOfMonth());
        CashFlow equity = new EquityCashFlow(nominal, equityIndex, startDate, endDate, paymentDate);
        return [[equity], interestLeg()];
    }

    private static double Available(double? value) => value ?? throw new NanoQLException("result not available");

    private static double? Element(ImmutableArray<double?> values, int j) =>
        !values.IsDefault && j < values.Length ? values[j] : null;
}
