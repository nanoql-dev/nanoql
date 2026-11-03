// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mario Pucci
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/rangeaccrual.hpp ql/cashflows/rangeaccrual.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Coupons;

/// <summary>A helper class building a sequence of range-accrual floating-rate coupons: QuantLib's
/// <c>RangeAccrualLeg</c>.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy. <see cref="ToLeg"/> builds the leg, in place of
/// QuantLib's conversion operator: one coupon per schedule period, a fixed coupon at the spread where the gearing is
/// zero, each floating coupon observing the index on a schedule of the observation tenor from its start to its end. No
/// pricer is set. A builder has no value equality.
/// </para>
/// <para>
/// QuantLib's leg is built as <c>Leg leg(n)</c> and then filled with <c>push_back</c>, so it holds n null cash flows
/// before its n coupons; here it holds the coupons alone. With no observation tenor, QuantLib's <c>Period()</c>, a
/// coupon has no observation dates and QuantLib's rate is 0/0; here such a leg is refused. A missing trigger is
/// QuantLib's <c>Null&lt;Rate&gt;()</c>, its value, which a coupon's checks and pricer read as QuantLib's do.
/// </para>
/// </remarks>
public sealed record RangeAccrualLeg
{
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>Initializes a new instance of the <see cref="RangeAccrualLeg"/> record.</summary>
    /// <param name="schedule">The schedule of accrual dates.</param>
    /// <param name="index">The index observed and paid.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public RangeAccrualLeg(Schedule schedule, IborIndex index)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(index);
        Schedule = schedule;
        Index = index;
    }

    private Schedule Schedule { get; init; }

    private IborIndex Index { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

    private ImmutableArray<int> FixingDays { get; init; } = [];

    private ImmutableArray<double> Gearings { get; init; } = [];

    private ImmutableArray<double> Spreads { get; init; } = [];

    private ImmutableArray<double> LowerTriggers { get; init; } = [];

    private ImmutableArray<double> UpperTriggers { get; init; } = [];

    private Period ObservationTenor { get; init; }

    private BusinessDayConvention ObservationConvention { get; init; } = BusinessDayConvention.ModifiedFollowing;

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public RangeAccrualLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals, the coupons after the last taking the last.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notionals"/> is null.</exception>
    public RangeAccrualLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the payment day counter, which also measures the coupons' times.</summary>
    /// <param name="dayCounter">The day counter, or <see langword="null"/>, QuantLib's empty day counter.</param>
    /// <returns>The modified builder.</returns>
    public RangeAccrualLeg WithPaymentDayCounter(DayCounter? dayCounter) => this with { PaymentDayCounter = dayCounter };

    /// <summary>Sets the convention adjusting the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public RangeAccrualLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets one number of fixing days for every coupon.</summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public RangeAccrualLeg WithFixingDays(int fixingDays) => WithFixingDays([fixingDays]);

    /// <summary>Sets the coupons' fixing days, the coupons after the last taking the last, 2 when none is
    /// given.</summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fixingDays"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of fixing days is negative.</exception>
    public RangeAccrualLeg WithFixingDays(IEnumerable<int> fixingDays)
    {
        ArgumentNullException.ThrowIfNull(fixingDays);
        ImmutableArray<int> days = [.. fixingDays];
        foreach (int d in days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(d, nameof(fixingDays));
        }

        return this with { FixingDays = days };
    }

    /// <summary>Sets one gearing for every coupon.</summary>
    /// <param name="gearing">The gearing.</param>
    /// <returns>The modified builder.</returns>
    public RangeAccrualLeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings, the coupons after the last taking the last, 1 when none is given.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gearings"/> is null.</exception>
    public RangeAccrualLeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one spread for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public RangeAccrualLeg WithSpreads(double spread) => this with { Spreads = [spread] };

    /// <summary>Sets the coupons' spreads, the coupons after the last taking the last, 0 when none is given.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spreads"/> is null.</exception>
    public RangeAccrualLeg WithSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { Spreads = [.. spreads] };
    }

    /// <summary>Sets one lower trigger for every coupon.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <returns>The modified builder.</returns>
    public RangeAccrualLeg WithLowerTriggers(double trigger) => this with { LowerTriggers = [trigger] };

    /// <summary>Sets the coupons' lower triggers, the coupons after the last taking the last, QuantLib's
    /// <c>Null&lt;Rate&gt;()</c> when none is given.</summary>
    /// <param name="triggers">The triggers.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="triggers"/> is null.</exception>
    public RangeAccrualLeg WithLowerTriggers(IEnumerable<double> triggers)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        return this with { LowerTriggers = [.. triggers] };
    }

    /// <summary>Sets one upper trigger for every coupon.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <returns>The modified builder.</returns>
    public RangeAccrualLeg WithUpperTriggers(double trigger) => this with { UpperTriggers = [trigger] };

    /// <summary>Sets the coupons' upper triggers, the coupons after the last taking the last, QuantLib's
    /// <c>Null&lt;Rate&gt;()</c> when none is given.</summary>
    /// <param name="triggers">The triggers.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="triggers"/> is null.</exception>
    public RangeAccrualLeg WithUpperTriggers(IEnumerable<double> triggers)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        return this with { UpperTriggers = [.. triggers] };
    }

    /// <summary>Sets the tenor of the observation schedules.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public RangeAccrualLeg WithObservationTenor(Period tenor) => this with { ObservationTenor = tenor };

    /// <summary>Sets the convention of the observation schedules.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public RangeAccrualLeg WithObservationConvention(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { ObservationConvention = convention };
    }

    /// <summary>Builds the leg.</summary>
    /// <param name="context">The evaluation context the observation schedules are built under.</param>
    /// <returns>The leg, one coupon per schedule period.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">No notional was given; more notionals, fixing days, gearings, spreads or
    /// triggers were given than there are periods; or a floating coupon is built with no observation tenor; each with
    /// QuantLib's text where it has one.</exception>
    /// <exception cref="ArgumentException">A coupon refuses its terms, with QuantLib's text.</exception>
    public ImmutableArray<CashFlow> ToLeg(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Notionals.IsEmpty)
        {
            throw new NanoQLException("no notional given");
        }

        Schedule schedule = Schedule;
        int n = schedule.Size - 1;
        CheckSize(Notionals.Length, n, "nominals");
        CheckSize(FixingDays.Length, n, "fixingDays");
        CheckSize(Gearings.Length, n, "gearings");
        CheckSize(Spreads.Length, n, "spreads");
        CheckSize(LowerTriggers.Length, n, "lowerTriggers");
        CheckSize(UpperTriggers.Length, n, "upperTriggers");

        var leg = ImmutableArray.CreateBuilder<CashFlow>(Math.Max(n, 0));

        // the following is not always correct
        Calendar calendar = schedule.Calendar ?? EmptyCalendar.Instance;
        for (int i = 0; i < n; ++i)
        {
            Date refStart, start, refEnd, end;
            refStart = start = schedule.Date(i);
            refEnd = end = schedule.Date(i + 1);
            Date paymentDate = calendar.Adjust(end, PaymentAdjustment);
            if (i == 0 && schedule.HasIsRegular && !schedule.IsRegular(i + 1))
            {
                BusinessDayConvention bdc = schedule.BusinessDayConvention;
                refStart = calendar.Adjust(end - schedule.Tenor(), bdc);
            }

            if (i == n - 1 && schedule.HasIsRegular && !schedule.IsRegular(i + 1))
            {
                BusinessDayConvention bdc = schedule.BusinessDayConvention;
                refEnd = calendar.Adjust(start + schedule.Tenor(), bdc);
            }

            if (VectorsFunctions.Get(Gearings, i, 1.0) == 0.0)
            {
                // fixed coupon
                leg.Add(new FixedRateCoupon(
                    paymentDate,
                    VectorsFunctions.Get(Notionals, i, NullReal),
                    VectorsFunctions.Get(Spreads, i, 0.0),
                    PaymentDayCounter ?? throw new NanoQLException("no day counter implementation provided"),
                    start,
                    end,
                    refStart,
                    refEnd));
            }
            else
            {
                // floating coupon
                if (ObservationTenor.Length == 0)
                {
                    throw new NanoQLException(
                        "no observation tenor given: a range-accrual coupon needs observation dates between its start and end");
                }

                var observationSchedule = new Schedule(
                    start, end, ObservationTenor, calendar, ObservationConvention, ObservationConvention, DateGenerationRule.Forward, false, context);
                leg.Add(new RangeAccrualFloatersCoupon(
                    paymentDate,
                    VectorsFunctions.Get(Notionals, i, NullReal),
                    Index,
                    start,
                    end,
                    VectorsFunctions.Get(FixingDays, i, 2),
                    PaymentDayCounter,
                    VectorsFunctions.Get(Gearings, i, 1.0),
                    VectorsFunctions.Get(Spreads, i, 0.0),
                    refStart,
                    refEnd,
                    observationSchedule,
                    VectorsFunctions.Get(LowerTriggers, i, NullReal),
                    VectorsFunctions.Get(UpperTriggers, i, NullReal)));
            }
        }

        return leg.ToImmutable();
    }

    private static void CheckSize(int given, int n, string what)
    {
        if (!(given <= n))
        {
            throw new NanoQLException($"too many {what} ({given}), only {n} required");
        }
    }
}
