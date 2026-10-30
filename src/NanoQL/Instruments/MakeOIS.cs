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
/// An option the builder isn't given that QuantLib's builder fixes takes the builder's provider's default for the
/// index's class (<see cref="Defaults"/>), read when the swap is built: QuantLib's values with nothing configured. Its
/// settlement days test the index's type, as QuantLib's builder does: 0 for <see cref="Sonia"/>, 1 for
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
        Provider = overnightIndex.Provider;
    }

    // A builder taking an outer object's provider: an overnight-indexed swap index's, a swaption builder's.
    internal MakeOIS(Period swapTenor, OvernightIndex overnightIndex, ConventionProvider provider)
        : this(swapTenor, overnightIndex)
    {
        Provider = provider;
    }

    // The provider the builder takes its conventions from and builds inside: its index's, or an outer object's.
    private ConventionProvider Provider { get; init; }

    private Period SwapTenor { get; init; }

    private OvernightIndex OvernightIndex { get; init; }

    private double? FixedRate { get; init; }

    private Period ForwardStart { get; init; } = new(0, TimeUnit.Days);

    private int? SettlementDays { get; init; }

    private Date? EffectiveDate { get; init; }

    private Date? TerminationDate { get; init; }

    private Calendar? FixedCalendar { get; init; }

    private Calendar? OvernightCalendar { get; init; }

    private Calendar? SettlementCalendar { get; init; }

    private Frequency? FixedPaymentFrequency { get; init; }

    private Frequency? OvernightPaymentFrequency { get; init; }

    private Calendar? PaymentCalendar { get; init; }

    private BusinessDayConvention? PaymentAdjustment { get; init; }

    private int? PaymentLag { get; init; }

    private BusinessDayConvention? FixedConvention { get; init; }

    private BusinessDayConvention? FixedTerminationDateConvention { get; init; }

    private BusinessDayConvention? OvernightConvention { get; init; }

    private BusinessDayConvention? OvernightTerminationDateConvention { get; init; }

    private DateGenerationRule? FixedRule { get; init; }

    private DateGenerationRule? OvernightRule { get; init; }

    private bool FixedEndOfMonth { get; init; }

    private bool OvernightEndOfMonth { get; init; }

    private bool IsDefaultEOM { get; init; } = true;

    private bool? MaturityEndOfMonth { get; init; }

    private SwapType Type { get; init; } = SwapType.Payer;

    private double Nominal { get; init; } = 1.0;

    private double OvernightSpread { get; init; }

    private DayCounter? FixedDayCount { get; init; }

    private Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? Engine { get; init; }

    private bool? TelescopicValueDates { get; init; }

    private RateAveragingType? AveragingMethod { get; init; }

    private int? LookbackDays { get; init; }

    private int? LockoutDays { get; init; }

    private bool? ApplyObservationShift { get; init; }

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
    /// Sets the settlement days, counted from the evaluation date on the settlement calendar; by default the
    /// provider's for the index's class, QuantLib's 0 for SONIA, 1 for CORRA and 2 for any other index.
    /// </summary>
    /// <param name="settlementDays">
    /// The settlement days, or <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c> and its default, for the
    /// index's default, which an explicit effective date doesn't clash with.
    /// </param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The settlement days are negative.</exception>
    public MakeOIS WithSettlementDays(int? settlementDays)
    {
        if (settlementDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(settlementDays));
        }

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
    /// <param name="cal">
    /// The calendar, or <see langword="null"/> for QuantLib's empty calendar, which the conversion uses as the
    /// fixed-leg and overnight-leg setters say.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithCalendar(Calendar? cal) => WithFixedLegCalendar(cal).WithOvernightLegCalendar(cal);

    /// <summary>Sets the fixed leg's calendar; the index's fixing calendar by default.</summary>
    /// <param name="cal">
    /// The calendar, or <see langword="null"/> for QuantLib's empty calendar, which the conversion uses as QuantLib
    /// does: the fixed schedule and, with no payment calendar, the fixed payment dates are built only where every
    /// adjustment is <see cref="BusinessDayConvention.Unadjusted"/>, and refused otherwise.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithFixedLegCalendar(Calendar? cal) => this with { FixedCalendar = cal };

    /// <summary>Sets the overnight leg's calendar; the index's fixing calendar by default.</summary>
    /// <param name="cal">
    /// The calendar, or <see langword="null"/> for QuantLib's empty calendar, which the conversion uses as QuantLib
    /// does: the start date, the end-of-month default, the maturity and the overnight schedule are built only where
    /// no date is adjusted or moved by business days on it, and refused otherwise; the overnight coupons are then paid
    /// on the payment calendar, or on <see cref="WeekendsOnly"/> when none is set.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithOvernightLegCalendar(Calendar? cal) => this with { OvernightCalendar = cal };

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
    /// <param name="discountingTermStructure">
    /// The discount curve, or <see langword="null"/>, QuantLib's empty handle: the engine then refuses to price, so a
    /// build at the par rate is refused, and a build with a fixed rate isn't.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithDiscountingTermStructure(YieldTermStructure? discountingTermStructure) =>
        this with
        {
            Engine = (swap, context) => discountingTermStructure is null
                ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
                : DiscountingSwapEngine.Calculate(
                    swap, context, discountingTermStructure, includeSettlementDateFlows: false),
        };

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
    /// <param name="lookbackDays">
    /// The lookback, or <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c> and its default, for none: the
    /// coupons then fix by the index's fixing days.
    /// </param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The lookback is negative.</exception>
    public MakeOIS WithLookbackDays(int? lookbackDays)
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
    /// <see langword="null"/> for none. Any value is stored, as QuantLib stores it: the swap's constructor refuses one
    /// outside [0, 16] when the builder converts.
    /// </summary>
    /// <param name="roundingPrecision">The precision.</param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithRoundingPrecision(int? roundingPrecision) => this with { RoundingPrecision = roundingPrecision };

    /// <summary>
    /// Sets the engine that computes the par rate when no fixed rate is given: a function of the swap and the context,
    /// such as <c>(s, c) =&gt; DiscountingSwapEngine.Calculate(s, c, curve, includeSettlementDateFlows: false)</c>. It
    /// is never stored on the swap.
    /// </summary>
    /// <param name="engine">
    /// The engine, or <see langword="null"/>, QuantLib's null engine, for the default: a
    /// <see cref="DiscountingSwapEngine"/> on the index's forwarding curve.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeOIS WithPricingEngine(
        Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? engine) =>
        this with { Engine = engine };

    /// <summary>The swap: QuantLib's conversion to <c>OvernightIndexedSwap</c>.</summary>
    /// <param name="context">The evaluation context, whose evaluation date the default start date counts from.</param>
    /// <returns>The swap, which holds no engine.</returns>
    /// <exception cref="ArgumentNullException">The context is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The provider has no defaults for the index (a set without QuantLib's
    /// presets).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rounding precision is outside [0, 16].</exception>
    /// <exception cref="NanoQLException">
    /// Both an effective date and settlement days are set; a date is needed that QuantLib's empty calendar can't give;
    /// a payment frequency has no period; a leg can't be built; or, with no fixed rate, there is neither an engine nor
    /// a forwarding curve, the engine has no discount curve, or the par rate is not available.
    /// </exception>
    public OvernightIndexedSwap ToOvernightIndexedSwap(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built inside the builder's provider, so what the build constructs takes its conventions.
        using IDisposable scope = ConventionProvider.Use(Provider);

        // the provider's defaults, read once an option needs one
        OisDefaults? defaults = null;
        OisDefaults Defaults() => defaults ??= MakeOIS.Defaults(OvernightIndex, Provider);

        // QuantLib's empty calendar, null here, is used unchecked, as QuantLib uses it
        Calendar overnightCalendar = OvernightCalendar ?? EmptyCalendar.Instance;

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
            int settlementDays = SettlementDays ?? Defaults().SettlementDays;

            Date refDate = context.EvaluationDate;

            // settlement days are counted from the actual evaluation
            // date, even when it is not a business day (see issue #753)
            Calendar settlementCalendar = SettlementCalendar ?? overnightCalendar;
            Date spotDate = settlementCalendar.Advance(refDate, settlementDays, TimeUnit.Days);
            startDate = spotDate + ForwardStart;
            startDate = ForwardStart.Length < 0
                ? overnightCalendar.Adjust(startDate, BusinessDayConvention.Preceding)
                : overnightCalendar.Adjust(startDate, BusinessDayConvention.Following);
        }

        bool fixedEndOfMonth, overnightEndOfMonth, maturityEndOfMonth;
        if (IsDefaultEOM)
        {
            fixedEndOfMonth = overnightEndOfMonth = maturityEndOfMonth = overnightCalendar.IsEndOfMonth(startDate);
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
                && overnightCalendar.IsEndOfMonth(startDate))
            {
                endDate = overnightCalendar.EndOfMonth(endDate);
            }
        }

        Frequency fixedPaymentFrequency = FixedPaymentFrequency ?? Defaults().FixedLegPaymentFrequency;
        Frequency overnightPaymentFrequency = OvernightPaymentFrequency ?? Defaults().OvernightLegPaymentFrequency;
        DateGenerationRule fixedRule = FixedRule ?? Defaults().FixedLegRule;
        DateGenerationRule overnightRule = OvernightRule ?? Defaults().OvernightLegRule;
        if (fixedPaymentFrequency == Frequency.Once || fixedRule == DateGenerationRule.Zero)
        {
            fixedPaymentFrequency = Frequency.Once;
            fixedRule = DateGenerationRule.Zero;
        }

        if (overnightPaymentFrequency == Frequency.Once || overnightRule == DateGenerationRule.Zero)
        {
            overnightPaymentFrequency = Frequency.Once;
            overnightRule = DateGenerationRule.Zero;
        }

        var fixedSchedule = new Schedule(
            startDate,
            endDate,
            PeriodOf(fixedPaymentFrequency),
            FixedCalendar,
            FixedConvention ?? Defaults().FixedLegConvention,
            FixedTerminationDateConvention ?? Defaults().FixedLegTerminationDateConvention,
            fixedRule,
            fixedEndOfMonth,
            context);

        var overnightSchedule = new Schedule(
            startDate,
            endDate,
            PeriodOf(overnightPaymentFrequency),
            OvernightCalendar,
            OvernightConvention ?? Defaults().OvernightLegConvention,
            OvernightTerminationDateConvention ?? Defaults().OvernightLegTerminationDateConvention,
            overnightRule,
            overnightEndOfMonth,
            context);

        int paymentLag = PaymentLag ?? Defaults().PaymentLag;
        BusinessDayConvention paymentAdjustment = PaymentAdjustment ?? Defaults().PaymentAdjustment;
        bool telescopicValueDates = TelescopicValueDates ?? Defaults().TelescopicValueDates;
        RateAveragingType averagingMethod = AveragingMethod ?? Defaults().AveragingMethod;
        int lockoutDays = LockoutDays ?? Defaults().LockoutDays;
        bool applyObservationShift = ApplyObservationShift ?? Defaults().ApplyObservationShift;
        OvernightIndexedSwap BuildSwap(double fixedRate) =>
            new(
                Type,
                Nominal,
                fixedSchedule,
                fixedRate,
                FixedDayCount,
                overnightSchedule,
                OvernightIndex,
                OvernightSpread,
                paymentLag,
                paymentAdjustment,
                PaymentCalendar,
                telescopicValueDates,
                averagingMethod,
                LookbackDays,
                lockoutDays,
                applyObservationShift,
                RoundingPrecision);

        double usedFixedRate;
        if (FixedRate is double fixedRate)
        {
            usedFixedRate = fixedRate;
        }
        else
        {
            OvernightIndexedSwap temp = BuildSwap(0.0 /* fixed rate */);
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

        return BuildSwap(usedFixedRate);
    }

    /// <summary>The defaults a builder takes under a provider for the options it isn't given, on an overnight
    /// index.</summary>
    /// <param name="overnightIndex">The index, whose class QuantLib's settlement days test.</param>
    /// <param name="provider">The provider, an index's, which a builder on the index takes.</param>
    /// <returns>The provider's defaults for the index's class; QuantLib's under
    /// <see cref="ConventionProvider.QuantLib"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The provider has no defaults for the index (a set without QuantLib's
    /// presets).</exception>
    public static OisDefaults Defaults(OvernightIndex overnightIndex, ConventionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(overnightIndex);
        ArgumentNullException.ThrowIfNull(provider);
        return ConventionProvider.ReadDefaults(
            provider,
            p => p.ResolveOisDefaults(overnightIndex.GetType().Name, () => QuantLibDefaults(overnightIndex)),
            $"overnight-indexed swaps on {overnightIndex.Name}");
    }

    // QuantLib's settlement days by the index's class, and the builder's constants.
    private static OisDefaults QuantLibDefaults(OvernightIndex overnightIndex) =>
        new(
            overnightIndex switch
            {
                Sonia => 0,
                Corra => 1,
                _ => 2,
            },
            Frequency.Annual,
            Frequency.Annual,
            BusinessDayConvention.Following,
            0,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            DateGenerationRule.Backward,
            false,
            RateAveragingType.Compound,
            0,
            false);

    // Period(Frequency) on a stored frequency: OtherFrequency, which a zero-coupon rule accepts, has no period.
    private static Period PeriodOf(Frequency f) =>
        f == Frequency.OtherFrequency ? throw new NanoQLException("unknown frequency") : new Period(f);
}
