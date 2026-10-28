// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catbond.hpp ql/experimental/catbonds/catbond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A floating-rate cat bond, possibly capped and/or floored: a floating-rate bond's leg under a notional
/// risk.</summary>
/// <remarks>The leg is the floating-rate bond's, built as QuantLib's is; QuantLib's ex-coupon and fixing-convention
/// options, which its cat bond doesn't take, keep their defaults.</remarks>
[Experimental("NQL0001")]
public sealed class FloatingCatBond : CatBond
{
    /// <summary>Initializes a new instance of the <see cref="FloatingCatBond"/> class on a schedule.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The face amount.</param>
    /// <param name="schedule">The coupons' schedule.</param>
    /// <param name="iborIndex">The index.</param>
    /// <param name="accrualDayCounter">The coupons' day counter, or <see langword="null"/> for the index's.</param>
    /// <param name="notionalRisk">The notional risk.</param>
    /// <param name="context">The evaluation context the leg is built in.</param>
    /// <param name="paymentConvention">The payments' convention.</param>
    /// <param name="fixingDays">The fixing days, or <see langword="null"/> for the index's.</param>
    /// <param name="gearings">The gearings; 1 when <see langword="null"/>.</param>
    /// <param name="spreads">The spreads; 0 when <see langword="null"/>.</param>
    /// <param name="caps">The caps; none when <see langword="null"/>.</param>
    /// <param name="floors">The floors; none when <see langword="null"/>.</param>
    /// <param name="inArrears">Whether the coupons fix in arrears.</param>
    /// <param name="redemption">The redemption, a percentage of the face amount.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The leg is empty ("bond with no cashflows!") or has more than one redemption
    /// ("multiple redemptions created").</exception>
    public FloatingCatBond(
        int settlementDays,
        double faceAmount,
        Schedule schedule,
        IborIndex iborIndex,
        DayCounter? accrualDayCounter,
        NotionalRisk notionalRisk,
        EvaluationContext context,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        int? fixingDays = null,
        IEnumerable<double>? gearings = null,
        IEnumerable<double>? spreads = null,
        IEnumerable<double?>? caps = null,
        IEnumerable<double?>? floors = null,
        bool inArrears = false,
        double redemption = 100.0,
        Date? issueDate = null)
        : base(settlementDays, CalendarOf(schedule, context), issueDate, notionalRisk)
    {
        maturityDate = schedule.EndDate();
        Leg(schedule, faceAmount, iborIndex, accrualDayCounter, context, paymentConvention, fixingDays, gearings, spreads, caps, floors, inArrears, redemption);
    }

    /// <summary>Initializes a new instance of the <see cref="FloatingCatBond"/> class on its dates, the schedule
    /// generated from them.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The face amount.</param>
    /// <param name="startDate">The schedule's start.</param>
    /// <param name="maturityDate">The maturity.</param>
    /// <param name="couponFrequency">The coupons' frequency.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="iborIndex">The index.</param>
    /// <param name="accrualDayCounter">The coupons' day counter, or <see langword="null"/> for the index's.</param>
    /// <param name="notionalRisk">The notional risk.</param>
    /// <param name="context">The evaluation context the leg is built in.</param>
    /// <param name="accrualConvention">The schedule's convention.</param>
    /// <param name="paymentConvention">The payments' convention.</param>
    /// <param name="fixingDays">The fixing days, or <see langword="null"/> for the index's.</param>
    /// <param name="gearings">The gearings; 1 when <see langword="null"/>.</param>
    /// <param name="spreads">The spreads; 0 when <see langword="null"/>.</param>
    /// <param name="caps">The caps; none when <see langword="null"/>.</param>
    /// <param name="floors">The floors; none when <see langword="null"/>.</param>
    /// <param name="inArrears">Whether the coupons fix in arrears.</param>
    /// <param name="redemption">The redemption, a percentage of the face amount.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="stubDate">The stub date, the first date under the forward rule and the next-to-last under the
    /// backward one, or <see langword="null"/> for none.</param>
    /// <param name="rule">The date-generation rule, backward or forward.</param>
    /// <param name="endOfMonth">Whether dates stay at month end.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The rule is neither backward nor forward ("stub date (…) not allowed with
    /// … DateGeneration::Rule", or "unknown DateGeneration::Rule (…)").</exception>
    /// <exception cref="NanoQLException">The leg is empty or has more than one redemption, or the schedule can't be
    /// generated.</exception>
    public FloatingCatBond(
        int settlementDays,
        double faceAmount,
        Date startDate,
        Date maturityDate,
        Frequency couponFrequency,
        Calendar calendar,
        IborIndex iborIndex,
        DayCounter? accrualDayCounter,
        NotionalRisk notionalRisk,
        EvaluationContext context,
        BusinessDayConvention accrualConvention = BusinessDayConvention.Following,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        int? fixingDays = null,
        IEnumerable<double>? gearings = null,
        IEnumerable<double>? spreads = null,
        IEnumerable<double?>? caps = null,
        IEnumerable<double?>? floors = null,
        bool inArrears = false,
        double redemption = 100.0,
        Date? issueDate = null,
        Date? stubDate = null,
        DateGenerationRule rule = DateGenerationRule.Backward,
        bool endOfMonth = false)
        : base(settlementDays, calendar ?? throw new ArgumentNullException(nameof(calendar)), issueDate, notionalRisk)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.maturityDate = maturityDate;
        Date? firstDate, nextToLastDate;
        switch (rule)
        {
            case DateGenerationRule.Backward:
                firstDate = null;
                nextToLastDate = stubDate;
                break;
            case DateGenerationRule.Forward:
                firstDate = stubDate;
                nextToLastDate = null;
                break;
            case DateGenerationRule.Zero:
            case DateGenerationRule.ThirdWednesday:
            case DateGenerationRule.Twentieth:
            case DateGenerationRule.TwentiethIMM:
                throw new ArgumentException(
                    $"stub date ({(stubDate is Date s ? s.ToString() : "null date")}) not allowed with {rule} DateGeneration::Rule",
                    nameof(rule));
            default:
                throw new ArgumentException($"unknown DateGeneration::Rule ({(int)rule})", nameof(rule));
        }

        var schedule = new Schedule(
            startDate,
            maturityDate,
            new Period(couponFrequency),
            calendar,
            accrualConvention,
            accrualConvention,
            rule,
            endOfMonth,
            context,
            firstDate,
            nextToLastDate);
        Leg(schedule, faceAmount, iborIndex, accrualDayCounter, context, paymentConvention, fixingDays, gearings, spreads, caps, floors, inArrears, redemption);
    }

    // The settlement calendar, the schedule's, after the context is checked, as every member taking it checks it first.
    private static Calendar? CalendarOf(Schedule schedule, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Calendar;
    }

    // The floating leg and its redemption, the floating-rate bond's.
    private void Leg(
        Schedule schedule,
        double faceAmount,
        IborIndex iborIndex,
        DayCounter? accrualDayCounter,
        EvaluationContext context,
        BusinessDayConvention paymentConvention,
        int? fixingDays,
        IEnumerable<double>? gearings,
        IEnumerable<double>? spreads,
        IEnumerable<double?>? caps,
        IEnumerable<double?>? floors,
        bool inArrears,
        double redemption)
    {
        if (fixingDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(fixingDays));
        }

        EnumArguments.Check(paymentConvention, nameof(paymentConvention));

        // QuantLib's leg builder refuses a null index under its own parameter's name
        if (iborIndex is null)
        {
            throw new ArgumentNullException(nameof(iborIndex), "no index provided");
        }

        cashflows = new IborLeg(schedule, iborIndex)
            .WithNotionals(faceAmount)
            .WithPaymentDayCounter(accrualDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithFixingDays(fixingDays)
            .WithGearings(gearings ?? [1.0])
            .WithSpreads(spreads ?? [0.0])
            .WithCaps(caps ?? [])
            .WithFloors(floors ?? [])
            .InArrears(inArrears)
            .ToLeg(context);

        AddRedemptionsToCashflows([redemption]);

        if (Cashflows.IsEmpty)
        {
            throw new NanoQLException("bond with no cashflows!");
        }

        if (redemptions.Length != 1)
        {
            throw new NanoQLException("multiple redemptions created");
        }
    }
}
