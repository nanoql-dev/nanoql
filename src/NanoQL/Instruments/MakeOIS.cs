// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2026 Sergio Araujo
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makeois.hpp ql/instruments/makeois.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A builder of overnight-indexed swaps.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToOvernightIndexedSwap"/> builds the swap, in place of QuantLib's conversion operators; it takes
/// the context for the evaluation date the default start date counts from.
/// </para>
/// <para>
/// The default settlement days test the index's type, as QuantLib's builder does: 0 for <see cref="Sonia"/>, 1 for
/// <see cref="Corra"/> and 2 for any other index, so an index cloned from SONIA, a plain <see cref="OvernightIndex"/>,
/// settles in 2 days.
/// </para>
/// <para>
/// With no fixed rate, the swap is built at its par rate, as QuantLib's builder builds it: a swap at 0% is priced with
/// the engine given, or with <see cref="DiscountingSwapEngine"/> on the index's forwarding curve when none is. The
/// engine computes that rate and is never stored: the swap returned holds no engine. A builder has no value equality:
/// its vectors compare by reference.
/// </para>
/// </remarks>
public sealed record MakeOIS
{
    /// <summary>Initializes a new instance of the <see cref="MakeOIS"/> record.</summary>
    /// <param name="swapTenor">The swap's tenor.</param>
    /// <param name="overnightIndex">The overnight index, whose calendar and day counter are the legs' defaults.</param>
    /// <exception cref="ArgumentNullException">The index is <see langword="null"/>.</exception>
    public MakeOIS(Period swapTenor, OvernightIndex overnightIndex)
    {
        // QuantLib reads the index's conventions here, which is undefined for a null index.
        ArgumentNullException.ThrowIfNull(overnightIndex);
        SwapTenor = swapTenor;
        OvernightIndex = overnightIndex;
        FixedCalendar = overnightIndex.FixingCalendar;
        OvernightCalendar = overnightIndex.FixingCalendar;
        FixedDayCount = overnightIndex.DayCounter;
    }

    private Period SwapTenor { get; init; }

    private OvernightIndex OvernightIndex { get; init; }

    private double? FixedRate { get; init; }

    private Period ForwardStart { get; init; } = new(0, TimeUnit.Days);

    private int? SettlementDays { get; init; }

    private Date? EffectiveDate { get; init; }

    private Date? TerminationDate { get; init; }

    private Calendar FixedCalendar { get; init; }

    private Calendar OvernightCalendar { get; init; }

    private Calendar? SettlementCalendar { get; init; }

    private Frequency FixedPaymentFrequency { get; init; } = Frequency.Annual;

    private Frequency OvernightPaymentFrequency { get; init; } = Frequency.Annual;

    private Calendar? PaymentCalendar { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

    private int PaymentLag { get; init; }

    private BusinessDayConvention FixedConvention { get; init; } = BusinessDayConvention.ModifiedFollowing;

    private BusinessDayConvention FixedTerminationDateConvention { get; init; } =
        BusinessDayConvention.ModifiedFollowing;

    private BusinessDayConvention OvernightConvention { get; init; } = BusinessDayConvention.ModifiedFollowing;

    private BusinessDayConvention OvernightTerminationDateConvention { get; init; } =
        BusinessDayConvention.ModifiedFollowing;

    private DateGenerationRule FixedRule { get; init; } = DateGenerationRule.Backward;

    private DateGenerationRule OvernightRule { get; init; } = DateGenerationRule.Backward;

    private bool FixedEndOfMonth { get; init; }

    private bool OvernightEndOfMonth { get; init; }

    private bool IsDefaultEOM { get; init; } = true;

    private bool? MaturityEndOfMonth { get; init; }

    private SwapType Type { get; init; } = SwapType.Payer;

    private double Nominal { get; init; } = 1.0;

    private double OvernightSpread { get; init; }

    private DayCounter? FixedDayCount { get; init; }

    private Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? Engine { get; init; }

    private bool TelescopicValueDates { get; init; }

    private RateAveragingType AveragingMethod { get; init; } = RateAveragingType.Compound;

    private int? LookbackDays { get; init; }

    private int LockoutDays { get; init; }

    private bool ApplyObservationShift { get; init; }

    private int? RoundingPrecision { get; init; }

    /// <summary>Sets whether the fixed rate is received (or paid).</summary>
    /// <param name="flag">Whether the fixed rate is received.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS ReceiveFixed(bool flag = true) => this with { Type = flag ? SwapType.Receiver : SwapType.Payer };

    /// <summary>Sets whether the fixed rate is paid or received.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a member of the enum.</exception>
    public MakeOIS WithType(SwapType type)
    {
        EnumArguments.Check(type, nameof(type));
        return this with { Type = type };
    }

    /// <summary>Sets the nominal.</summary>
    /// <param name="n">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithNominal(double n) => this with { Nominal = n };

    /// <summary>Sets the fixed rate; with none, the swap is built at its par rate.</summary>
    /// <param name="k">
    /// The fixed rate, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for the par rate.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithFixedRate(double? k) => this with { FixedRate = k };

    /// <summary>Sets the forward start, after the spot date.</summary>
    /// <param name="f">The forward start.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithForwardStart(Period f) => this with { ForwardStart = f };

    /// <summary>
    /// Sets the settlement days, counted from the evaluation date on the settlement calendar; by default 0 for SONIA,
    /// 1 for CORRA and 2 for any other index.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The settlement days are negative.</exception>
    public MakeOIS WithSettlementDays(int settlementDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        return this with { SettlementDays = settlementDays };
    }

    /// <summary>
    /// Sets the calendar the settlement days are counted on; the overnight leg's calendar by default.
    /// </summary>
    /// <param name="cal">The calendar; the overnight leg's when <see langword="null"/>, QuantLib's empty
    /// calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithSettlementCalendar(Calendar? cal) => this with { SettlementCalendar = cal };

    /// <summary>Sets an explicit effective date, or <see langword="null"/> for none.</summary>
    /// <param name="effectiveDate">The effective date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeOIS WithEffectiveDate(Date? effectiveDate)
    {
        Date.ThrowIfDefault(effectiveDate, nameof(effectiveDate));
        return this with { EffectiveDate = effectiveDate };
    }

    /// <summary>
    /// Sets an explicit termination date, or <see langword="null"/> for none; a date given clears the swap's tenor.
    /// </summary>
    /// <param name="terminationDate">The termination date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeOIS WithTerminationDate(Date? terminationDate)
    {
        Date.ThrowIfDefault(terminationDate, nameof(terminationDate));
        return this with
        {
            TerminationDate = terminationDate,
            SwapTenor = terminationDate is null ? SwapTenor : new Period(0, TimeUnit.Days),
        };
    }

    /// <summary>Sets both legs' date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public MakeOIS WithRule(DateGenerationRule r) => WithFixedLegRule(r).WithOvernightLegRule(r);

    /// <summary>Sets the fixed leg's date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public MakeOIS WithFixedLegRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { FixedRule = r };
    }

    /// <summary>Sets the overnight leg's date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public MakeOIS WithOvernightLegRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { OvernightRule = r };
    }

    /// <summary>Sets both legs' payment frequency.</summary>
    /// <param name="f">The frequency.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The frequency isn't a member of the enum.</exception>
    public MakeOIS WithPaymentFrequency(Frequency f) =>
        WithFixedLegPaymentFrequency(f).WithOvernightLegPaymentFrequency(f);

    /// <summary>
    /// Sets the fixed leg's payment frequency; <see cref="Frequency.Once"/> makes it a zero-coupon leg.
    /// </summary>
    /// <param name="f">The frequency.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The frequency isn't a member of the enum.</exception>
    public MakeOIS WithFixedLegPaymentFrequency(Frequency f)
    {
        EnumArguments.Check(f, nameof(f));
        return this with { FixedPaymentFrequency = f };
    }

    /// <summary>
    /// Sets the overnight leg's payment frequency; <see cref="Frequency.Once"/> makes it a zero-coupon leg.
    /// </summary>
    /// <param name="f">The frequency.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The frequency isn't a member of the enum.</exception>
    public MakeOIS WithOvernightLegPaymentFrequency(Frequency f)
    {
        EnumArguments.Check(f, nameof(f));
        return this with { OvernightPaymentFrequency = f };
    }

    /// <summary>Sets the payment dates' business-day convention.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeOIS WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets the payment lag, in business days.</summary>
    /// <param name="lag">The lag.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithPaymentLag(int lag) => this with { PaymentLag = lag };

    /// <summary>Sets the payment calendar; each leg's schedule calendar by default.</summary>
    /// <param name="cal">The calendar; each leg's schedule calendar when <see langword="null"/>, QuantLib's empty
    /// calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithPaymentCalendar(Calendar? cal) => this with { PaymentCalendar = cal };

    /// <summary>Sets both legs' calendar.</summary>
    /// <param name="cal">The calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithCalendar(Calendar cal) => WithFixedLegCalendar(cal).WithOvernightLegCalendar(cal);

    /// <summary>Sets the fixed leg's calendar; the index's fixing calendar by default.</summary>
    /// <param name="cal">The calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithFixedLegCalendar(Calendar cal)
    {
        ArgumentNullException.ThrowIfNull(cal);
        return this with { FixedCalendar = cal };
    }

    /// <summary>Sets the overnight leg's calendar; the index's fixing calendar by default.</summary>
    /// <param name="cal">The calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithOvernightLegCalendar(Calendar cal)
    {
        ArgumentNullException.ThrowIfNull(cal);
        return this with { OvernightCalendar = cal };
    }

    /// <summary>Sets both legs' business-day convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeOIS WithConvention(BusinessDayConvention bdc) =>
        WithFixedLegConvention(bdc).WithOvernightLegConvention(bdc);

    /// <summary>Sets the fixed leg's business-day convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeOIS WithFixedLegConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FixedConvention = bdc };
    }

    /// <summary>Sets the overnight leg's business-day convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeOIS WithOvernightLegConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { OvernightConvention = bdc };
    }

    /// <summary>Sets both legs' termination-date convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeOIS WithTerminationDateConvention(BusinessDayConvention bdc) =>
        WithFixedLegTerminationDateConvention(bdc).WithOvernightLegTerminationDateConvention(bdc);

    /// <summary>Sets the fixed leg's termination-date convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeOIS WithFixedLegTerminationDateConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FixedTerminationDateConvention = bdc };
    }

    /// <summary>Sets the overnight leg's termination-date convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeOIS WithOvernightLegTerminationDateConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { OvernightTerminationDateConvention = bdc };
    }

    /// <summary>
    /// Sets both legs' end-of-month flag. By default, before any end-of-month setter is called, both legs and the
    /// maturity follow whether the start date is at month end on the overnight calendar.
    /// </summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithEndOfMonth(bool flag = true) => WithFixedLegEndOfMonth(flag).WithOvernightLegEndOfMonth(flag);

    /// <summary>Sets whether the fixed leg's dates stay at month end.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithFixedLegEndOfMonth(bool flag = true) =>
        this with { FixedEndOfMonth = flag, IsDefaultEOM = false };

    /// <summary>Sets whether the overnight leg's dates stay at month end; it is the maturity's default too.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithOvernightLegEndOfMonth(bool flag = true) =>
        this with { OvernightEndOfMonth = flag, IsDefaultEOM = false };

    /// <summary>Sets whether a maturity computed from a month-end start date is moved to its month's end.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithMaturityEndOfMonth(bool flag = true) =>
        this with { MaturityEndOfMonth = flag, IsDefaultEOM = false };

    /// <summary>Sets the fixed leg's day counter; the index's by default.</summary>
    /// <param name="dc">
    /// The day counter, or <see langword="null"/>, QuantLib's empty day counter, for the index's.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithFixedLegDayCount(DayCounter? dc) => this with { FixedDayCount = dc };

    /// <summary>Sets the overnight leg's spread.</summary>
    /// <param name="sp">The spread.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithOvernightLegSpread(double sp) => this with { OvernightSpread = sp };

    /// <summary>
    /// Sets <see cref="DiscountingSwapEngine"/> on a discount curve, without the settlement date's flows, as the
    /// engine that computes the par rate when no fixed rate is given. With a fixed rate given it changes nothing: the
    /// swap returned holds no engine, where QuantLib's builder attaches it.
    /// </summary>
    /// <param name="discountingTermStructure">The discount curve.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithDiscountingTermStructure(YieldTermStructure discountingTermStructure)
    {
        ArgumentNullException.ThrowIfNull(discountingTermStructure);
        return this with
        {
            Engine = (swap, context) => DiscountingSwapEngine.Calculate(
                swap, context, discountingTermStructure, includeSettlementDateFlows: false),
        };
    }

    /// <summary>
    /// Sets QuantLib's telescopic value dates, which on averaged coupons select the averaging pricer's approximation;
    /// the coupons' value dates are the full daily list either way.
    /// </summary>
    /// <param name="telescopicValueDates">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithTelescopicValueDates(bool telescopicValueDates) =>
        this with { TelescopicValueDates = telescopicValueDates };

    /// <summary>Sets whether the overnight rates are compounded or averaged.</summary>
    /// <param name="averagingMethod">The method.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The method isn't a member of the enum.</exception>
    public MakeOIS WithAveragingMethod(RateAveragingType averagingMethod)
    {
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        return this with { AveragingMethod = averagingMethod };
    }

    /// <summary>Sets the lookback days; the index's fixing days by default.</summary>
    /// <param name="lookbackDays">The lookback.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The lookback is negative.</exception>
    public MakeOIS WithLookbackDays(int lookbackDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lookbackDays);
        return this with { LookbackDays = lookbackDays };
    }

    /// <summary>Sets the lockout days.</summary>
    /// <param name="lockoutDays">The lockout.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The lockout is negative.</exception>
    public MakeOIS WithLockoutDays(int lockoutDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lockoutDays);
        return this with { LockoutDays = lockoutDays };
    }

    /// <summary>Sets whether the observation shift applies.</summary>
    /// <param name="applyObservationShift">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithObservationShift(bool applyObservationShift = true) =>
        this with { ApplyObservationShift = applyObservationShift };

    /// <summary>
    /// Sets the decimal places the overnight coupons' rates are rounded to before their amounts are computed, or
    /// <see langword="null"/> for none.
    /// </summary>
    /// <param name="roundingPrecision">The precision.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside [0, 16].</exception>
    public MakeOIS WithRoundingPrecision(int? roundingPrecision)
    {
        // QuantLib stores any value, which the swap's constructor refuses outside [0, 16]; it is refused here.
        if (roundingPrecision is int precision)
        {
            Rounding.CheckPrecision(precision, nameof(roundingPrecision));
        }

        return this with { RoundingPrecision = roundingPrecision };
    }

    /// <summary>
    /// Sets the engine that computes the par rate when no fixed rate is given: a function of the swap and the context,
    /// such as <c>(s, c) =&gt; DiscountingSwapEngine.Calculate(s, c, curve, includeSettlementDateFlows: false)</c>. It
    /// is never stored on the swap.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithPricingEngine(Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults> engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return this with { Engine = engine };
    }

    /// <summary>The swap: QuantLib's conversion to <c>OvernightIndexedSwap</c>.</summary>
    /// <param name="context">The evaluation context, whose evaluation date the default start date counts from.</param>
    /// <returns>The swap, which holds no engine.</returns>
    /// <exception cref="ArgumentNullException">The context is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// Both an effective date and settlement days are set; a payment frequency has no period; a leg can't be built;
    /// or, with no fixed rate, there is neither an engine nor a forwarding curve, or the par rate is not available.
    /// </exception>
    public OvernightIndexedSwap ToOvernightIndexedSwap(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!(EffectiveDate is null || SettlementDays is null))
        {
            throw new NanoQLException(
                "cannot set both an explicit effective date and settlement days; use one or the other");
        }

        Date startDate;
        if (EffectiveDate is Date effective)
        {
            startDate = effective;
        }
        else
        {
            // settlement days: override if set, else fallback to default by index name
            int settlementDays = SettlementDays ?? OvernightIndex switch
            {
                Sonia => 0,
                Corra => 1,
                _ => 2,
            };

            Date refDate = context.EvaluationDate;

            // settlement days are counted from the actual evaluation
            // date, even when it is not a business day (see issue #753)
            Calendar settlementCalendar = SettlementCalendar ?? OvernightCalendar;
            Date spotDate = settlementCalendar.Advance(refDate, settlementDays, TimeUnit.Days);
            startDate = spotDate + ForwardStart;
            startDate = ForwardStart.Length < 0
                ? OvernightCalendar.Adjust(startDate, BusinessDayConvention.Preceding)
                : OvernightCalendar.Adjust(startDate, BusinessDayConvention.Following);
        }

        bool fixedEndOfMonth, overnightEndOfMonth, maturityEndOfMonth;
        if (IsDefaultEOM)
        {
            fixedEndOfMonth = overnightEndOfMonth = maturityEndOfMonth = OvernightCalendar.IsEndOfMonth(startDate);
        }
        else
        {
            fixedEndOfMonth = FixedEndOfMonth;
            overnightEndOfMonth = OvernightEndOfMonth;
            maturityEndOfMonth = MaturityEndOfMonth ?? OvernightEndOfMonth;
        }

        Date endDate;
        if (TerminationDate is Date termination)
        {
            endDate = termination;
        }
        else
        {
            endDate = startDate + SwapTenor;
            if (maturityEndOfMonth && ScheduleFunctions.AllowsEndOfMonth(SwapTenor)
                && OvernightCalendar.IsEndOfMonth(startDate))
            {
                endDate = OvernightCalendar.EndOfMonth(endDate);
            }
        }

        Frequency fixedPaymentFrequency, overnightPaymentFrequency;
        DateGenerationRule fixedRule, overnightRule;
        if (FixedPaymentFrequency == Frequency.Once || FixedRule == DateGenerationRule.Zero)
        {
            fixedPaymentFrequency = Frequency.Once;
            fixedRule = DateGenerationRule.Zero;
        }
        else
        {
            fixedPaymentFrequency = FixedPaymentFrequency;
            fixedRule = FixedRule;
        }

        if (OvernightPaymentFrequency == Frequency.Once || OvernightRule == DateGenerationRule.Zero)
        {
            overnightPaymentFrequency = Frequency.Once;
            overnightRule = DateGenerationRule.Zero;
        }
        else
        {
            overnightPaymentFrequency = OvernightPaymentFrequency;
            overnightRule = OvernightRule;
        }

        var fixedSchedule = new Schedule(
            startDate,
            endDate,
            PeriodOf(fixedPaymentFrequency),
            FixedCalendar,
            FixedConvention,
            FixedTerminationDateConvention,
            fixedRule,
            fixedEndOfMonth,
            context);

        var overnightSchedule = new Schedule(
            startDate,
            endDate,
            PeriodOf(overnightPaymentFrequency),
            OvernightCalendar,
            OvernightConvention,
            OvernightTerminationDateConvention,
            overnightRule,
            overnightEndOfMonth,
            context);

        double usedFixedRate;
        if (FixedRate is double fixedRate)
        {
            usedFixedRate = fixedRate;
        }
        else
        {
            OvernightIndexedSwap temp = BuildSwap(fixedSchedule, 0.0 /* fixed rate */, overnightSchedule);
            FixedVsFloatingSwapResults results;
            if (Engine is null)
            {
                YieldTermStructure disc = OvernightIndex.ForwardingTermStructure
                    ?? throw new NanoQLException(
                        $"null term structure set to this instance of {OvernightIndex.Name}");
                results = DiscountingSwapEngine.Calculate(temp, context, disc, includeSettlementDateFlows: false);
            }
            else
            {
                results = Engine(temp, context)
                    ?? throw new NanoQLException("the pricing engine returned no results");
            }

            usedFixedRate = results.FairRate ?? throw new NanoQLException("result not available");
        }

        return BuildSwap(fixedSchedule, usedFixedRate, overnightSchedule);
    }

    // Period(Frequency) on a stored frequency: OtherFrequency, which a zero-coupon rule accepts, has no period.
    private static Period PeriodOf(Frequency f) =>
        f == Frequency.OtherFrequency ? throw new NanoQLException("unknown frequency") : new Period(f);

    private OvernightIndexedSwap BuildSwap(Schedule fixedSchedule, double fixedRate, Schedule overnightSchedule) =>
        new(
            Type,
            Nominal,
            fixedSchedule,
            fixedRate,
            FixedDayCount,
            overnightSchedule,
            OvernightIndex,
            OvernightSpread,
            PaymentLag,
            PaymentAdjustment,
            PaymentCalendar,
            TelescopicValueDates,
            AveragingMethod,
            LookbackDays,
            LockoutDays,
            ApplyObservationShift,
            RoundingPrecision);
}
