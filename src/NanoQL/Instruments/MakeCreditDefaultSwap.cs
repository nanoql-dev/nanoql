// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makecds.hpp ql/instruments/makecds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A builder of standard credit default swaps.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToCreditDefaultSwap"/> builds the swap, in place of QuantLib's conversion operators. Its defaults
/// are QuantLib's: protection bought on a notional of 1, no upfront, quarterly coupons under the
/// <see cref="DateGenerationRule.CDS"/> rule, Following, Actual/360 with Actual/360 (including the last day) for the
/// last period, accrual settled and paid at the default time, the accrual rebated, and three days of cash settlement.
/// </para>
/// <para>
/// The trade date is the evaluation date unless given, and the upfront date the trade date advanced by the cash
/// settlement days on <see cref="WeekendsOnly"/>. Unless a schedule is given, the protection starts on the trade date
/// under the 2009 rules and the day after it under the others, and the schedule runs from it on
/// <see cref="WeekendsOnly"/> to the term date, or, from a tenor, to the trade date plus the tenor or, under a CDS
/// rule, to <see cref="CreditDefaultSwapFunctions.CdsMaturity"/>'s maturity, which the schedule refuses when there is
/// none ("null termination date"). <see cref="WithPricingEngine"/> is kept and changes nothing: the swap holds no
/// engine. A builder has no value equality.
/// </para>
/// </remarks>
public sealed record MakeCreditDefaultSwap
{
    /// <summary>Initializes a new instance of the <see cref="MakeCreditDefaultSwap"/> record from a tenor.</summary>
    /// <param name="tenor">The tenor, from the trade date.</param>
    /// <param name="runningSpread">The running spread, in fractional units.</param>
    public MakeCreditDefaultSwap(Period tenor, double runningSpread)
    {
        Tenor = tenor;
        RunningSpread = runningSpread;
    }

    /// <summary>Initializes a new instance of the <see cref="MakeCreditDefaultSwap"/> record from a term
    /// date.</summary>
    /// <param name="termDate">The term date, the schedule's last.</param>
    /// <param name="runningSpread">The running spread, in fractional units.</param>
    /// <exception cref="ArgumentException"><paramref name="termDate"/> is <c>default(Date)</c>.</exception>
    public MakeCreditDefaultSwap(Date termDate, double runningSpread)
    {
        Date.ThrowIfDefault(termDate, nameof(termDate));
        TermDate = termDate;
        RunningSpread = runningSpread;
    }

    /// <summary>Initializes a new instance of the <see cref="MakeCreditDefaultSwap"/> record from a
    /// schedule.</summary>
    /// <param name="schedule">The coupon schedule.</param>
    /// <param name="runningSpread">The running spread, in fractional units.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> is null.</exception>
    public MakeCreditDefaultSwap(Schedule schedule, double runningSpread)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Schedule = schedule;
        RunningSpread = runningSpread;
    }

    private Period? Tenor { get; init; }

    private Date? TermDate { get; init; }

    private Schedule? Schedule { get; init; }

    private double RunningSpread { get; init; }

    private ProtectionSide Side { get; init; } = ProtectionSide.Buyer;

    private double Nominal { get; init; } = 1.0;

    private double UpfrontRate { get; init; }

    private Period CouponTenor { get; init; } = new(3, TimeUnit.Months);

    private DateGenerationRule Rule { get; init; } = DateGenerationRule.CDS;

    private BusinessDayConvention Convention { get; init; } = BusinessDayConvention.Following;

    private DayCounter DayCounter { get; init; } = new Actual360();

    private bool SettlesAccrual { get; init; } = true;

    private bool PaysAtDefaultTime { get; init; } = true;

    private Date? ProtectionStart { get; init; }

    private Date? UpfrontDate { get; init; }

    private Claim? Claim { get; init; }

    private DayCounter? LastPeriodDayCounter { get; init; } = new Actual360(true);

    private bool RebatesAccrual { get; init; } = true;

    private Date? TradeDate { get; init; }

    private int CashSettlementDays { get; init; } = 3;

    /// <summary>Builds the swap.</summary>
    /// <param name="context">The evaluation context: its evaluation date is the trade date when none is given.</param>
    /// <returns>The swap.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The schedule can't be built, or the swap refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A given schedule is empty and no protection start is given, as
    /// QuantLib's <c>schedule_-&gt;at(0)</c> refuses it.</exception>
    /// <exception cref="NanoQLException">The maturity isn't after the trade date, or the coupon leg can't be
    /// built.</exception>
    public CreditDefaultSwap ToCreditDefaultSwap(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date tradeDate = TradeDate ?? context.EvaluationDate;
        Date upfrontDate = UpfrontDate ?? new WeekendsOnly().Advance(tradeDate, CashSettlementDays, TimeUnit.Days);

        Date protectionStart;
        if (ProtectionStart is Date given)
        {
            protectionStart = given;
        }
        else if (Schedule is not null)
        {
            protectionStart = Schedule.At(0);
        }
        else if (Rule == DateGenerationRule.CDS2015 || Rule == DateGenerationRule.CDS)
        {
            protectionStart = tradeDate;
        }
        else
        {
            protectionStart = tradeDate + 1;
        }

        // schedule, tenor and term date come from different constructors; exactly one of them is not null.
        Schedule schedule;
        if (Schedule is not null)
        {
            schedule = Schedule;
        }
        else
        {
            Date end;
            if (Tenor is Period tenor)
            {
                if (Rule == DateGenerationRule.CDS2015 || Rule == DateGenerationRule.CDS
                    || Rule == DateGenerationRule.OldCDS)
                {
                    // QuantLib's Date() when there is no maturity, which the schedule refuses
                    end = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, Rule) ?? default;
                }
                else
                {
                    end = tradeDate + tenor;
                }
            }
            else
            {
                end = TermDate!.Value;
            }

            schedule = new Schedule(
                protectionStart,
                end,
                CouponTenor,
                new WeekendsOnly(),
                Convention,
                BusinessDayConvention.Unadjusted,
                Rule,
                false,
                context);
        }

        return new CreditDefaultSwap(
            Side,
            Nominal,
            UpfrontRate,
            RunningSpread,
            schedule,
            Convention,
            DayCounter,
            SettlesAccrual,
            PaysAtDefaultTime,
            protectionStart,
            upfrontDate,
            Claim,
            LastPeriodDayCounter,
            RebatesAccrual,
            tradeDate,
            CashSettlementDays);
    }

    /// <summary>Sets whether the protection is bought or sold; bought by default.</summary>
    /// <param name="side">The side; the engines refuse a value outside the enum, as QuantLib's do.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap WithSide(ProtectionSide side) => this with { Side = side };

    /// <summary>Sets the notional; 1 by default.</summary>
    /// <param name="nominal">The notional.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap WithNominal(double nominal) => this with { Nominal = nominal };

    /// <summary>Sets the upfront, in fractional units; 0 by default.</summary>
    /// <param name="upfrontRate">The upfront.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap WithUpfrontRate(double upfrontRate) => this with { UpfrontRate = upfrontRate };

    /// <summary>Sets the coupon tenor of a schedule the builder generates; three months by default.</summary>
    /// <param name="couponTenor">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap WithCouponTenor(Period couponTenor) => this with { CouponTenor = couponTenor };

    /// <summary>Sets the date-generation rule; <see cref="DateGenerationRule.CDS"/> by default.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rule"/> isn't a member of its type.</exception>
    public MakeCreditDefaultSwap WithDateGenerationRule(DateGenerationRule rule)
    {
        EnumArguments.Check(rule, nameof(rule));
        return this with { Rule = rule };
    }

    /// <summary>Sets the business-day convention of the schedule and the payments; Following by default.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="convention"/> isn't a member of its
    /// type.</exception>
    public MakeCreditDefaultSwap WithConvention(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { Convention = convention };
    }

    /// <summary>Sets the day counter of the accrual; Actual/360 by default.</summary>
    /// <param name="dayCounter">The day counter.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is null.</exception>
    public MakeCreditDefaultSwap WithDayCounter(DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        return this with { DayCounter = dayCounter };
    }

    /// <summary>Sets whether the accrued coupon is due on a default; it is by default.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap SettleAccrual(bool b = true) => this with { SettlesAccrual = b };

    /// <summary>Sets whether the payments a default triggers are due at the default time; they are by
    /// default.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap PayAtDefaultTime(bool b = true) => this with { PaysAtDefaultTime = b };

    /// <summary>Sets the protection start.</summary>
    /// <param name="d">The date, or <see langword="null"/>, QuantLib's <c>Date()</c>, to deduce it.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public MakeCreditDefaultSwap WithProtectionStart(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { ProtectionStart = d };
    }

    /// <summary>Sets the settlement date of the upfront and the accrual rebate.</summary>
    /// <param name="d">The date, or <see langword="null"/>, QuantLib's <c>Date()</c>, to deduce it.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public MakeCreditDefaultSwap WithUpfrontDate(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { UpfrontDate = d };
    }

    /// <summary>Sets the claim.</summary>
    /// <param name="claim">The claim, or <see langword="null"/> for a <see cref="FaceValueClaim"/>.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap WithClaim(Claim? claim) => this with { Claim = claim };

    /// <summary>Sets the day counter of the last period's accrual; Actual/360 (including the last day) by
    /// default.</summary>
    /// <param name="lastPeriodDayCounter">The day counter, or <see langword="null"/>, QuantLib's empty one, for the
    /// accrual's.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap WithLastPeriodDayCounter(DayCounter? lastPeriodDayCounter) =>
        this with { LastPeriodDayCounter = lastPeriodDayCounter };

    /// <summary>Sets whether the seller pays the accrued current coupon at the cash settlement date; it does by
    /// default.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCreditDefaultSwap RebateAccrual(bool b = true) => this with { RebatesAccrual = b };

    /// <summary>Sets the trade date.</summary>
    /// <param name="tradeDate">The date, or <see langword="null"/>, QuantLib's <c>Date()</c>, for the evaluation
    /// date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="tradeDate"/> is <c>default(Date)</c>.</exception>
    public MakeCreditDefaultSwap WithTradeDate(Date? tradeDate)
    {
        Date.ThrowIfDefault(tradeDate, nameof(tradeDate));
        return this with { TradeDate = tradeDate };
    }

    /// <summary>Sets the business days from the trade date to the cash settlement date; 3 by default.</summary>
    /// <param name="cashSettlementDays">The days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cashSettlementDays"/> is negative.</exception>
    public MakeCreditDefaultSwap WithCashSettlementDays(int cashSettlementDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cashSettlementDays);
        return this with { CashSettlementDays = cashSettlementDays };
    }

    /// <summary>
    /// Kept from QuantLib, where it sets the engine the swap is priced with: it changes nothing, since the swap holds
    /// no engine; price the swap by calling the engine.
    /// </summary>
    /// <param name="engine">The engine, a function of the swap and the context, such as
    /// <c>(s, ctx) =&gt; MidPointCdsEngine.Calculate(s, ctx, probability, recovery, curve)</c>.</param>
    /// <returns>The builder, unchanged.</returns>
    public MakeCreditDefaultSwap WithPricingEngine(
        Func<CreditDefaultSwap, EvaluationContext, InstrumentResults>? engine) => this;
}
