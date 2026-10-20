// SPDX-FileCopyrightText: Copyright (C) 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2022 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cpicoupon.hpp ql/cashflows/cpicoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Coupons;

/// <summary>
/// A helper class building a sequence of CPI coupons, with the inflated notional at the end: QuantLib's <c>CPILeg</c>.
/// </summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy. <see cref="ToLeg"/> builds the leg, in place of
/// QuantLib's conversion operator, and attaches QuantLib's default <see cref="CPICouponPricer"/>. A period with a zero
/// fixed rate is a fixed-rate coupon paying its floor or cap; a period with a cap or a floor and a non-zero rate is
/// refused, as QuantLib refuses it. The notional flow comes last, even when the schedule has a single date. A builder
/// has no value equality.
/// </para>
/// <para>
/// With neither a base CPI nor a base date, every flow's base date is the schedule's start less the lag, as QuantLib
/// gives it, but every flow observes its base on the schedule's start with the leg's lag; with a base date, the
/// notional flow observes its base as the coupons do, on the base date plus the lag. So the coupons and the notional
/// flow share one base CPI, where QuantLib's can differ (<see cref="CPICoupon"/>'s remarks), a documented divergence.
/// </para>
/// </remarks>
public sealed record CPILeg
{
    /// <summary>Initializes a new instance of the <see cref="CPILeg"/> class.</summary>
    /// <param name="schedule">The schedule of accrual dates; its calendar is the default payment calendar.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="baseCPI">The base CPI, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for one
    /// observed on the base date.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or <paramref name="index"/> is
    /// null.</exception>
    public CPILeg(Schedule schedule, ZeroInflationIndex index, double? baseCPI, Period observationLag)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(index);
        Schedule = schedule;
        Index = index;
        BaseCPI = baseCPI;
        ObservationLag = observationLag;
        PaymentCalendar = schedule.Calendar;
    }

    private Schedule Schedule { get; init; }

    private ZeroInflationIndex Index { get; init; }

    private double? BaseCPI { get; init; }

    private Period ObservationLag { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private ImmutableArray<double> FixedRates { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; } = new Thirty360(Thirty360Convention.BondBasis);

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.ModifiedFollowing;

    private Calendar? PaymentCalendar { get; init; }

    private CPIInterpolationType ObservationInterpolation { get; init; } = CPIInterpolationType.Flat;

    private bool SubtractInflationNominal { get; init; } = true;

    private ImmutableArray<double?> Caps { get; init; } = [];

    private ImmutableArray<double?> Floors { get; init; } = [];

    private Period ExCouponPeriod { get; init; }

    private Calendar? ExCouponCalendar { get; init; }

    private BusinessDayConvention ExCouponAdjustment { get; init; } = BusinessDayConvention.Following;

    private bool ExCouponEndOfMonth { get; init; }

    private Date? BaseDate { get; init; }

    /// <summary>Sets one notional for every flow.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public CPILeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the flows' notionals; the last one repeats for any further flows, the notional flow among
    /// them.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notionals"/> is null.</exception>
    public CPILeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets one fixed rate for every coupon.</summary>
    /// <param name="fixedRate">The fixed rate; a zero rate makes fixed-rate coupons.</param>
    /// <returns>The modified builder.</returns>
    public CPILeg WithFixedRates(double fixedRate) => this with { FixedRates = [fixedRate] };

    /// <summary>Sets the coupons' fixed rates; the last one repeats for any further coupons.</summary>
    /// <param name="fixedRates">The fixed rates.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fixedRates"/> is null.</exception>
    public CPILeg WithFixedRates(IEnumerable<double> fixedRates)
    {
        ArgumentNullException.ThrowIfNull(fixedRates);
        return this with { FixedRates = [.. fixedRates] };
    }

    /// <summary>Sets the coupons' day counter; 30/360 (Bond Basis) by default.</summary>
    /// <param name="dayCounter">The day counter, or <see langword="null"/>, QuantLib's empty one, which a leg with no
    /// coupon, its schedule a single date, never reads.</param>
    /// <returns>The modified builder.</returns>
    public CPILeg WithPaymentDayCounter(DayCounter? dayCounter) => this with { PaymentDayCounter = dayCounter };

    /// <summary>Sets the convention adjusting the payment dates; ModifiedFollowing by default.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="convention"/> isn't a member of its
    /// type.</exception>
    public CPILeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets the calendar of the payment dates; the schedule's calendar by default.</summary>
    /// <param name="cal">The calendar, or <see langword="null"/> for QuantLib's empty calendar, used unchecked as
    /// QuantLib uses it.</param>
    /// <returns>The modified builder.</returns>
    public CPILeg WithPaymentCalendar(Calendar? cal) => this with { PaymentCalendar = cal };

    /// <summary>Sets how the index is observed; flat by default.</summary>
    /// <param name="interpolation">The interpolation.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interpolation"/> isn't a member of its
    /// type.</exception>
    public CPILeg WithObservationInterpolation(CPIInterpolationType interpolation)
    {
        EnumArguments.Check(interpolation, nameof(interpolation));
        return this with { ObservationInterpolation = interpolation };
    }

    /// <summary>Sets whether the notional flow pays only the growth, the inflated notional less the notional; it does
    /// by default.</summary>
    /// <param name="growthOnly">Whether it does.</param>
    /// <returns>The modified builder.</returns>
    public CPILeg WithSubtractInflationNominal(bool growthOnly) => this with { SubtractInflationNominal = growthOnly };

    /// <summary>Sets one cap for every coupon.</summary>
    /// <param name="cap">The cap, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for none.</param>
    /// <returns>The modified builder.</returns>
    public CPILeg WithCaps(double? cap) => this with { Caps = [cap] };

    /// <summary>Sets the coupons' caps; the last one repeats for any further coupons.</summary>
    /// <param name="caps">The caps, null elements for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="caps"/> is null.</exception>
    public CPILeg WithCaps(IEnumerable<double?> caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        return this with { Caps = [.. caps] };
    }

    /// <summary>Sets one floor for every coupon.</summary>
    /// <param name="floor">The floor, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for
    /// none.</param>
    /// <returns>The modified builder.</returns>
    public CPILeg WithFloors(double? floor) => this with { Floors = [floor] };

    /// <summary>Sets the coupons' floors; the last one repeats for any further coupons.</summary>
    /// <param name="floors">The floors, null elements for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="floors"/> is null.</exception>
    public CPILeg WithFloors(IEnumerable<double?> floors)
    {
        ArgumentNullException.ThrowIfNull(floors);
        return this with { Floors = [.. floors] };
    }

    /// <summary>Sets the ex-coupon period, counted back from each coupon's payment date.</summary>
    /// <param name="period">The period; a zero-length period means no ex-coupon dates.</param>
    /// <param name="cal">The calendar the period is counted on, or <see langword="null"/> for QuantLib's empty
    /// calendar, used unchecked as QuantLib uses it.</param>
    /// <param name="convention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="endOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="convention"/> isn't a member of its
    /// type.</exception>
    public CPILeg WithExCouponPeriod(
        Period period,
        Calendar? cal,
        BusinessDayConvention convention,
        bool endOfMonth = false)
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

    /// <summary>Sets the base date, on which the base CPI is observed when none is given.</summary>
    /// <param name="baseDate">The base date, or <see langword="null"/>, QuantLib's <c>Date()</c>, for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="baseDate"/> is <c>default(Date)</c>.</exception>
    public CPILeg WithBaseDate(Date? baseDate)
    {
        Date.ThrowIfDefault(baseDate, nameof(baseDate));
        return this with { BaseDate = baseDate };
    }

    /// <summary>Builds the leg, with QuantLib's default pricer attached to each CPI coupon.</summary>
    /// <returns>The leg: one coupon per schedule period, then the notional flow.</returns>
    /// <exception cref="NanoQLException">No notional was given; the schedule has periods, or is empty, and no fixed
    /// rate was given; the schedule is empty; a period with a non-zero rate has a cap or a floor; a coupon is built
    /// with no day counter, with the text QuantLib's coupon fails with on its first accrual ("no day counter
    /// implementation provided"); or a date is computed on QuantLib's empty calendar where it can't give
    /// one.</exception>
    /// <exception cref="ArgumentException">A flow refuses its base: neither a base CPI nor a base date, where the
    /// schedule has a single date; or a base CPI below 1e-16 in absolute value.</exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        if (Notionals.IsEmpty)
        {
            throw new NanoQLException("no notional given");
        }

        Schedule schedule = Schedule;
        if (schedule.Empty)
        {
            // QuantLib's n is schedule_.size()-1, which wraps to the largest Size: it asks for the fixed rates, then
            // reads schedule_.date(0), whose at() throws std::out_of_range.
            if (FixedRates.IsEmpty)
            {
                throw new NanoQLException("no fixedRates given");
            }

            throw new NanoQLException("empty schedule");
        }

        int n = schedule.Size - 1;
        var leg = ImmutableArray.CreateBuilder<CashFlow>(n + 1);

        // QuantLib's empty calendars, null here, are used unchecked, as QuantLib uses them
        Calendar paymentCalendar = PaymentCalendar ?? EmptyCalendar.Instance;
        Calendar scheduleCalendar = schedule.Calendar ?? EmptyCalendar.Instance;

        Date? baseDate = BaseDate;

        // The observation whose lagged fixing is every flow's base when no base CPI is given (SIR5 (xi)): with a base
        // date, the coupons' baseDate + lag at the lag; with neither, the schedule's start at the lag, which QuantLib's
        // start - lag base date loses at a month end.
        (Date Date, Period Lag)? baseObservation = baseDate is Date bd ? (bd + ObservationLag, ObservationLag) : null;

        // BaseDate and baseCPI are not given, use the first date as startDate and the baseFixingg
        // should be at startDate - observationLag
        if (n > 0)
        {
            if (FixedRates.IsEmpty)
            {
                throw new NanoQLException("no fixedRates given");
            }

            if (BaseDate is null && BaseCPI is null)
            {
                baseDate = schedule.Date(0) - ObservationLag;
                baseObservation = (schedule.Date(0), ObservationLag);
            }

            for (int i = 0; i < n; ++i)
            {
                Date refStart, start, refEnd, end;
                refStart = start = schedule.Date(i);
                refEnd = end = schedule.Date(i + 1);
                Date paymentDate = paymentCalendar.Adjust(end, PaymentAdjustment);

                Date? exCouponDate = null;
                if (ExCouponPeriod != default)
                {
                    exCouponDate = (ExCouponCalendar ?? EmptyCalendar.Instance)
                        .Advance(paymentDate, -ExCouponPeriod, ExCouponAdjustment, ExCouponEndOfMonth);
                }

                if (i == 0 && schedule.HasIsRegular && !schedule.IsRegular(i + 1))
                {
                    BusinessDayConvention bdc = schedule.BusinessDayConvention;
                    refStart = scheduleCalendar.Adjust(end - schedule.Tenor(), bdc);
                }

                if (i == n - 1 && schedule.HasIsRegular && !schedule.IsRegular(i + 1))
                {
                    BusinessDayConvention bdc = schedule.BusinessDayConvention;
                    refEnd = scheduleCalendar.Adjust(start + schedule.Tenor(), bdc);
                }

                if (VectorsFunctions.Get(FixedRates, i, 1.0) == 0.0)
                {
                    // fixed coupon
                    leg.Add(new FixedRateCoupon(
                        paymentDate,
                        VectorsFunctions.Get(Notionals, i, 0.0),
                        CashFlowVectorsFunctions.EffectiveFixedRate([], Caps, Floors, i),
                        CouponDayCounter(),
                        start,
                        end,
                        refStart,
                        refEnd,
                        exCouponDate));
                }
                else if (CashFlowVectorsFunctions.NoOption(Caps, Floors, i))
                {
                    // zero inflation coupon, just swaplet; all have same base for ratio
                    leg.Add(new CPICoupon(
                        BaseCPI,
                        baseDate,
                        baseObservation,
                        paymentDate,
                        VectorsFunctions.Get(Notionals, i, 0.0),
                        start,
                        end,
                        Index,
                        ObservationLag,
                        ObservationInterpolation,
                        CouponDayCounter(),
                        VectorsFunctions.Get(FixedRates, i, 0.0),
                        refStart,
                        refEnd,
                        exCouponDate));
                }
                else
                {
                    // cap/floorlet
                    throw new NanoQLException("caps/floors on CPI coupons not implemented.");
                }
            }
        }

        // in CPI legs you always have a notional flow of some sort
        Date notionalPaymentDate = paymentCalendar.Adjust(schedule.Date(n), PaymentAdjustment);
        leg.Add(new CPICashFlow(
            VectorsFunctions.Get(Notionals, n, 0.0),
            Index,
            baseDate,
            BaseCPI,
            baseObservation,
            schedule.Date(n),
            ObservationLag,
            ObservationInterpolation,
            notionalPaymentDate,
            SubtractInflationNominal));

        // no caps and floors here, so this is enough
        return InflationCouponPricerFunctions.SetCouponPricer(leg.MoveToImmutable(), new CPICouponPricer());
    }

    // QuantLib's empty day counter builds a coupon that fails on its first accrual; here the coupon isn't built.
    private DayCounter CouponDayCounter() =>
        PaymentDayCounter ?? throw new NanoQLException("no day counter implementation provided");
}
