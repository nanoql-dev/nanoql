// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/overnightindexedcoupon.hpp ql/cashflows/overnightindexedcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Coupons;

/// <summary>A helper class building a sequence of overnight-indexed coupons.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToLeg"/> builds the leg, in place of QuantLib's conversion operator; it reads no evaluation date,
/// since the coupons' value dates are always the full daily list. A builder has no value equality: its vectors compare
/// by reference.
/// </para>
/// <para>
/// A period whose gearing is zero is a <see cref="FixedRateCoupon"/> at the spread, capped and floored; a period with a
/// cap or a floor is a <see cref="CappedFlooredOvernightIndexedCoupon"/>. A coupon pricer given with
/// <see cref="WithCouponPricer"/> is set on each coupon and capped coupon; otherwise the coupons keep their default
/// pricers, and the capped coupons have none.
/// </para>
/// </remarks>
public sealed record OvernightLeg
{
    /// <summary>Initializes a new instance of the <see cref="OvernightLeg"/> record.</summary>
    /// <param name="schedule">The schedule; its calendar is the default payment calendar.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public OvernightLeg(Schedule schedule, OvernightIndex overnightIndex)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (overnightIndex is null)
        {
            throw new ArgumentNullException(nameof(overnightIndex), "no index provided");
        }

        Schedule = schedule;
        OvernightIndex = overnightIndex;
        PaymentCalendar = schedule.Calendar;
    }

    private Schedule Schedule { get; init; }

    private OvernightIndex OvernightIndex { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; }

    private Calendar? PaymentCalendar { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

    private int PaymentLag { get; init; }

    private ImmutableArray<double> Gearings { get; init; } = [];

    private ImmutableArray<double> Spreads { get; init; } = [];

    private bool TelescopicValueDates { get; init; }

    private RateAveragingType AveragingMethod { get; init; } = RateAveragingType.Compound;

    private int? LookbackDays { get; init; }

    private int LockoutDays { get; init; }

    private bool ApplyObservationShift { get; init; }

    private bool CompoundSpreadDaily { get; init; }

    private int? RoundingPrecision { get; init; }

    private ImmutableArray<double?> Caps { get; init; } = [];

    private ImmutableArray<double?> Floors { get; init; } = [];

    private bool NakedOption { get; init; }

    private bool DailyCapFloor { get; init; }

    private bool IsInArrears { get; init; } = true;

    private Period? LastRecentPeriod { get; init; }

    private Calendar? LastRecentPeriodCalendar { get; init; }

    private ImmutableArray<Date> PaymentDates { get; init; } = [];

    private OvernightIndexedCouponPricer? CouponPricer { get; init; }

    /// <summary>Sets one nominal for every coupon.</summary>
    /// <param name="notional">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' nominals; the last one repeats past the end.</summary>
    /// <param name="notionals">The nominals.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the coupons' accrual day counter; the index's when none is set.</summary>
    /// <param name="dc">The day counter; the index's when <see langword="null"/>, QuantLib's empty day counter. A
    /// period with a zero gearing then refuses the conversion, its fixed-rate coupon needing one, after every refusal
    /// QuantLib raises while it builds the leg.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithPaymentDayCounter(DayCounter? dc) => this with { PaymentDayCounter = dc };

    /// <summary>Sets the convention adjusting the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets the payment calendar; the schedule's when none is set.</summary>
    /// <param name="cal">The calendar; the schedule's when <see langword="null"/>, QuantLib's empty calendar, and
    /// <see cref="WeekendsOnly"/> when the schedule's is empty too.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithPaymentCalendar(Calendar? cal) => this with { PaymentCalendar = cal };

    /// <summary>Sets the payment lag, in business days after each accrual end.</summary>
    /// <param name="lag">The lag.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithPaymentLag(int lag) => this with { PaymentLag = lag };

    /// <summary>Sets one gearing for every coupon.</summary>
    /// <param name="gearing">The gearing.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings; the last one repeats past the end, and 1 applies when none is set.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one spread for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithSpreads(double spread) => this with { Spreads = [spread] };

    /// <summary>Sets the coupons' spreads; the last one repeats past the end, and 0 applies when none is set.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { Spreads = [.. spreads] };
    }

    /// <summary>
    /// Sets QuantLib's telescopic value dates, which on averaged coupons select the averaging pricer's approximation;
    /// the value dates are the full daily list either way.
    /// </summary>
    /// <param name="telescopicValueDates">The flag.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithTelescopicValueDates(bool telescopicValueDates) =>
        this with { TelescopicValueDates = telescopicValueDates };

    /// <summary>Sets the averaging method.</summary>
    /// <param name="averagingMethod">The method.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithAveragingMethod(RateAveragingType averagingMethod)
    {
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        return this with { AveragingMethod = averagingMethod };
    }

    /// <summary>Sets the lookback days.</summary>
    /// <param name="lookbackDays">
    /// The lookback, or <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c> and its default, for none: the
    /// coupons then fix by the index's fixing days.
    /// </param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The lookback is negative.</exception>
    public OvernightLeg WithLookbackDays(int? lookbackDays)
    {
        if (lookbackDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(lookbackDays));
        }

        return this with { LookbackDays = lookbackDays };
    }

    /// <summary>Sets the lockout days.</summary>
    /// <param name="lockoutDays">The lockout.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The lockout is negative.</exception>
    public OvernightLeg WithLockoutDays(int lockoutDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lockoutDays);
        return this with { LockoutDays = lockoutDays };
    }

    /// <summary>Sets whether the observation shift applies.</summary>
    /// <param name="applyObservationShift">The flag.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithObservationShift(bool applyObservationShift = true) =>
        this with { ApplyObservationShift = applyObservationShift };

    /// <summary>Sets whether the spread compounds daily.</summary>
    /// <param name="compoundSpreadDaily">The flag.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg CompoundingSpreadDaily(bool compoundSpreadDaily = true) =>
        this with { CompoundSpreadDaily = compoundSpreadDaily };

    /// <summary>Sets the decimal places the coupons' rates are rounded to before their amounts are computed.</summary>
    /// <param name="roundingPrecision">The precision.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside [0, 16].</exception>
    public OvernightLeg WithRoundingPrecision(int roundingPrecision)
    {
        Rounding.CheckPrecision(roundingPrecision, nameof(roundingPrecision));
        return this with { RoundingPrecision = roundingPrecision };
    }

    /// <summary>Sets one cap for every coupon.</summary>
    /// <param name="cap">
    /// The cap, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for none: the coupons are then
    /// uncapped.
    /// </param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithCaps(double? cap) => this with { Caps = [cap] };

    /// <summary>Sets the coupons' caps, <see langword="null"/> for none; the last one repeats past the end.</summary>
    /// <param name="caps">The caps.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithCaps(IEnumerable<double?> caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        return this with { Caps = [.. caps] };
    }

    /// <summary>Sets one floor for every coupon.</summary>
    /// <param name="floor">
    /// The floor, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for none: the coupons are then
    /// unfloored.
    /// </param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithFloors(double? floor) => this with { Floors = [floor] };

    /// <summary>Sets the coupons' floors, <see langword="null"/> for none; the last one repeats past the end.</summary>
    /// <param name="floors">The floors.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithFloors(IEnumerable<double?> floors)
    {
        ArgumentNullException.ThrowIfNull(floors);
        return this with { Floors = [.. floors] };
    }

    /// <summary>Sets whether the capped coupons pay the options alone.</summary>
    /// <param name="nakedOption">The flag.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithNakedOption(bool nakedOption) => this with { NakedOption = nakedOption };

    /// <summary>Sets whether the caps and floors apply to the daily rates.</summary>
    /// <param name="dailyCapFloor">The flag.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithDailyCapFloor(bool dailyCapFloor = true) => this with { DailyCapFloor = dailyCapFloor };

    /// <summary>
    /// Sets whether the rates are computed over each coupon's own period (in arrears, the default) or over the period
    /// before (in advance).
    /// </summary>
    /// <param name="inArrears">The flag.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg InArrears(bool inArrears) => this with { IsInArrears = inArrears };

    /// <summary>
    /// Sets the period before each rate computation's end where the computation starts, or <see langword="null"/> to
    /// compute over the whole period.
    /// </summary>
    /// <param name="lastRecentPeriod">The period.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithLastRecentPeriod(Period? lastRecentPeriod) =>
        this with { LastRecentPeriod = lastRecentPeriod };

    /// <summary>Sets the calendar of the last recent period; the schedule's when none is set.</summary>
    /// <param name="lastRecentPeriodCalendar">The calendar; the schedule's when <see langword="null"/>, QuantLib's
    /// empty calendar.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithLastRecentPeriodCalendar(Calendar? lastRecentPeriodCalendar) =>
        this with { LastRecentPeriodCalendar = lastRecentPeriodCalendar };

    /// <summary>Sets explicit payment dates, one per period, in place of the lagged and adjusted accrual ends.</summary>
    /// <param name="paymentDates">The dates.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithPaymentDates(IEnumerable<Date> paymentDates)
    {
        ArgumentNullException.ThrowIfNull(paymentDates);
        ImmutableArray<Date> dates = [.. paymentDates];
        foreach (Date d in dates)
        {
            Date.ThrowIfDefault(d, nameof(paymentDates));
        }

        return this with { PaymentDates = dates };
    }

    /// <summary>
    /// Sets the pricer of every coupon and capped coupon: a compounding pricer for compounded coupons, an averaging
    /// pricer for averaged ones; <see langword="null"/> keeps the coupons' default pricers.
    /// </summary>
    /// <param name="couponPricer">The pricer.</param>
    /// <returns>The modified builder.</returns>
    public OvernightLeg WithCouponPricer(OvernightIndexedCouponPricer? couponPricer) =>
        this with { CouponPricer = couponPricer };

    /// <summary>The leg: QuantLib's conversion to <c>Leg</c>.</summary>
    /// <returns>The leg; empty for a schedule of one date.</returns>
    /// <exception cref="NanoQLException">No nominal is given, the coupon pricer doesn't suit the averaging method, the
    /// schedule is empty, the explicit payment dates don't match the periods, or, after every refusal QuantLib raises
    /// while it builds the leg, a period of zero gearing has no payment day counter.</exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        if (Notionals.IsEmpty)
        {
            throw new NanoQLException("no notional given");
        }

        if (CouponPricer is not null)
        {
            if (AveragingMethod == RateAveragingType.Compound)
            {
                if (CouponPricer is not CompoundingOvernightIndexedCouponPricer)
                {
                    throw new NanoQLException(
                        "Wrong coupon pricer provided, provide a CompoundingOvernightIndexedCouponPricer");
                }
            }
            else if (CouponPricer is not ArithmeticAveragedOvernightIndexedCouponPricer)
            {
                throw new NanoQLException(
                    "Wrong coupon pricer provided, provide a ArithmeticAveragedOvernightIndexedCouponPricer");
            }
        }

        var cashflows = ImmutableArray.CreateBuilder<CashFlow>();
        NanoQLException? zeroGearingRefusal = null;

        // the following is not always correct
        Calendar? calendar = Schedule.Calendar;
        Calendar? paymentCalendar = PaymentCalendar;
        calendar ??= paymentCalendar;
        calendar ??= new WeekendsOnly();
        paymentCalendar ??= calendar;

        if (Schedule.Empty)
        {
            // QuantLib counts schedule_.size()-1 periods, a Size that wraps to its largest value, and its first read,
            // schedule_.date(0), throws std::out_of_range; given payment dates, its own count check refuses first,
            // printing the wrapped count (overnightindexedcoupon.cpp:596-601), which this text replaces.
            throw new NanoQLException("empty schedule");
        }

        int n = Schedule.Size - 1;

        // Initial consistency checks
        if (!PaymentDates.IsEmpty && PaymentDates.Length != n)
        {
            throw new NanoQLException(
                $"Expected the number of explicit payment dates ({PaymentDates.Length}) to equal the number of "
                + $"calculation periods ({n})");
        }

        for (int i = 0; i < n; ++i)
        {
            Date start = Schedule.Date(i), refStart = start;
            Date end = Schedule.Date(i + 1), refEnd = end;

            // If explicit payment dates provided, use them.
            Date paymentDate = !PaymentDates.IsEmpty
                ? PaymentDates[i]
                : paymentCalendar.Advance(end, PaymentLag, TimeUnit.Days, PaymentAdjustment);

            // determine refStart and refEnd
            if (i == 0 && Schedule.HasIsRegular && !Schedule.IsRegular(i + 1))
            {
                refStart = calendar.Adjust(end - Schedule.Tenor(), PaymentAdjustment);
            }

            if (i == n - 1 && Schedule.HasIsRegular && !Schedule.IsRegular(i + 1))
            {
                refEnd = calendar.Adjust(start + Schedule.Tenor(), PaymentAdjustment);
            }

            // Determine the rate computation start and end date as
            // - the coupon start and end date, if in arrears, and
            // - the previous coupon start and end date, if in advance.
            // In addition, adjust the start date, if a last recent period is given.
            Date rateComputationStartDate, rateComputationEndDate;
            if (IsInArrears)
            {
                // in arrears fixing (i.e. the "classic" case)
                rateComputationStartDate = start;
                rateComputationEndDate = end;
            }
            else if (i > 0)
            {
                // handle in advance fixing: if there is a previous period, we take that
                rateComputationStartDate = Schedule.Date(i - 1);
                rateComputationEndDate = Schedule.Date(i);
            }
            else
            {
                // otherwise we construct the previous period
                rateComputationEndDate = start;
                rateComputationStartDate = Schedule.HasTenor && Schedule.Tenor() != new Period(0, TimeUnit.Days)
                    ? calendar.Adjust(start - Schedule.Tenor(), BusinessDayConvention.Preceding)
                    : calendar.Adjust(start - (end - start), BusinessDayConvention.Preceding);
            }

            if (LastRecentPeriod is Period lastRecentPeriod)
            {
                rateComputationStartDate =
                    (LastRecentPeriodCalendar ?? calendar).Advance(rateComputationEndDate, -lastRecentPeriod);
            }

            // build coupon
            double gearing = VectorsFunctions.Get(Gearings, i, 1.0);
            double nominal = VectorsFunctions.Get(Notionals, i, 1.0);
            if (ComparisonFunctions.CloseEnough(gearing, 0.0))
            {
                // fixed coupon. QuantLib builds it with an empty day counter when none is set, and its accrual
                // members throw this text when they read it; a coupon here can't hold a null day counter, so the leg
                // is refused once it is built, and the coupon takes a placeholder day counter meanwhile.
                if (PaymentDayCounter is null)
                {
                    zeroGearingRefusal ??= new NanoQLException("no day counter implementation provided");
                }

                DayCounter dayCounter = PaymentDayCounter ?? new Actual365Fixed();
                cashflows.Add(new FixedRateCoupon(
                    paymentDate, nominal, CashFlowVectorsFunctions.EffectiveFixedRate(Spreads, Caps, Floors, i), dayCounter, start, end, refStart, refEnd));
            }
            else
            {
                // floating coupon
                var cpn = new OvernightIndexedCoupon(
                    paymentDate,
                    nominal,
                    start,
                    end,
                    OvernightIndex,
                    gearing,
                    VectorsFunctions.Get(Spreads, i, 0.0),
                    refStart,
                    refEnd,
                    PaymentDayCounter,
                    TelescopicValueDates,
                    AveragingMethod,
                    LookbackDays,
                    LockoutDays,
                    ApplyObservationShift,
                    CompoundSpreadDaily,
                    rateComputationStartDate,
                    rateComputationEndDate,
                    null,
                    RoundingPrecision);
                if (CouponPricer is not null)
                {
                    cpn = cpn.SetPricer(CouponPricer);
                }

                double? cap = VectorsFunctions.Get(Caps, i, null);
                double? floor = VectorsFunctions.Get(Floors, i, null);
                if (cap is null && floor is null)
                {
                    cashflows.Add(cpn);
                }
                else
                {
                    var cfCpn = new CappedFlooredOvernightIndexedCoupon(cpn, cap, floor, NakedOption, DailyCapFloor);
                    if (CouponPricer is not null)
                    {
                        cfCpn = cfCpn.SetPricer(CouponPricer);
                    }

                    cashflows.Add(cfCpn);
                }
            }
        }

        return zeroGearingRefusal is null ? cashflows.ToImmutable() : throw zeroGearingRefusal;
    }
}
