// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cmscoupon.hpp ql/cashflows/cmscoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A builder of a sequence of CMS coupons, capped, floored or not.</summary>
/// <remarks>
/// An immutable builder, as <see cref="IborLeg"/>: each method returns a modified copy, and <see cref="ToLeg"/> builds
/// the leg in place of QuantLib's conversion operator. The coupons are built without pricers, as QuantLib's are; a CMS
/// coupon pricer, which waits for batch 3 (B3.3), is set with
/// <see cref="CouponPricerFunctions.SetCouponPricer(ImmutableArray{CashFlow}, FloatingRateCouponPricer)"/>. A builder
/// has no value equality: its vectors compare by reference.
/// </remarks>
public sealed record CmsLeg
{
    /// <summary>Initializes a new instance of the <see cref="CmsLeg"/> class.</summary>
    /// <param name="schedule">The schedule of accrual dates.</param>
    /// <param name="swapIndex">The swap index.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CmsLeg(Schedule schedule, SwapIndex swapIndex)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (swapIndex is null)
        {
            throw new ArgumentNullException(nameof(swapIndex), "no index provided");
        }

        Schedule = schedule;
        SwapIndex = swapIndex;
    }

    private Schedule Schedule { get; init; }

    private SwapIndex SwapIndex { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

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

    private BusinessDayConvention ExCouponAdjustment { get; init; } = BusinessDayConvention.Following;

    private bool ExCouponEndOfMonth { get; init; }

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals; the last one repeats for any further coupons.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the accrual day counter; the index's by default.</summary>
    /// <param name="dayCounter">The day counter; the index's when <see langword="null"/>, QuantLib's empty day
    /// counter. A period with a zero gearing then refuses the conversion, its fixed-rate coupon needing one, after
    /// every refusal QuantLib raises while it builds the leg.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithPaymentDayCounter(DayCounter? dayCounter) => this with { PaymentDayCounter = dayCounter };

    /// <summary>Sets the business-day convention of the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public CmsLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets one count of fixing days for every coupon.</summary>
    /// <param name="fixingDays">The fixing days; the index's when <see langword="null"/>, QuantLib's
    /// <c>Null&lt;Natural&gt;()</c>.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public CmsLeg WithFixingDays(int? fixingDays) => WithFixingDays([fixingDays]);

    /// <summary>Sets the coupons' fixing days; the last count repeats for any further coupons.</summary>
    /// <param name="fixingDays">The fixing days, null elements taking the index's.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An element is negative.</exception>
    public CmsLeg WithFixingDays(IEnumerable<int?> fixingDays)
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
    public CmsLeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings; the last one repeats for any further coupons.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one spread for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithSpreads(double spread) => this with { Spreads = [spread] };

    /// <summary>Sets the coupons' spreads; the last one repeats for any further coupons.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { Spreads = [.. spreads] };
    }

    /// <summary>Sets one cap for every coupon's rate.</summary>
    /// <param name="cap">
    /// The cap, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for a cap list holding no cap: the
    /// coupons are then uncapped.
    /// </param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithCaps(double? cap) => this with { Caps = [cap] };

    /// <summary>Sets the caps on the coupons' rates; the last one repeats for any further coupons.</summary>
    /// <param name="caps">The caps, null elements for none.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithCaps(IEnumerable<double?> caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        return this with { Caps = [.. caps] };
    }

    /// <summary>Sets one floor for every coupon's rate.</summary>
    /// <param name="floor">
    /// The floor, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for a floor list holding no floor:
    /// the coupons are then unfloored.
    /// </param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithFloors(double? floor) => this with { Floors = [floor] };

    /// <summary>Sets the floors on the coupons' rates; the last one repeats for any further coupons.</summary>
    /// <param name="floors">The floors, null elements for none.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithFloors(IEnumerable<double?> floors)
    {
        ArgumentNullException.ThrowIfNull(floors);
        return this with { Floors = [.. floors] };
    }

    /// <summary>Sets whether the coupons fix at the end of their periods.</summary>
    /// <param name="flag">Whether they do.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg InArrears(bool flag = true) => this with { IsInArrears = flag };

    /// <summary>Sets whether every coupon is paid on the last payment date.</summary>
    /// <param name="flag">Whether it is.</param>
    /// <returns>The modified builder.</returns>
    public CmsLeg WithZeroPayments(bool flag = true) => this with { ZeroPayments = flag };

    /// <summary>Sets the ex-coupon period, counted back from each payment date.</summary>
    /// <param name="period">The period; a zero-length period means no ex-coupon dates.</param>
    /// <param name="cal">The calendar the period is counted on; the schedule's when <see langword="null"/>, QuantLib's
    /// empty calendar.</param>
    /// <param name="convention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="endOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public CmsLeg WithExCouponPeriod(
        Period period,
        Calendar? cal,
        BusinessDayConvention convention,
        bool endOfMonth)
    {
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
    public CmsLeg WithFixingConvention(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { FixingConvention = convention };
    }

    /// <summary>Builds the leg.</summary>
    /// <returns>The leg, one coupon per schedule period, without pricers.</returns>
    /// <exception cref="NanoQLException">No notional was given; a setting has more values than the schedule has
    /// periods; the leg is both in arrears and zero; the schedule is empty; or, after these, a period has a zero
    /// gearing and no payment day counter is set.</exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        ImmutableArray<CashFlow> leg = CashFlowVectorsFunctions.Build<SwapIndex>(
            (paymentDate, nominal, start, end, fixingDays, index, gearing, spread, refStart, refEnd, dc, inArrears,
                exCouponDate, fixingConvention) =>
                new CmsCoupon(
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
                new CappedFlooredCmsCoupon(
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
            SwapIndex,
            PaymentDayCounter,
            PaymentAdjustment,
            FixingDays,
            Gearings,
            Spreads,
            Caps,
            Floors,
            IsInArrears,
            ZeroPayments,
            0,
            null,
            ExCouponPeriod,
            ExCouponCalendar,
            ExCouponAdjustment,
            ExCouponEndOfMonth,
            FixingConvention,
            stored: true,
            out Exception? zeroGearingRefusal);
        return zeroGearingRefusal is null ? leg : throw zeroGearingRefusal;
    }
}
