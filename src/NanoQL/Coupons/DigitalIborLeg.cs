// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/digitaliborcoupon.hpp ql/cashflows/digitaliborcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A builder of a sequence of digital Ibor coupons: QuantLib's <c>DigitalIborLeg</c>.</summary>
/// <remarks>
/// An immutable builder, as <see cref="IborLeg"/>: each method returns a modified copy, and <see cref="ToLeg"/> builds
/// the leg through <see cref="CashFlowVectorsFunctions.FloatingDigitalLeg{TIndex, TCoupon}"/> in place of QuantLib's
/// conversion operator. The coupons are built without pricers, as QuantLib's are. The leg pays the underlying's rate
/// unless <see cref="WithNakedOption"/> is set: QuantLib's builder leaves its flag uninitialized until that call, an
/// undefined read (R8, the S-cms plan's CMR5 (viii); QuantLib's check, its legs built with
/// <c>withNakedOption(false)</c>). A builder has no value equality: its vectors compare by reference.
/// </remarks>
public sealed record DigitalIborLeg
{
    /// <summary>Initializes a new instance of the <see cref="DigitalIborLeg"/> class.</summary>
    /// <param name="schedule">The schedule of accrual dates.</param>
    /// <param name="index">The index.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public DigitalIborLeg(Schedule schedule, IborIndex index)
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

    private ImmutableArray<int?> FixingDays { get; init; } = [];

    private ImmutableArray<double> Gearings { get; init; } = [];

    private ImmutableArray<double> Spreads { get; init; } = [];

    private bool IsInArrears { get; init; }

    private ImmutableArray<double?> CallStrikes { get; init; } = [];

    private ImmutableArray<double?> CallPayoffs { get; init; } = [];

    private PositionType LongCallOption { get; init; } = PositionType.Long;

    private bool CallAtm { get; init; }

    private ImmutableArray<double?> PutStrikes { get; init; } = [];

    private ImmutableArray<double?> PutPayoffs { get; init; } = [];

    private PositionType LongPutOption { get; init; } = PositionType.Long;

    private bool PutAtm { get; init; }

    private DigitalReplication? Replication { get; init; }

    private bool NakedOption { get; init; }

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals; the last one repeats for any further coupons.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the accrual day counter; the index's by default.</summary>
    /// <param name="dayCounter">The day counter; the index's when <see langword="null"/>, QuantLib's empty day
    /// counter. A period with a zero gearing then refuses the conversion, its fixed-rate coupon needing one.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithPaymentDayCounter(DayCounter? dayCounter) => this with { PaymentDayCounter = dayCounter };

    /// <summary>Sets the business-day convention of the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public DigitalIborLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets one count of fixing days for every coupon.</summary>
    /// <param name="fixingDays">The fixing days; the index's when <see langword="null"/>.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public DigitalIborLeg WithFixingDays(int? fixingDays) => WithFixingDays([fixingDays]);

    /// <summary>Sets the coupons' fixing days; the last count repeats for any further coupons.</summary>
    /// <param name="fixingDays">The fixing days, null elements taking the index's.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An element is negative.</exception>
    public DigitalIborLeg WithFixingDays(IEnumerable<int?> fixingDays)
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
    public DigitalIborLeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings; the last one repeats for any further coupons.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one spread for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithSpreads(double spread) => this with { Spreads = [spread] };

    /// <summary>Sets the coupons' spreads; the last one repeats for any further coupons.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { Spreads = [.. spreads] };
    }

    /// <summary>Sets whether the coupons fix at the end of their periods.</summary>
    /// <param name="flag">Whether they do.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg InArrears(bool flag = true) => this with { IsInArrears = flag };

    /// <summary>Sets one call strike for every coupon.</summary>
    /// <param name="strike">The strike, or <see langword="null"/> for no call.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithCallStrikes(double? strike) => this with { CallStrikes = [strike] };

    /// <summary>Sets the coupons' call strikes; the last one repeats for any further coupons.</summary>
    /// <param name="strikes">The strikes, null elements for no call.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithCallStrikes(IEnumerable<double?> strikes)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        return this with { CallStrikes = [.. strikes] };
    }

    /// <summary>Sets the calls' position.</summary>
    /// <param name="type">The position.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithLongCallOption(PositionType type) => this with { LongCallOption = type };

    /// <summary>Sets whether a fixed rate at a call's strike pays the call.</summary>
    /// <param name="flag">Whether it does.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithCallAtm(bool flag = true) => this with { CallAtm = flag };

    /// <summary>Sets one call payoff for every coupon.</summary>
    /// <param name="payoff">The cash payoff, or <see langword="null"/> for asset-or-nothing calls.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithCallPayoffs(double? payoff) => this with { CallPayoffs = [payoff] };

    /// <summary>Sets the coupons' call payoffs; the last one repeats for any further coupons.</summary>
    /// <param name="payoffs">The cash payoffs, null elements for asset-or-nothing calls.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithCallPayoffs(IEnumerable<double?> payoffs)
    {
        ArgumentNullException.ThrowIfNull(payoffs);
        return this with { CallPayoffs = [.. payoffs] };
    }

    /// <summary>Sets one put strike for every coupon.</summary>
    /// <param name="strike">The strike, or <see langword="null"/> for no put.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithPutStrikes(double? strike) => this with { PutStrikes = [strike] };

    /// <summary>Sets the coupons' put strikes; the last one repeats for any further coupons.</summary>
    /// <param name="strikes">The strikes, null elements for no put.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithPutStrikes(IEnumerable<double?> strikes)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        return this with { PutStrikes = [.. strikes] };
    }

    /// <summary>Sets the puts' position.</summary>
    /// <param name="type">The position.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithLongPutOption(PositionType type) => this with { LongPutOption = type };

    /// <summary>Sets whether a fixed rate at a put's strike pays the put.</summary>
    /// <param name="flag">Whether it does.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithPutAtm(bool flag = true) => this with { PutAtm = flag };

    /// <summary>Sets one put payoff for every coupon.</summary>
    /// <param name="payoff">The cash payoff, or <see langword="null"/> for asset-or-nothing puts.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithPutPayoffs(double? payoff) => this with { PutPayoffs = [payoff] };

    /// <summary>Sets the coupons' put payoffs; the last one repeats for any further coupons.</summary>
    /// <param name="payoffs">The cash payoffs, null elements for asset-or-nothing puts.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithPutPayoffs(IEnumerable<double?> payoffs)
    {
        ArgumentNullException.ThrowIfNull(payoffs);
        return this with { PutPayoffs = [.. payoffs] };
    }

    /// <summary>Sets the replication.</summary>
    /// <param name="replication">The replication, or <see langword="null"/> for the digital coupon's default.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithReplication(DigitalReplication? replication) => this with { Replication = replication };

    /// <summary>Sets whether the coupons pay the options alone, without the underlying's rate.</summary>
    /// <param name="nakedOption">Whether they do.</param>
    /// <returns>The modified builder.</returns>
    public DigitalIborLeg WithNakedOption(bool nakedOption = true) => this with { NakedOption = nakedOption };

    /// <summary>Builds the leg.</summary>
    /// <returns>The leg, one coupon per schedule period, without pricers.</returns>
    /// <exception cref="NanoQLException">No notional was given; a setting has more values than the schedule has
    /// periods; the schedule is empty; or, after these, a period has a zero gearing and no payment day counter is
    /// set.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A position set with its strikes isn't a member of its
    /// type.</exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        ImmutableArray<CashFlow> leg = CashFlowVectorsFunctions.BuildDigital<IborIndex, IborCoupon>(
            (paymentDate, nominal, start, end, fixingDays, index, gearing, spread, refStart, refEnd, dc, inArrears) =>
                new IborCoupon(paymentDate, nominal, start, end, fixingDays, index, gearing, spread, refStart, refEnd, dc, inArrears),
            (underlying, callStrike, callPosition, callAtm, callPayoff, putStrike, putPosition, putAtm, putPayoff, replication,
                naked) =>
                new DigitalIborCoupon(
                    underlying,
                    callStrike,
                    callPosition,
                    callAtm,
                    callPayoff,
                    putStrike,
                    putPosition,
                    putAtm,
                    putPayoff,
                    replication,
                    naked),
            Schedule,
            Notionals,
            Index,
            PaymentDayCounter,
            PaymentAdjustment,
            FixingDays,
            Gearings,
            Spreads,
            IsInArrears,
            CallStrikes,
            LongCallOption,
            CallAtm,
            CallPayoffs,
            PutStrikes,
            LongPutOption,
            PutAtm,
            PutPayoffs,
            Replication,
            NakedOption,
            stored: true,
            out Exception? zeroGearingRefusal);
        return zeroGearingRefusal is null ? leg : throw zeroGearingRefusal;
    }
}
