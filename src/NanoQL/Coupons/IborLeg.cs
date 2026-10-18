// SPDX-FileCopyrightText: Copyright (C) 2007, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/iborcoupon.hpp ql/cashflows/iborcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A builder of a sequence of Ibor coupons, capped, floored or not.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToLeg"/> builds the leg, in place of QuantLib's conversion operator; it takes the context because
/// the default pricer it attaches reads the at-par switch when the leg has no explicit choice. A builder has no value
/// equality: its vectors compare by reference.
/// </para>
/// <para>
/// A leg with neither caps, floors nor in-arrears fixing gets a <see cref="BlackIborCouponPricer"/> with no
/// volatility, as QuantLib's does; any other leg is built without pricers, for
/// <see cref="CouponPricerFunctions.SetCouponPricer(ImmutableArray{CashFlow}, FloatingRateCouponPricer)"/> to set.
/// </para>
/// </remarks>
public sealed record IborLeg
{
    /// <summary>Initializes a new instance of the <see cref="IborLeg"/> class.</summary>
    /// <param name="schedule">The schedule of accrual dates.</param>
    /// <param name="index">The index.</param>
    /// <exception cref="ArgumentException">The schedule is empty.</exception>
    public IborLeg(Schedule schedule, IborIndex index)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(index);
        if (schedule.Empty)
        {
            // QuantLib's period count wraps below zero for an empty schedule.
            throw new ArgumentException("empty schedule", nameof(schedule));
        }

        Schedule = schedule;
        Index = index;
    }

    private Schedule Schedule { get; init; }

    private IborIndex Index { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

    private int PaymentLag { get; init; }

    private Calendar? PaymentCalendar { get; init; }

    private ImmutableArray<int?> FixingDays { get; init; } = [];

    private ImmutableArray<double> Gearings { get; init; } = [];

    private ImmutableArray<double> Spreads { get; init; } = [];

    private ImmutableArray<double?> Caps { get; init; } = [];

    private ImmutableArray<double?> Floors { get; init; } = [];

    private bool IsInArrears { get; init; }

    private bool ZeroPayments { get; init; }

    private BusinessDayConvention FixingConvention { get; init; } = BusinessDayConvention.Preceding;

    private Period ExCouponPeriod { get; init; }

    private Calendar? ExCouponCalendar { get; init; }

    private BusinessDayConvention ExCouponAdjustment { get; init; } = BusinessDayConvention.Unadjusted;

    private bool ExCouponEndOfMonth { get; init; }

    private bool? UseIndexedCoupons { get; init; }

    private StubIndexSelection? Selection { get; init; }

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals; the last one repeats for any further coupons.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the accrual day counter; the index's by default.</summary>
    /// <param name="dayCounter">The day counter.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithPaymentDayCounter(DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        return this with { PaymentDayCounter = dayCounter };
    }

    /// <summary>Sets the business-day convention of the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public IborLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets the number of business days from each accrual end to its payment date.</summary>
    /// <param name="lag">The lag.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithPaymentLag(int lag) => this with { PaymentLag = lag };

    /// <summary>Sets the calendar of the payment dates; the schedule's calendar by default.</summary>
    /// <param name="cal">The calendar.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithPaymentCalendar(Calendar cal)
    {
        ArgumentNullException.ThrowIfNull(cal);
        return this with { PaymentCalendar = cal };
    }

    /// <summary>Sets one count of fixing days for every coupon.</summary>
    /// <param name="fixingDays">The fixing days; the index's when <see langword="null"/>, QuantLib's
    /// <c>Null&lt;Natural&gt;()</c>.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public IborLeg WithFixingDays(int? fixingDays) => WithFixingDays([fixingDays]);

    /// <summary>Sets the coupons' fixing days; the last count repeats for any further coupons.</summary>
    /// <param name="fixingDays">The fixing days, null elements taking the index's.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An element is negative.</exception>
    public IborLeg WithFixingDays(IEnumerable<int?> fixingDays)
    {
        ArgumentNullException.ThrowIfNull(fixingDays);
        ImmutableArray<int?> days = [.. fixingDays];
        foreach (int? d in days)
        {
            if (d is int count)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(fixingDays));
            }
        }

        return this with { FixingDays = days };
    }

    /// <summary>Sets one gearing for every coupon.</summary>
    /// <param name="gearing">The gearing; a zero gearing makes a fixed-rate coupon paying the spread.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings; the last one repeats for any further coupons.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one spread for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithSpreads(double spread) => this with { Spreads = [spread] };

    /// <summary>Sets the coupons' spreads; the last one repeats for any further coupons.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { Spreads = [.. spreads] };
    }

    /// <summary>Sets one cap for every coupon's rate.</summary>
    /// <param name="cap">The cap.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithCaps(double cap) => this with { Caps = [cap] };

    /// <summary>Sets the caps on the coupons' rates; the last one repeats for any further coupons.</summary>
    /// <param name="caps">The caps, null elements for none.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithCaps(IEnumerable<double?> caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        return this with { Caps = [.. caps] };
    }

    /// <summary>Sets one floor for every coupon's rate.</summary>
    /// <param name="floor">The floor.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithFloors(double floor) => this with { Floors = [floor] };

    /// <summary>Sets the floors on the coupons' rates; the last one repeats for any further coupons.</summary>
    /// <param name="floors">The floors, null elements for none.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithFloors(IEnumerable<double?> floors)
    {
        ArgumentNullException.ThrowIfNull(floors);
        return this with { Floors = [.. floors] };
    }

    /// <summary>Sets whether the coupons fix at the end of their periods.</summary>
    /// <param name="flag">Whether they do.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg InArrears(bool flag = true) => this with { IsInArrears = flag };

    /// <summary>Sets whether every coupon is paid on the last payment date.</summary>
    /// <param name="flag">Whether it is.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithZeroPayments(bool flag = true) => this with { ZeroPayments = flag };

    /// <summary>Sets the ex-coupon period, counted back from each payment date.</summary>
    /// <param name="period">The period; a zero-length period means no ex-coupon dates.</param>
    /// <param name="cal">The calendar the period is counted on.</param>
    /// <param name="convention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="endOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public IborLeg WithExCouponPeriod(
        Period period,
        Calendar cal,
        BusinessDayConvention convention,
        bool endOfMonth = false)
    {
        ArgumentNullException.ThrowIfNull(cal);
        EnumArguments.Check(convention, nameof(convention));
        return this with
        {
            ExCouponPeriod = period,
            ExCouponCalendar = cal,
            ExCouponAdjustment = convention,
            ExCouponEndOfMonth = endOfMonth,
        };
    }

    /// <summary>Sets the convention adjusting the fixing dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public IborLeg WithFixingConvention(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { FixingConvention = convention };
    }

    /// <summary>Sets whether the leg's default pricer prices indexed coupons, rather than at-par ones.</summary>
    /// <param name="b">Whether it does; the context's at-par switch decides when <see langword="null"/>.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithIndexedCoupons(bool? b = true) => this with { UseIndexedCoupons = b };

    /// <summary>Sets whether the leg's default pricer prices at-par coupons, rather than indexed ones.</summary>
    /// <param name="b">Whether it does.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithAtParCoupons(bool b = true) => this with { UseIndexedCoupons = !b };

    /// <summary>
    /// Sets the index selection for the schedule's irregular periods, which then fix on the selected indices; it needs
    /// indexed coupons, and neither caps, floors nor in-arrears fixing.
    /// </summary>
    /// <param name="selection">The selection; an empty one leaves the leg alone.</param>
    /// <returns>The modified builder.</returns>
    public IborLeg WithStubIndexSelection(StubIndexSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return this with { Selection = selection };
    }

    /// <summary>Builds the leg.</summary>
    /// <param name="context">The evaluation context, whose at-par switch the default pricer and the stub check read
    /// when the leg has no explicit choice.</param>
    /// <returns>The leg, one coupon per schedule period.</returns>
    /// <exception cref="NanoQLException">No notional was given; a setting has more values than the schedule has
    /// periods; the leg is both in arrears and zero; a period has a zero gearing and no payment day counter is set;
    /// or a stub selection is given with caps, floors, in-arrears fixing, at-par coupons or a schedule without
    /// regularity information.</exception>
    public ImmutableArray<CashFlow> ToLeg(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ImmutableArray<CashFlow> leg = CashFlowVectorsFunctions.Build<IborIndex>(
            (paymentDate, nominal, start, end, fixingDays, index, gearing, spread, refStart, refEnd, dc, inArrears,
                exCouponDate, fixingConvention) =>
                new IborCoupon(
                    paymentDate,
                    nominal,
                    start,
                    end,
                    fixingDays,
                    index,
                    gearing,
                    spread,
                    refStart,
                    refEnd,
                    dc,
                    inArrears,
                    exCouponDate,
                    fixingConvention),
            (paymentDate, nominal, start, end, fixingDays, index, gearing, spread, cap, floor, refStart, refEnd, dc,
                inArrears, exCouponDate, fixingConvention) =>
                new CappedFlooredIborCoupon(
                    paymentDate,
                    nominal,
                    start,
                    end,
                    fixingDays,
                    index,
                    gearing,
                    spread,
                    cap,
                    floor,
                    refStart,
                    refEnd,
                    dc,
                    inArrears,
                    exCouponDate,
                    fixingConvention),
            Schedule,
            Notionals,
            Index,
            PaymentDayCounter,
            PaymentAdjustment,
            FixingDays,
            Gearings,
            Spreads,
            Caps,
            Floors,
            IsInArrears,
            ZeroPayments,
            PaymentLag,
            PaymentCalendar,
            ExCouponPeriod,
            ExCouponCalendar,
            ExCouponAdjustment,
            ExCouponEndOfMonth,
            FixingConvention,
            stored: true);

        if (Selection is { Empty: false } selection)
        {
            if (!(Caps.IsEmpty && Floors.IsEmpty))
            {
                throw new NanoQLException("stub index conventions are not supported for capped/floored Ibor legs");
            }

            if (IsInArrears)
            {
                throw new NanoQLException("stub index conventions are not supported for in-arrears Ibor legs");
            }

            if (!(UseIndexedCoupons ?? !context.UseAtParCoupons))
            {
                throw new NanoQLException("stub index conventions require indexed coupons");
            }

            if (!Schedule.HasIsRegular)
            {
                throw new NanoQLException("schedule does not provide regularity information for stub index selection");
            }

            ImmutableArray<CashFlow>.Builder stubbed = leg.ToBuilder();
            for (int i = 0; i < stubbed.Count; ++i)
            {
                if (Schedule.IsRegular(i + 1))
                {
                    continue;
                }

                // periods with null gearing are fixed-rate coupons; leave them alone
                if (stubbed[i] is not IborCoupon coupon)
                {
                    continue;
                }

                stubbed[i] = new StubIborCoupon(
                    coupon.Date,
                    coupon.ContractNominal,
                    coupon.AccrualStartDate,
                    coupon.AccrualEndDate,
                    coupon.FixingDays,
                    selection,
                    coupon.Gearing,
                    coupon.Spread,
                    coupon.ReferencePeriodStart,
                    coupon.ReferencePeriodEnd,
                    coupon.DayCounter,
                    coupon.IsInArrears,
                    coupon.ExCouponDate,
                    coupon.FixingConvention);
            }

            leg = stubbed.MoveToImmutable();
        }

        if (Caps.IsEmpty && Floors.IsEmpty && !IsInArrears)
        {
            IborCouponPricer pricer = UseIndexedCoupons is bool useIndexedCoupon
                ? new BlackIborCouponPricer(null, BlackIborCouponPricerTimingAdjustment.Black76, 1.0, useIndexedCoupon)
                : new BlackIborCouponPricer(context, null, BlackIborCouponPricerTimingAdjustment.Black76, 1.0);
            leg = CouponPricerFunctions.SetCouponPricer(leg, pricer);
        }

        return leg;
    }
}
