// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/yoyinflationcoupon.hpp ql/cashflows/yoyinflationcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Coupons;

/// <summary>
/// A helper class building a sequence of year-on-year inflation coupons: QuantLib's <c>yoyInflationLeg</c>.
/// </summary>
/// <remarks>
/// An immutable builder: each method returns a modified copy. <see cref="ToLeg"/> builds the leg, in place of
/// QuantLib's conversion operator, and attaches QuantLib's default <see cref="YoYInflationCouponPricer"/>, as QuantLib
/// does when there are no caps or floors; a leg with caps or floors needs a pricer set on it, as QuantLib's comment
/// says. A period with a zero gearing is a fixed-rate coupon paying its spread, capped and floored; a period with a
/// cap or a floor a <see cref="CappedFlooredYoYInflationCoupon"/>. A builder has no value equality.
/// </remarks>
public sealed record YoYInflationLeg
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationLeg"/> class.</summary>
    /// <param name="schedule">The schedule of accrual dates.</param>
    /// <param name="paymentCalendar">The payment calendar, or <see langword="null"/>, QuantLib's empty
    /// calendar.</param>
    /// <param name="index">The year-on-year inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or <paramref name="index"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interpolation"/> isn't a member of its
    /// type.</exception>
    public YoYInflationLeg(
        Schedule schedule,
        Calendar? paymentCalendar,
        YoYInflationIndex index,
        Period observationLag,
        CPIInterpolationType interpolation)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(index);
        EnumArguments.Check(interpolation, nameof(interpolation));
        Schedule = schedule;
        PaymentCalendar = paymentCalendar;
        Index = index;
        ObservationLag = observationLag;
        Interpolation = interpolation;
    }

    private Schedule Schedule { get; init; }

    private Calendar? PaymentCalendar { get; init; }

    private YoYInflationIndex Index { get; init; }

    private Period ObservationLag { get; init; }

    private CPIInterpolationType Interpolation { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.ModifiedFollowing;

    private ImmutableArray<int> FixingDays { get; init; } = [];

    private ImmutableArray<double> Gearings { get; init; } = [];

    private ImmutableArray<double> Spreads { get; init; } = [];

    private ImmutableArray<double?> Caps { get; init; } = [];

    private ImmutableArray<double?> Floors { get; init; } = [];

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public YoYInflationLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals, the last repeated for the coupons after it.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notionals"/> is null.</exception>
    public YoYInflationLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the coupons' day counter.</summary>
    /// <param name="dayCounter">The day counter, or <see langword="null"/>, QuantLib's empty one, which the leg
    /// refuses.</param>
    /// <returns>The modified builder.</returns>
    public YoYInflationLeg WithPaymentDayCounter(DayCounter? dayCounter) => this with { PaymentDayCounter = dayCounter };

    /// <summary>Sets the convention adjusting the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="convention"/> isn't a member of its
    /// type.</exception>
    public YoYInflationLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets one number of fixing days for every coupon.</summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public YoYInflationLeg WithFixingDays(int fixingDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        return this with { FixingDays = [fixingDays] };
    }

    /// <summary>Sets the coupons' fixing days; the last one repeats for any further coupons.</summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fixingDays"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public YoYInflationLeg WithFixingDays(IEnumerable<int> fixingDays)
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
    public YoYInflationLeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings; the last one repeats for any further coupons.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gearings"/> is null.</exception>
    public YoYInflationLeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one spread for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public YoYInflationLeg WithSpreads(double spread) => this with { Spreads = [spread] };

    /// <summary>Sets the coupons' spreads; the last one repeats for any further coupons.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spreads"/> is null.</exception>
    public YoYInflationLeg WithSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { Spreads = [.. spreads] };
    }

    /// <summary>Sets one cap for every coupon.</summary>
    /// <param name="cap">The cap, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public YoYInflationLeg WithCaps(double? cap) => this with { Caps = [cap] };

    /// <summary>Sets the coupons' caps; the last one repeats for any further coupons.</summary>
    /// <param name="caps">The caps, each <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="caps"/> is null.</exception>
    public YoYInflationLeg WithCaps(IEnumerable<double?> caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        return this with { Caps = [.. caps] };
    }

    /// <summary>Sets one floor for every coupon.</summary>
    /// <param name="floor">The floor, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public YoYInflationLeg WithFloors(double? floor) => this with { Floors = [floor] };

    /// <summary>Sets the coupons' floors; the last one repeats for any further coupons.</summary>
    /// <param name="floors">The floors, each <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="floors"/> is null.</exception>
    public YoYInflationLeg WithFloors(IEnumerable<double?> floors)
    {
        ArgumentNullException.ThrowIfNull(floors);
        return this with { Floors = [.. floors] };
    }

    /// <summary>Builds the leg, with QuantLib's default pricer attached to each year-on-year coupon when there are no
    /// caps or floors.</summary>
    /// <returns>The leg, one coupon per schedule period.</returns>
    /// <exception cref="NanoQLException">No payment day counter or notional was given; the schedule is empty; more
    /// notionals, gearings, spreads, caps or floors were given than there are periods; or a date is computed on
    /// QuantLib's empty calendar where it can't give one.</exception>
    /// <exception cref="ArgumentException">A period's cap is below its floor, with the coupon's text.</exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        Schedule schedule = Schedule;
        DayCounter paymentDayCounter = PaymentDayCounter ?? throw new NanoQLException("no payment daycounter given");
        if (Notionals.IsEmpty)
        {
            throw new NanoQLException("no notional given");
        }

        if (schedule.Empty)
        {
            // QuantLib's n is schedule_.size()-1, which wraps to the largest Size: its size checks pass, and
            // leg.reserve(n) throws std::length_error.
            throw new NanoQLException("empty schedule");
        }

        int n = schedule.Size - 1;

        CheckSize(Notionals.Length, n, "nominals");
        CheckSize(Gearings.Length, n, "gearings");
        CheckSize(Spreads.Length, n, "spreads");
        CheckSize(Caps.Length, n, "caps");
        CheckSize(Floors.Length, n, "floors");

        var leg = ImmutableArray.CreateBuilder<CashFlow>(n);

        // QuantLib's empty calendars, null here, are used unchecked, as QuantLib uses them
        Calendar calendar = PaymentCalendar ?? EmptyCalendar.Instance;
        Calendar scheduleCalendar = schedule.Calendar ?? EmptyCalendar.Instance;
        for (int i = 0; i < n; ++i)
        {
            Date refStart, start, refEnd, end;
            refStart = start = schedule.Date(i);
            refEnd = end = schedule.Date(i + 1);
            Date paymentDate = calendar.Adjust(end, PaymentAdjustment);
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

            double notional = VectorsFunctions.Get(Notionals, i, 1.0);
            double gearing = VectorsFunctions.Get(Gearings, i, 1.0);
            if (gearing == 0.0)
            {
                // fixed coupon
                double rate = CashFlowVectorsFunctions.EffectiveFixedRate(Spreads, Caps, Floors, i);
                leg.Add(new FixedRateCoupon(paymentDate, notional, rate, paymentDayCounter, start, end, refStart, refEnd));
            }
            else if (CashFlowVectorsFunctions.NoOption(Caps, Floors, i))
            {
                // yoy inflation coupon, just swaplet
                leg.Add(new YoYInflationCoupon(
                    paymentDate,
                    notional,
                    start,
                    end,
                    VectorsFunctions.Get(FixingDays, i, 0),
                    Index,
                    ObservationLag,
                    Interpolation,
                    paymentDayCounter,
                    gearing,
                    VectorsFunctions.Get(Spreads, i, 0.0),
                    refStart,
                    refEnd));
            }
            else
            {
                // cap/floorlet
                leg.Add(new CappedFlooredYoYInflationCoupon(
                    paymentDate,
                    notional,
                    start,
                    end,
                    VectorsFunctions.Get(FixingDays, i, 0),
                    Index,
                    ObservationLag,
                    Interpolation,
                    paymentDayCounter,
                    gearing,
                    VectorsFunctions.Get(Spreads, i, 0.0),
                    VectorsFunctions.Get(Caps, i, null),
                    VectorsFunctions.Get(Floors, i, null),
                    refStart,
                    refEnd));
            }
        }

        // Without caps or floors, this is enough; otherwise, a more
        // specific pricer will need to be set in client code.
        ImmutableArray<CashFlow> built = leg.MoveToImmutable();
        return Caps.IsEmpty && Floors.IsEmpty
            ? InflationCouponPricerFunctions.SetCouponPricer(built, new YoYInflationCouponPricer())
            : built;
    }

    private static void CheckSize(int given, int n, string what)
    {
        if (!(given <= n))
        {
            throw new NanoQLException($"too many {what} ({given}), only {n} required");
        }
    }
}
