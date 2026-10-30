// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2023 Andrea Pellegatta
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/defaultprobabilityhelpers.hpp ql/termstructures/credit/defaultprobabilityhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The base of the credit-default-swap bootstrap helpers: a standard swap from the evaluation date, repriced on the
/// default-probability curve under construction.
/// </summary>
/// <remarks>
/// <para>
/// The helper's dates follow the evaluation date: the protection starts the settlement days after it, and the schedule
/// runs from the protection start, or the given start date, to the maturity the tenor gives, under a CDS rule
/// <see cref="CreditDefaultSwapFunctions.CdsMaturity"/>'s; the start is adjusted to the payment convention under the
/// other rules only. The earliest date is the schedule's first, the latest its last adjusted, a day later under the
/// ISDA model. The constructor computes them through the helper's own body, as QuantLib calls
/// <c>CdsHelper::initializeDates()</c> explicitly; a re-dated copy computes them through
/// <see cref="InitializeDates"/>, which a derived helper extends.
/// </para>
/// <para>
/// A bound copy (<see cref="DefaultProbabilityHelperExtensions.SetTermStructure{T}"/>) builds its swap, which
/// <see cref="Swap"/> returns, and prices it with the model's engine on the bound curve: QuantLib's
/// <c>resetEngine()</c>, whose engine half is the same for both helpers and lives here. The mid-point engine takes the
/// context's settlement-date flag; the ISDA engine leaves out the flows on the protection start, with QuantLib's
/// options. A model outside the enum is refused at construction, with QuantLib's "unknown CDS pricing model".
/// </para>
/// </remarks>
public abstract class CdsHelper : RelativeDateBootstrapHelper<DefaultProbabilityTermStructure>
{
    private readonly Period tenor;
    private readonly int settlementDays;
    private readonly Calendar? calendar;
    private readonly Frequency frequency;
    private readonly DateGenerationRule rule;
    private readonly double recoveryRate;
    private readonly YieldTermStructure? discountCurve;
    private readonly Date? startDate;

    // Written by the dates' body, on a helper under construction or a fresh copy (InitializeDates), never after.
    private Schedule schedule = null!;
    private Date protectionStart;

    // Written on a bound copy (WithTermStructure), never after.
    private CreditDefaultSwap? swap;

    // The provider at the helper's construction, which its swap is built in when the bootstrap binds it.
    private readonly ConventionProvider provider = ConventionProvider.Current;

    /// <summary>Initializes a new instance of the <see cref="CdsHelper"/> class.</summary>
    /// <param name="quote">The helper's market quote.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="settlementDays">The calendar days from the evaluation date to the protection start: typically 1
    /// before the CDS Big Bang of 2009, 0 after it.</param>
    /// <param name="calendar">The swap's calendar, or <see langword="null"/> for QuantLib's empty calendar, used
    /// unchecked as QuantLib uses it.</param>
    /// <param name="frequency">The coupon frequency.</param>
    /// <param name="paymentConvention">The convention of the schedule and the payments.</param>
    /// <param name="rule">The date-generation rule.</param>
    /// <param name="dayCounter">The day counter of the coupons.</param>
    /// <param name="recoveryRate">The reference entity's recovery rate.</param>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/>, QuantLib's empty handle, which the
    /// engines refuse.</param>
    /// <param name="context">The evaluation context whose evaluation date the helper's dates follow.</param>
    /// <param name="settlesAccrual">Whether the accrued coupon is due on a default.</param>
    /// <param name="paysAtDefaultTime">Whether the payments a default triggers are due at the default time.</param>
    /// <param name="startDate">The schedule's start and the date the maturity is counted from, or
    /// <see langword="null"/>, QuantLib's <c>Date()</c>, for the protection start and the evaluation date.</param>
    /// <param name="lastPeriodDayCounter">The day counter of the last coupon, or <see langword="null"/>, QuantLib's
    /// empty one, for <paramref name="dayCounter"/>.</param>
    /// <param name="rebatesAccrual">Whether the accrual is rebated at the cash settlement date.</param>
    /// <param name="model">The model the swap is priced with.</param>
    /// <param name="tradeDate">The swap's trade date, or <see langword="null"/> for the swap's own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type: for the model, with
    /// QuantLib's "unknown CDS pricing model".</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the schedule can't be built.</exception>
    /// <exception cref="NanoQLException">The schedule has no maturity, with QuantLib's "termination date not
    /// provided", or can't be generated.</exception>
    protected CdsHelper(
        double quote,
        Period tenor,
        int settlementDays,
        Calendar? calendar,
        Frequency frequency,
        BusinessDayConvention paymentConvention,
        DateGenerationRule rule,
        DayCounter dayCounter,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        EvaluationContext context,
        bool settlesAccrual = true,
        bool paysAtDefaultTime = true,
        Date? startDate = null,
        DayCounter? lastPeriodDayCounter = null,
        bool rebatesAccrual = true,
        CreditDefaultSwapPricingModel model = CreditDefaultSwapPricingModel.Midpoint,
        Date? tradeDate = null)
        : base(quote, context ?? throw new ArgumentNullException(nameof(context)))
    {
        EnumArguments.Check(frequency, nameof(frequency));
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(rule, nameof(rule));
        if (model is not (CreditDefaultSwapPricingModel.Midpoint or CreditDefaultSwapPricingModel.ISDA))
        {
            throw new ArgumentOutOfRangeException(nameof(model), model, $"unknown CDS pricing model: {(int)model}");
        }

        ArgumentNullException.ThrowIfNull(dayCounter);
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(tradeDate, nameof(tradeDate));
        this.tenor = tenor;
        this.settlementDays = settlementDays;
        this.calendar = calendar;
        this.frequency = frequency;
        PaymentConvention = paymentConvention;
        this.rule = rule;
        DayCounter = dayCounter;
        this.recoveryRate = recoveryRate;
        this.discountCurve = discountCurve;
        SettlesAccrual = settlesAccrual;
        PaysAtDefaultTime = paysAtDefaultTime;
        this.startDate = startDate;
        LastPeriodDayCounter = lastPeriodDayCounter;
        RebatesAccrual = rebatesAccrual;
        Model = model;
        TradeDate = tradeDate;

        // QuantLib's explicit CdsHelper::initializeDates(), not the derived helper's
        CdsDates();
    }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>.</summary>
    /// <value>The swap, or <see langword="null"/> until the helper is bound to a curve, as QuantLib's is.</value>
    public CreditDefaultSwap? Swap => swap;

    /// <summary>Gets the model the swap is priced with.</summary>
    public CreditDefaultSwapPricingModel Model { get; }

    /// <summary>Gets the convention of the schedule and the payments.</summary>
    protected BusinessDayConvention PaymentConvention { get; }

    /// <summary>Gets the day counter of the coupons.</summary>
    protected DayCounter DayCounter { get; }

    /// <summary>Gets whether the accrued coupon is due on a default.</summary>
    protected bool SettlesAccrual { get; }

    /// <summary>Gets whether the payments a default triggers are due at the default time.</summary>
    protected bool PaysAtDefaultTime { get; }

    /// <summary>Gets the day counter of the last coupon, or <see langword="null"/> for the coupons'.</summary>
    protected DayCounter? LastPeriodDayCounter { get; }

    /// <summary>Gets whether the accrual is rebated at the cash settlement date.</summary>
    protected bool RebatesAccrual { get; }

    /// <summary>Gets the swap's trade date, or <see langword="null"/> for the swap's own.</summary>
    protected Date? TradeDate { get; }

    /// <summary>Gets the swap's schedule.</summary>
    protected Schedule Schedule => schedule;

    /// <summary>Gets the protection start: the evaluation date plus the settlement days.</summary>
    protected Date ProtectionStart => protectionStart;

    /// <summary>Gets the calendar, or <see langword="null"/> for QuantLib's empty calendar.</summary>
    protected Calendar? Calendar => calendar;

    /// <inheritdoc/>
    protected internal override BootstrapHelper<DefaultProbabilityTermStructure> WithTermStructure(
        DefaultProbabilityTermStructure t)
    {
        var copy = (CdsHelper)base.WithTermStructure(t);

        // built in the provider the helper was built with
        using (ConventionProvider.Use(provider))
        {
            copy.swap = copy.BuildSwap();
        }

        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CdsDates();
    }

    /// <summary>The swap the helper prices, from its dates: the swap half of QuantLib's <c>resetEngine()</c>.</summary>
    /// <returns>The swap.</returns>
    protected abstract CreditDefaultSwap BuildSwap();

    /// <summary>Prices the swap on the bound curve with the model's engine: the engine half of QuantLib's
    /// <c>resetEngine()</c>, then <c>recalculate()</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The swap's results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The helper isn't bound to a term structure, or the engine refuses the
    /// swap or the curves.</exception>
    protected CreditDefaultSwapResults Price(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        CreditDefaultSwap cds = swap!;
        return Model switch
        {
            CreditDefaultSwapPricingModel.ISDA => IsdaCdsEngine.Calculate(
                cds,
                context,
                TermStructure,
                recoveryRate,
                discountCurve,
                false,
                IsdaCdsEngineNumericalFix.Taylor,
                IsdaCdsEngineAccrualBias.HalfDayBias,
                IsdaCdsEngineForwardsInCouponPeriod.Piecewise),
            CreditDefaultSwapPricingModel.Midpoint => MidPointCdsEngine.Calculate(
                cds, context, TermStructure, recoveryRate, discountCurve),
            _ => throw new NanoQLException($"unknown CDS pricing model: {(int)Model}"),
        };
    }

    // QuantLib's CdsHelper::initializeDates().
    private void CdsDates()
    {
        Date evaluationDate = EvaluationDate!.Value;
        Calendar cal = calendar ?? EmptyCalendar.Instance;
        protectionStart = evaluationDate + settlementDays;

        Date start = startDate ?? protectionStart;

        // Only adjust start date if rule is not CDS or CDS2015. Unsure about OldCDS.
        if (rule != DateGenerationRule.CDS && rule != DateGenerationRule.CDS2015)
        {
            start = cal.Adjust(start, PaymentConvention);
        }

        Date? endDate;
        if (rule == DateGenerationRule.CDS2015 || rule == DateGenerationRule.CDS || rule == DateGenerationRule.OldCDS)
        {
            Date refDate = startDate ?? evaluationDate;
            endDate = CreditDefaultSwapFunctions.CdsMaturity(refDate, tenor, rule);
        }
        else
        {
            // Keep the old logic here
            Date refDate = startDate is Date s ? s + settlementDays : protectionStart;
            endDate = refDate + tenor;
        }

        // QuantLib's Date() when there is no maturity, which the schedule's builder refuses
        MakeSchedule make = new MakeSchedule().From(start);
        if (endDate is Date end)
        {
            make = make.To(end);
        }

        schedule = make
            .WithFrequency(frequency)
            .WithCalendar(calendar)
            .WithConvention(PaymentConvention)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .WithRule(rule)
            .ToSchedule();
        earliestDate = schedule.Dates[0];
        latestDate = cal.Adjust(schedule.Dates[^1], PaymentConvention);
        if (Model == CreditDefaultSwapPricingModel.ISDA)
        {
            latestDate = latestDate.Value + 1;
        }
    }
}
