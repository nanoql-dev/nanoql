// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/creditdefaultswap.hpp ql/instruments/creditdefaultswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A credit default swap: a premium leg of running-spread coupons against protection on a reference
/// entity's default, with an optional upfront and an accrual rebate.</summary>
/// <remarks>
/// <para>
/// The instrument assumes that the issuer hasn't defaulted by the evaluation date. With
/// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> set, payments at the swap's settlement date may count in
/// its NPV and its fair spread. The implied quantities price with the mid-point engine by default, which isn't ISDA
/// conformant.
/// </para>
/// <para>
/// QuantLib's constructors read no evaluation date: the trade date, when not given, is deduced from the protection
/// start and the schedule's rule, and the cash settlement date from the trade date. An empty schedule is refused before
/// its first date is read, where QuantLib, given no protection start, reads it first for one, an undefined read (a
/// documented divergence). The accrual rebate reads its first live coupon's amount or accrued amount through the
/// coupon's context-free forms (SID10). The engines are static (<see cref="MidPointCdsEngine"/>,
/// <see cref="IsdaCdsEngine"/>), and the results are a <see cref="CreditDefaultSwapResults"/>. An expired swap's
/// results are all 0, its accrual rebate's NPV included, where QuantLib leaves that one unset and reads an
/// uninitialized value, a documented divergence. Equality is by reference.
/// </para>
/// <para>
/// <see cref="ImpliedHazardRate"/> and <see cref="ConventionalSpread"/> refuse an expired swap ("instrument expired"),
/// where QuantLib returns its solver's guess or a sentinel; the conventional spread also refuses where the engine
/// leaves the fair spread null, with QuantLib's "fair spread not available". Under the ISDA model their flat curve is
/// dated on the evaluation date, which the ISDA engine requires, where QuantLib's rolls a weekend to Monday and its
/// engine then refuses it: documented divergences.
/// </para>
/// </remarks>
public sealed class CreditDefaultSwap : Instrument
{
    // The provider at the swap's construction, which the implied quantities' flat curve is built in.
    private readonly ConventionProvider provider = ConventionProvider.Current;

    /// <summary>Initializes a new instance of the <see cref="CreditDefaultSwap"/> class quoted as a running spread
    /// only.</summary>
    /// <param name="side">Whether the protection is bought or sold.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="spread">The running spread, in fractional units.</param>
    /// <param name="schedule">The coupon schedule.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="dayCounter">The day counter of the accrual.</param>
    /// <param name="settlesAccrual">Whether the accrued coupon is due on a default.</param>
    /// <param name="paysAtDefaultTime">Whether the payments a default triggers are due at the default time, or at the
    /// end of the accrual period.</param>
    /// <param name="protectionStart">The first date a default triggers the contract; the schedule's first date when
    /// <see langword="null"/>.</param>
    /// <param name="claim">The claim, or <see langword="null"/> for a <see cref="FaceValueClaim"/>.</param>
    /// <param name="lastPeriodDayCounter">The day counter of the last period's accrual, or <see langword="null"/> for
    /// <paramref name="dayCounter"/>.</param>
    /// <param name="rebatesAccrual">Whether the seller pays the accrued current coupon at the cash settlement
    /// date.</param>
    /// <param name="tradeDate">The trade date, or <see langword="null"/> to deduce it from the protection start and the
    /// schedule's rule.</param>
    /// <param name="cashSettlementDays">The business days from the trade date to the cash settlement date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The schedule is empty; a date is <c>default(Date)</c>; the protection starts
    /// after the accrual under a pre-2009 rule; or the cash settlement date is before the protection start.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type, or
    /// <paramref name="cashSettlementDays"/> is negative.</exception>
    /// <exception cref="NanoQLException">The coupon leg can't be built.</exception>
    public CreditDefaultSwap(
        ProtectionSide side,
        double notional,
        double spread,
        Schedule schedule,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        bool settlesAccrual = true,
        bool paysAtDefaultTime = true,
        Date? protectionStart = null,
        Claim? claim = null,
        DayCounter? lastPeriodDayCounter = null,
        bool rebatesAccrual = true,
        Date? tradeDate = null,
        int cashSettlementDays = 3)
        : this(
            side,
            notional,
            null,
            spread,
            schedule,
            paymentConvention,
            dayCounter,
            settlesAccrual,
            paysAtDefaultTime,
            protectionStart,
            null,
            claim,
            lastPeriodDayCounter,
            rebatesAccrual,
            tradeDate,
            cashSettlementDays)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CreditDefaultSwap"/> class quoted as an upfront and a
    /// running spread.</summary>
    /// <param name="side">Whether the protection is bought or sold.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="upfront">The upfront, in fractional units.</param>
    /// <param name="spread">The running spread, in fractional units.</param>
    /// <param name="schedule">The coupon schedule.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="dayCounter">The day counter of the accrual.</param>
    /// <param name="settlesAccrual">Whether the accrued coupon is due on a default.</param>
    /// <param name="paysAtDefaultTime">Whether the payments a default triggers are due at the default time, or at the
    /// end of the accrual period.</param>
    /// <param name="protectionStart">The first date a default triggers the contract; the schedule's first date when
    /// <see langword="null"/>.</param>
    /// <param name="upfrontDate">The settlement date of the upfront and the accrual rebate, or
    /// <see langword="null"/> for the cash settlement date after the trade date.</param>
    /// <param name="claim">The claim, or <see langword="null"/> for a <see cref="FaceValueClaim"/>.</param>
    /// <param name="lastPeriodDayCounter">The day counter of the last period's accrual, or <see langword="null"/> for
    /// <paramref name="dayCounter"/>.</param>
    /// <param name="rebatesAccrual">Whether the seller pays the accrued current coupon at the cash settlement
    /// date.</param>
    /// <param name="tradeDate">The trade date, or <see langword="null"/> to deduce it from the protection start and the
    /// schedule's rule.</param>
    /// <param name="cashSettlementDays">The business days from the trade date to the cash settlement date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The schedule is empty; a date is <c>default(Date)</c>; the protection starts
    /// after the accrual under a pre-2009 rule; or the cash settlement date is before the protection start.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type, or
    /// <paramref name="cashSettlementDays"/> is negative.</exception>
    /// <exception cref="NanoQLException">The coupon leg can't be built.</exception>
    public CreditDefaultSwap(
        ProtectionSide side,
        double notional,
        double upfront,
        double spread,
        Schedule schedule,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        bool settlesAccrual = true,
        bool paysAtDefaultTime = true,
        Date? protectionStart = null,
        Date? upfrontDate = null,
        Claim? claim = null,
        DayCounter? lastPeriodDayCounter = null,
        bool rebatesAccrual = true,
        Date? tradeDate = null,
        int cashSettlementDays = 3)
        : this(
            side,
            notional,
            (double?)upfront,
            spread,
            schedule,
            paymentConvention,
            dayCounter,
            settlesAccrual,
            paysAtDefaultTime,
            protectionStart,
            upfrontDate,
            claim,
            lastPeriodDayCounter,
            rebatesAccrual,
            tradeDate,
            cashSettlementDays)
    {
    }

    // The two constructors' work: QuantLib's member initializers, then its init().
    private CreditDefaultSwap(
        ProtectionSide side,
        double notional,
        double? upfront,
        double spread,
        Schedule schedule,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        bool settlesAccrual,
        bool paysAtDefaultTime,
        Date? protectionStart,
        Date? upfrontDate,
        Claim? claim,
        DayCounter? lastPeriodDayCounter,
        bool rebatesAccrual,
        Date? tradeDate,
        int cashSettlementDays)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(dayCounter);
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        Date.ThrowIfDefault(protectionStart, nameof(protectionStart));
        Date.ThrowIfDefault(upfrontDate, nameof(upfrontDate));
        Date.ThrowIfDefault(tradeDate, nameof(tradeDate));
        ArgumentOutOfRangeException.ThrowIfNegative(cashSettlementDays);

        Side = side;
        Notional = notional;
        Upfront = upfront;
        RunningSpread = spread;
        SettlesAccrual = settlesAccrual;
        PaysAtDefaultTime = paysAtDefaultTime;
        CashSettlementDays = cashSettlementDays;

        if (schedule.Empty)
        {
            throw new ArgumentException("CreditDefaultSwap needs a non-empty schedule.", nameof(schedule));
        }

        ProtectionStartDate = protectionStart ?? schedule[0];

        bool postBigBang = false;
        if (schedule.HasRule)
        {
            DateGenerationRule rule = schedule.Rule();
            postBigBang = rule == DateGenerationRule.CDS || rule == DateGenerationRule.CDS2015;
        }

        if (!postBigBang)
        {
            if (!(ProtectionStartDate <= schedule[0]))
            {
                throw new ArgumentException("protection can not start after accrual", nameof(protectionStart));
            }
        }

        Coupons = new FixedRateLeg(schedule)
            .WithNotionals(notional)
            .WithCouponRates(spread, dayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithLastPeriodDayCounter(lastPeriodDayCounter)
            .ToLeg();

        // Deduce the trade date if not given.
        TradeDate = tradeDate ?? (postBigBang ? ProtectionStartDate : ProtectionStartDate - 1);

        // Deduce the cash settlement date if not given; QuantLib's empty calendar, null here, is used unchecked.
        Date effectiveUpfrontDate = upfrontDate
            ?? (schedule.Calendar ?? EmptyCalendar.Instance).Advance(
                TradeDate, cashSettlementDays, TimeUnit.Days, paymentConvention);
        if (!(effectiveUpfrontDate >= ProtectionStartDate))
        {
            throw new ArgumentException(
                "The cash settlement date must not be before the protection start date.", nameof(upfrontDate));
        }

        // Create the upfront payment, if one is provided.
        double upfrontAmount = 0.0;
        if (upfront is double u)
        {
            upfrontAmount = u * notional;
        }

        UpfrontPayment = new SimpleCashFlow(upfrontAmount, effectiveUpfrontDate);

        // Set the maturity date.
        Maturity = schedule.Dates[^1];

        // Deal with the accrual rebate. We use the standard conventions for accrual calculation introduced with the
        // CDS Big Bang in 2009.
        if (rebatesAccrual)
        {
            double rebateAmount = 0.0;
            Date refDate = TradeDate + 1;

            if (TradeDate >= schedule.Dates[0])
            {
                for (int i = 0; i < Coupons.Length; ++i)
                {
                    CashFlow cf = Coupons[i];
                    if (refDate > cf.Date)
                    {
                        // This coupon is in the past; check the next one
                        continue;
                    }
                    else if (refDate == cf.Date)
                    {
                        // This coupon pays at the reference date.
                        // If it's not the last coupon, the accrual is 0 so do nothing.
                        if (i < Coupons.Length - 1)
                        {
                            rebateAmount = 0.0;
                        }
                        else
                        {
                            // On last coupon
                            rebateAmount = ((FixedRateCoupon)cf).Amount();
                        }

                        break;
                    }
                    else
                    {
                        // This coupon pays in the future, and is the first coupon to do so (since they're sorted).
                        // Calculate the accrual and skip further coupons
                        rebateAmount = ((FixedRateCoupon)cf).AccruedAmount(refDate);
                        break;
                    }
                }
            }

            AccrualRebate = new SimpleCashFlow(rebateAmount, effectiveUpfrontDate);
        }

        Claim = claim ?? new FaceValueClaim();
    }

    /// <summary>Gets whether the protection is bought or sold.</summary>
    public ProtectionSide Side { get; }

    /// <summary>Gets the notional.</summary>
    public double Notional { get; }

    /// <summary>Gets the running spread.</summary>
    public double RunningSpread { get; }

    /// <summary>Gets the upfront, or <see langword="null"/> for a swap quoted as a running spread only.</summary>
    public double? Upfront { get; }

    /// <summary>Gets a value indicating whether the accrued coupon is due on a default.</summary>
    public bool SettlesAccrual { get; }

    /// <summary>Gets a value indicating whether the payments a default triggers are due at the default time.</summary>
    public bool PaysAtDefaultTime { get; }

    /// <summary>Gets the premium leg's coupons.</summary>
    public ImmutableArray<CashFlow> Coupons { get; }

    /// <summary>Gets the first date a default triggers the contract.</summary>
    public Date ProtectionStartDate { get; }

    /// <summary>Gets the last date a default triggers the contract: the last coupon's accrual end.</summary>
    public Date ProtectionEndDate => ((Coupon)Coupons[^1]).AccrualEndDate;

    /// <summary>Gets a value indicating whether the seller pays the accrued current coupon at the cash settlement
    /// date.</summary>
    public bool RebatesAccrual => AccrualRebate is not null;

    /// <summary>Gets the upfront payment, an amount of 0 for a swap quoted as a running spread only.</summary>
    public SimpleCashFlow UpfrontPayment { get; }

    /// <summary>Gets the accrual rebate, or <see langword="null"/> when the swap doesn't rebate its accrual.</summary>
    public SimpleCashFlow? AccrualRebate { get; }

    /// <summary>Gets the trade date.</summary>
    public Date TradeDate { get; }

    /// <summary>Gets the business days from the trade date to the cash settlement date.</summary>
    public int CashSettlementDays { get; }

    // QuantLib's claim_, which only its engines read.
    internal Claim Claim { get; }

    // QuantLib's maturity_: the schedule's last date.
    internal Date Maturity { get; }

    /// <summary>Whether every coupon has occurred.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        for (int i = Coupons.Length - 1; i >= 0; --i)
        {
            if (!Coupons[i].HasOccurred(context))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The results of an expired swap: every value and sensitivity 0, the accrual rebate's NPV
    /// included.</summary>
    /// <returns>The results.</returns>
    public override InstrumentResults SetupExpired() => new CreditDefaultSwapResults
    {
        NPV = 0.0,
        ErrorEstimate = 0.0,
        ValuationDate = null,
        FairSpread = 0.0,
        FairUpfront = 0.0,
        CouponLegBPS = 0.0,
        UpfrontBPS = 0.0,
        CouponLegNPV = 0.0,
        DefaultLegNPV = 0.0,
        UpfrontNPV = 0.0,
        AccrualRebateNPV = 0.0,
    };

    /// <summary>QuantLib's checks of the swap's arguments, in its order. Those of a value its arguments can leave
    /// unset, which the constructors always set here (the notional, the spread, the upfront payment, the claim and the
    /// two dates), hold by construction, as does "coupons not set": a schedule of one date fails to build the leg, as
    /// QuantLib's reads its second date, and an empty one is refused.</summary>
    /// <exception cref="NanoQLException">The side is -1, QuantLib's unset value ("side not set"), or the notional is 0
    /// ("null notional set").</exception>
    public override void Validate()
    {
        if (!(Side != (ProtectionSide)(-1)))
        {
            throw new NanoQLException("side not set");
        }

        if (!(Notional != 0.0))
        {
            throw new NanoQLException("null notional set");
        }
    }

    /// <summary>The hazard rate that gives the swap a target NPV, on a flat hazard-rate curve.</summary>
    /// <param name="targetNPV">The target NPV.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="dayCounter">The flat curve's day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="model">The engine the swap is priced with.</param>
    /// <returns>The hazard rate.</returns>
    /// <remarks>
    /// Under <see cref="CreditDefaultSwapPricingModel.Midpoint"/> the curve is QuantLib's, settled no business days
    /// after the evaluation date on the weekends-only calendar; under <see cref="CreditDefaultSwapPricingModel.ISDA"/>
    /// it is dated on the evaluation date, which the ISDA engine requires. The solver starts from QuantLib's guess, the
    /// running spread over the loss rate times 365/360, with a tenth of it as its step.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="model"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The swap has expired ("instrument expired"); the engine refuses the curves; or
    /// the solver fails.</exception>
    public double ImpliedHazardRate(
        double targetNPV,
        YieldTermStructure discountCurve,
        DayCounter dayCounter,
        EvaluationContext context,
        double recoveryRate = 0.4,
        double accuracy = 1.0e-8,
        CreditDefaultSwapPricingModel model = CreditDefaultSwapPricingModel.Midpoint)
    {
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(context);
        Func<double, CreditDefaultSwapResults> price = Engine(model, recoveryRate, discountCurve, dayCounter, context);
        if (IsExpired(context))
        {
            throw new NanoQLException("instrument expired");
        }

        double F(double guess) => price(guess).NPV!.Value - targetNPV;

        // very close guess if targetNPV = 0.
        double guess = RunningSpread / (1 - recoveryRate) * 365.0 / 360.0;
        double step = 0.1 * guess;
        return new Brent().Solve(F, accuracy, guess, step);
    }

    /// <summary>
    /// The conventional, standard upfront-to-spread conversion: the running-only spread that gives the swap a zero NPV
    /// on the flat hazard-rate curve that prices it at par, under the ISDA's standard conventional recovery.
    /// </summary>
    /// <param name="conventionalRecovery">The conventional recovery rate, as ISDA specifies it: typically 0.4 for
    /// senior and 0.2 for subordinate debt.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="dayCounter">The flat curve's day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The engine the swap is priced with.</param>
    /// <returns>The spread.</returns>
    /// <remarks>
    /// The flat curve is <see cref="ImpliedHazardRate"/>'s. QuantLib returns its engine's fair spread from the solver's
    /// last evaluation, at the root; here the engine prices once more at the root the solver returns. A flat hazard
    /// rate doesn't recover the market quotes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="model"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The swap has expired ("instrument expired"); the engine leaves the fair spread
    /// null ("fair spread not available"); the engine refuses the curves; or the solver fails.</exception>
    public double ConventionalSpread(
        double conventionalRecovery,
        YieldTermStructure discountCurve,
        DayCounter dayCounter,
        EvaluationContext context,
        CreditDefaultSwapPricingModel model = CreditDefaultSwapPricingModel.Midpoint)
    {
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(context);
        Func<double, CreditDefaultSwapResults> price =
            Engine(model, conventionalRecovery, discountCurve, dayCounter, context);
        if (IsExpired(context))
        {
            throw new NanoQLException("instrument expired");
        }

        double F(double guess) => price(guess).NPV!.Value;

        double guess = RunningSpread / (1 - conventionalRecovery) * 365.0 / 360.0;
        double step = guess * 0.1;

        double root = new Brent().Solve(F, 1e-9, guess, step);
        return price(root).FairSpread ?? throw new NanoQLException("fair spread not available");
    }

    // The engine the implied quantities price with, on a flat hazard-rate curve at a trial rate: QuantLib's engine
    // object on its quote, called directly, with no expiry check.
    private Func<double, CreditDefaultSwapResults> Engine(
        CreditDefaultSwapPricingModel model,
        double recoveryRate,
        YieldTermStructure discountCurve,
        DayCounter dayCounter,
        EvaluationContext context)
    {
        switch (model)
        {
            case CreditDefaultSwapPricingModel.Midpoint:
            {
                // the flat curve's calendar, built in the provider the swap was built with
                Calendar weekendsOnly;
                using (ConventionProvider.Use(provider))
                {
                    weekendsOnly = new WeekendsOnly();
                }

                return h => MidPointCdsEngine.Body(
                    this, context, FlatHazardRate.Create(0, weekendsOnly, h, dayCounter, context), recoveryRate, discountCurve, null);
            }

            case CreditDefaultSwapPricingModel.ISDA:
                // the curve the ISDA engine requires, on the evaluation date
                return h => IsdaCdsEngine.Body(
                    this,
                    context,
                    new FlatHazardRate(context.EvaluationDate, h, dayCounter),
                    recoveryRate,
                    discountCurve,
                    null,
                    IsdaCdsEngineNumericalFix.Taylor,
                    IsdaCdsEngineAccrualBias.HalfDayBias,
                    IsdaCdsEngineForwardsInCouponPeriod.Piecewise);
            default:
                throw new ArgumentOutOfRangeException(nameof(model), model, $"unknown CDS pricing model: {(int)model}");
        }
    }
}
