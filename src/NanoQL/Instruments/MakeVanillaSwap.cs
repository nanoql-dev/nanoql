// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2010, 2014, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Groncki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makevanillaswap.hpp ql/instruments/makevanillaswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A builder of standard market vanilla swaps.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToVanillaSwap"/> builds the swap, in place of QuantLib's conversion operators; it takes the
/// context for the evaluation date the default start date counts from, and for the at-par switch the floating leg
/// reads. A builder has no value equality: its vectors compare by reference.
/// </para>
/// <para>
/// With no fixed rate, the swap is built at its par rate, as QuantLib's builder builds it: a swap at 0% is priced with
/// the engine given, or with <see cref="DiscountingSwapEngine"/> on the index's forwarding curve when none is. The
/// engine computes that rate and is never stored: the swap returned holds no engine.
/// </para>
/// </remarks>
public sealed record MakeVanillaSwap
{
    /// <summary>Initializes a new instance of the <see cref="MakeVanillaSwap"/> record.</summary>
    /// <param name="swapTenor">The swap's tenor.</param>
    /// <param name="index">The floating leg's index, whose calendar, tenor, convention and day counter are the
    /// floating leg's defaults.</param>
    /// <exception cref="ArgumentNullException">The index is <see langword="null"/>.</exception>
    public MakeVanillaSwap(Period swapTenor, IborIndex index)
    {
        // QuantLib reads the index's conventions here, which is undefined for a null index.
        ArgumentNullException.ThrowIfNull(index);
        SwapTenor = swapTenor;
        Index = index;
        FixedCalendar = index.FixingCalendar;
        FloatCalendar = index.FixingCalendar;
        FloatTenor = index.Tenor;
        FloatConvention = index.BusinessDayConvention;
        FloatTerminationDateConvention = index.BusinessDayConvention;
        FloatDayCount = index.DayCounter;
        Provider = index.Provider;
    }

    // A builder taking an outer object's provider: a swap index's, a swaption builder's, a cap builder's.
    internal MakeVanillaSwap(Period swapTenor, IborIndex index, ConventionProvider provider)
        : this(swapTenor, index)
    {
        Provider = provider;
    }

    // The provider the builder takes its conventions from and builds inside: its index's, or an outer object's.
    private ConventionProvider Provider { get; init; }

    private Period SwapTenor { get; init; }

    private IborIndex Index { get; init; }

    private double? FixedRate { get; init; }

    private Period ForwardStart { get; init; } = new(0, TimeUnit.Days);

    private int? SettlementDays { get; init; }

    private Date? EffectiveDate { get; init; }

    private Date? TerminationDate { get; init; }

    private Calendar? FixedCalendar { get; init; }

    private Calendar? FloatCalendar { get; init; }

    private Calendar? SettlementCalendar { get; init; }

    private SwapType Type { get; init; } = SwapType.Payer;

    private double Nominal { get; init; } = 1.0;

    private Period FixedTenor { get; init; } = new(0, TimeUnit.Days);

    private Period FloatTenor { get; init; }

    private BusinessDayConvention FixedConvention { get; init; } = BusinessDayConvention.ModifiedFollowing;

    private BusinessDayConvention FixedTerminationDateConvention { get; init; } =
        BusinessDayConvention.ModifiedFollowing;

    private BusinessDayConvention FloatConvention { get; init; }

    private BusinessDayConvention FloatTerminationDateConvention { get; init; }

    private DateGenerationRule FixedRule { get; init; } = DateGenerationRule.Backward;

    private DateGenerationRule FloatRule { get; init; } = DateGenerationRule.Backward;

    private bool FixedEndOfMonth { get; init; }

    private bool FloatEndOfMonth { get; init; }

    private bool? MaturityEndOfMonth { get; init; }

    private Date? FixedFirstDate { get; init; }

    private Date? FixedNextToLastDate { get; init; }

    private Date? FloatFirstDate { get; init; }

    private Date? FloatNextToLastDate { get; init; }

    private double FloatSpread { get; init; }

    private DayCounter? FixedDayCount { get; init; }

    private DayCounter? FloatDayCount { get; init; }

    private bool? UseIndexedCoupons { get; init; }

    private BusinessDayConvention? PaymentConvention { get; init; }

    private Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? Engine { get; init; }

    /// <summary>Sets whether the fixed rate is received (or paid).</summary>
    /// <param name="flag">Whether the fixed rate is received.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap ReceiveFixed(bool flag = true) =>
        this with { Type = flag ? SwapType.Receiver : SwapType.Payer };

    /// <summary>Sets whether the fixed rate is paid or received.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a member of the enum.</exception>
    public MakeVanillaSwap WithType(SwapType type)
    {
        EnumArguments.Check(type, nameof(type));
        return this with { Type = type };
    }

    /// <summary>Sets the nominal.</summary>
    /// <param name="n">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithNominal(double n) => this with { Nominal = n };

    /// <summary>Sets the fixed rate; with none, the swap is built at its par rate.</summary>
    /// <param name="k">
    /// The fixed rate, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for the par rate.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFixedRate(double? k) => this with { FixedRate = k };

    /// <summary>Sets the forward start, after the spot date.</summary>
    /// <param name="f">The forward start.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithForwardStart(Period f) => this with { ForwardStart = f };

    /// <summary>
    /// Sets the settlement days, counted from the evaluation date on the settlement calendar; by default the index's
    /// value date is the spot date.
    /// </summary>
    /// <param name="settlementDays">
    /// The settlement days, or <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c> and its default, for the
    /// index's value date, which an explicit effective date doesn't clash with.
    /// </param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The settlement days are negative.</exception>
    public MakeVanillaSwap WithSettlementDays(int? settlementDays)
    {
        if (settlementDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(settlementDays));
        }

        return this with { SettlementDays = settlementDays };
    }

    /// <summary>Sets the calendar the settlement days are counted on; the floating leg's calendar by default.</summary>
    /// <param name="cal">The calendar; the floating leg's when <see langword="null"/>, QuantLib's empty
    /// calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithSettlementCalendar(Calendar? cal) => this with { SettlementCalendar = cal };

    /// <summary>Sets an explicit effective date, or <see langword="null"/> for none.</summary>
    /// <param name="effectiveDate">The effective date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeVanillaSwap WithEffectiveDate(Date? effectiveDate)
    {
        Date.ThrowIfDefault(effectiveDate, nameof(effectiveDate));
        return this with { EffectiveDate = effectiveDate };
    }

    /// <summary>
    /// Sets an explicit termination date, or <see langword="null"/> for none. A date given clears the swap's tenor, so
    /// the fixed leg's default tenor is inferred from the swap's dates.
    /// </summary>
    /// <param name="terminationDate">The termination date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeVanillaSwap WithTerminationDate(Date? terminationDate)
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
    public MakeVanillaSwap WithRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { FixedRule = r, FloatRule = r };
    }

    /// <summary>Sets the payment convention; the floating schedule's by default.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeVanillaSwap WithPaymentConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { PaymentConvention = bdc };
    }

    /// <summary>Sets the fixed leg's tenor; by default it follows the index's currency.</summary>
    /// <param name="t">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFixedLegTenor(Period t) => this with { FixedTenor = t };

    /// <summary>Sets the fixed leg's calendar; the index's fixing calendar by default.</summary>
    /// <param name="cal">
    /// The calendar, or <see langword="null"/> for QuantLib's empty calendar, which the conversion uses as QuantLib
    /// does: the fixed schedule and the fixed payment dates are built only where every adjustment is
    /// <see cref="BusinessDayConvention.Unadjusted"/>, and refused otherwise.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFixedLegCalendar(Calendar? cal) => this with { FixedCalendar = cal };

    /// <summary>Sets the fixed leg's business-day convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeVanillaSwap WithFixedLegConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FixedConvention = bdc };
    }

    /// <summary>Sets the fixed leg's termination-date convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeVanillaSwap WithFixedLegTerminationDateConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FixedTerminationDateConvention = bdc };
    }

    /// <summary>Sets the fixed leg's date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public MakeVanillaSwap WithFixedLegRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { FixedRule = r };
    }

    /// <summary>Sets whether the fixed leg's dates stay at month end.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFixedLegEndOfMonth(bool flag = true) => this with { FixedEndOfMonth = flag };

    /// <summary>Sets the fixed leg's first date, for an irregular first period, or <see langword="null"/>.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeVanillaSwap WithFixedLegFirstDate(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { FixedFirstDate = d };
    }

    /// <summary>
    /// Sets the fixed leg's next-to-last date, for an irregular last period, or <see langword="null"/>.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeVanillaSwap WithFixedLegNextToLastDate(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { FixedNextToLastDate = d };
    }

    /// <summary>
    /// Sets the fixed leg's day counter, or <see langword="null"/>, QuantLib's empty day counter, for the default that
    /// follows the index's currency.
    /// </summary>
    /// <param name="dc">The day counter.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFixedLegDayCount(DayCounter? dc) => this with { FixedDayCount = dc };

    /// <summary>Sets the floating leg's tenor; the index's by default.</summary>
    /// <param name="t">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFloatingLegTenor(Period t) => this with { FloatTenor = t };

    /// <summary>Sets the floating leg's calendar; the index's fixing calendar by default.</summary>
    /// <param name="cal">
    /// The calendar, or <see langword="null"/> for QuantLib's empty calendar, which the conversion uses as QuantLib
    /// does: the start date, the maturity and the floating schedule and payment dates are built only where no date is
    /// adjusted or moved by business days on it, and refused otherwise.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFloatingLegCalendar(Calendar? cal) => this with { FloatCalendar = cal };

    /// <summary>Sets the floating leg's business-day convention; the index's by default.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeVanillaSwap WithFloatingLegConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FloatConvention = bdc };
    }

    /// <summary>Sets the floating leg's termination-date convention; the index's convention by default.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeVanillaSwap WithFloatingLegTerminationDateConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FloatTerminationDateConvention = bdc };
    }

    /// <summary>Sets the floating leg's date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public MakeVanillaSwap WithFloatingLegRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { FloatRule = r };
    }

    /// <summary>
    /// Sets whether the floating leg's dates stay at month end; it is the maturity's default too.
    /// </summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFloatingLegEndOfMonth(bool flag = true) => this with { FloatEndOfMonth = flag };

    /// <summary>
    /// Sets whether a maturity computed from a month-end start date is moved to its month's end; the floating leg's
    /// end-of-month flag by default.
    /// </summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithMaturityEndOfMonth(bool flag = true) => this with { MaturityEndOfMonth = flag };

    /// <summary>Sets the floating leg's first date, for an irregular first period, or <see langword="null"/>.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeVanillaSwap WithFloatingLegFirstDate(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { FloatFirstDate = d };
    }

    /// <summary>
    /// Sets the floating leg's next-to-last date, for an irregular last period, or <see langword="null"/>.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeVanillaSwap WithFloatingLegNextToLastDate(Date? d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return this with { FloatNextToLastDate = d };
    }

    /// <summary>
    /// Sets the floating coupons' day counter, or <see langword="null"/>, QuantLib's empty day counter, for the
    /// index's; the index's by default.
    /// </summary>
    /// <param name="dc">The day counter.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFloatingLegDayCount(DayCounter? dc) => this with { FloatDayCount = dc };

    /// <summary>Sets the floating leg's spread.</summary>
    /// <param name="sp">The spread.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithFloatingLegSpread(double sp) => this with { FloatSpread = sp };

    /// <summary>
    /// Sets <see cref="DiscountingSwapEngine"/> on a discount curve, without the settlement date's flows, as the
    /// engine that computes the par rate when no fixed rate is given. With a fixed rate given it changes nothing: the
    /// swap returned holds no engine, where QuantLib's builder attaches it.
    /// </summary>
    /// <param name="discountCurve">
    /// The discount curve, or <see langword="null"/>, QuantLib's empty handle: the engine then refuses to price, so a
    /// build at the par rate is refused, and a build with a fixed rate isn't.
    /// </param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithDiscountingTermStructure(YieldTermStructure? discountCurve) =>
        this with
        {
            Engine = (swap, context) => discountCurve is null
                ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
                : DiscountingSwapEngine.Calculate(swap, context, discountCurve, includeSettlementDateFlows: false),
        };

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
    public MakeVanillaSwap WithPricingEngine(
        Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? engine) =>
        this with { Engine = engine };

    /// <summary>
    /// Sets whether the floating coupons use indexed coupons rather than par coupons; <see langword="null"/> for the
    /// context's switch, the default.
    /// </summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithIndexedCoupons(bool? b = true) => this with { UseIndexedCoupons = b };

    /// <summary>Sets whether the floating coupons use par coupons rather than indexed coupons.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeVanillaSwap WithAtParCoupons(bool b = true) => this with { UseIndexedCoupons = !b };

    /// <summary>The swap: QuantLib's conversion to <c>VanillaSwap</c>.</summary>
    /// <param name="context">The evaluation context: the default start date counts from its evaluation date, and the
    /// floating leg reads its at-par switch when no coupon choice is given.</param>
    /// <returns>The swap, which holds no engine.</returns>
    /// <exception cref="ArgumentNullException">The context is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// Both an effective date and settlement days are set; the fixed leg's default tenor or day counter is unknown for
    /// the index's currency; a date is needed that QuantLib's empty calendar can't give; or, with no fixed rate, there
    /// is neither an engine nor a forwarding curve, the engine has no discount curve, or the par rate is not
    /// available.
    /// </exception>
    public VanillaSwap ToVanillaSwap(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built inside the builder's provider, so what the build constructs takes its conventions.
        using IDisposable scope = ConventionProvider.Use(Provider);

        if (!(EffectiveDate is null || SettlementDays is null))
        {
            throw new NanoQLException(
                "cannot set both an explicit effective date and settlement days; use one or the other");
        }

        // QuantLib's empty calendars, null here, are used unchecked, as QuantLib uses them
        Calendar floatCalendar = FloatCalendar ?? EmptyCalendar.Instance;
        Date startDate;
        if (EffectiveDate is Date effective)
        {
            startDate = effective;
        }
        else
        {
            Date refDate = context.EvaluationDate;

            // use index valueDate interface wherever possible to estimate spot date.
            // Unless we pass an explicit settlementDays_ which does not match the index-defined number of fixing days.
            Date spotDate;
            if (SettlementDays is not int settlementDays)
            {
                // the spot date is defined by the index, so the reference
                // date must be adjusted on the index fixing calendar (not the
                // float/payment calendar) to keep the pre-adjust consistent
                // with valueDate's own fixing-calendar advance.
                refDate = Index.FixingCalendar.Adjust(refDate);
                spotDate = Index.ValueDate(refDate);
            }
            else
            {
                // settlement days are counted from the actual evaluation
                // date, even when it is not a business day (see issue #753)
                Calendar settlementCalendar = SettlementCalendar ?? floatCalendar;
                spotDate = settlementCalendar.Advance(refDate, settlementDays, TimeUnit.Days);
            }

            startDate = spotDate + ForwardStart;
            if (ForwardStart.Length < 0)
            {
                startDate = floatCalendar.Adjust(startDate, BusinessDayConvention.Preceding);
            }
            else if (ForwardStart.Length > 0)
            {
                startDate = floatCalendar.Adjust(startDate, BusinessDayConvention.Following);
            }

            // no explicit date adjustment needed for forwardStart_.length()==0
            // (already handled by spotDate arithmetic above)
        }

        Date endDate;
        if (TerminationDate is Date termination)
        {
            endDate = termination;
        }
        else
        {
            endDate = startDate + SwapTenor;
            bool maturityEndOfMonth = MaturityEndOfMonth ?? FloatEndOfMonth;
            if (maturityEndOfMonth && ScheduleFunctions.AllowsEndOfMonth(SwapTenor)
                && floatCalendar.IsEndOfMonth(startDate))
            {
                endDate = floatCalendar.EndOfMonth(endDate);
            }
        }

        Currency? curr = Index.Currency;
        Period fixedTenor;
        if (FixedTenor != new Period(0, TimeUnit.Days))
        {
            fixedTenor = FixedTenor;
        }
        else
        {
            // When swapTenor_ was cleared by withTerminationDate(),
            // use the actual swap length for currency-dependent inference.
            Period tenor = SwapTenor;
            if (tenor == new Period(0, TimeUnit.Days) && endDate > startDate)
            {
                // approximate months = days * 12/365, rounded (182 = 365/2)
                int months = ((12 * (endDate - startDate)) + 182) / 365;
                tenor = new Period(months, TimeUnit.Months);
            }

            if (curr == new EURCurrency()
                || curr == new USDCurrency()
                || curr == new CHFCurrency()
                || curr == new SEKCurrency()
                || (curr == new GBPCurrency() && tenor <= new Period(1, TimeUnit.Years)))
            {
                fixedTenor = new Period(1, TimeUnit.Years);
            }
            else if ((curr == new GBPCurrency() && tenor > new Period(1, TimeUnit.Years))
                || curr == new JPYCurrency()
                || (curr == new AUDCurrency() && tenor >= new Period(4, TimeUnit.Years)))
            {
                fixedTenor = new Period(6, TimeUnit.Months);
            }
            else if (curr == new HKDCurrency() || (curr == new AUDCurrency() && tenor < new Period(4, TimeUnit.Years)))
            {
                fixedTenor = new Period(3, TimeUnit.Months);
            }
            else
            {
                throw new NanoQLException($"unknown fixed leg default tenor for {Currency.Format(curr)}");
            }
        }

        var fixedSchedule = new Schedule(
            startDate,
            endDate,
            fixedTenor,
            FixedCalendar,
            FixedConvention,
            FixedTerminationDateConvention,
            FixedRule,
            FixedEndOfMonth,
            context,
            FixedFirstDate,
            FixedNextToLastDate);

        var floatSchedule = new Schedule(
            startDate,
            endDate,
            FloatTenor,
            FloatCalendar,
            FloatConvention,
            FloatTerminationDateConvention,
            FloatRule,
            FloatEndOfMonth,
            context,
            FloatFirstDate,
            FloatNextToLastDate);

        DayCounter fixedDayCount;
        if (FixedDayCount is not null)
        {
            fixedDayCount = FixedDayCount;
        }
        else if (curr == new USDCurrency())
        {
            fixedDayCount = new Actual360();
        }
        else if (curr == new EURCurrency() || curr == new CHFCurrency() || curr == new SEKCurrency())
        {
            fixedDayCount = new Thirty360(Thirty360Convention.BondBasis);
        }
        else if (curr == new GBPCurrency() || curr == new JPYCurrency() || curr == new AUDCurrency()
            || curr == new HKDCurrency() || curr == new THBCurrency())
        {
            fixedDayCount = new Actual365Fixed();
        }
        else
        {
            throw new NanoQLException($"unknown fixed leg day counter for {Currency.Format(curr)}");
        }

        double usedFixedRate;
        if (FixedRate is double fixedRate)
        {
            usedFixedRate = fixedRate;
        }
        else
        {
            var temp = new VanillaSwap(
                Type,
                100.00,
                fixedSchedule,
                0.0, // fixed rate
                fixedDayCount,
                floatSchedule,
                Index,
                FloatSpread,
                FloatDayCount,
                context,
                PaymentConvention,
                UseIndexedCoupons);
            FixedVsFloatingSwapResults results;
            if (Engine is null)
            {
                YieldTermStructure disc = Index.ForwardingTermStructure
                    ?? throw new NanoQLException($"null term structure set to this instance of {Index.Name}");
                results = DiscountingSwapEngine.Calculate(temp, context, disc, includeSettlementDateFlows: false);
            }
            else
            {
                results = Engine(temp, context)
                    ?? throw new NanoQLException("the pricing engine returned no results");
            }

            usedFixedRate = results.FairRate ?? throw new NanoQLException("result not available");
        }

        return new VanillaSwap(
            Type,
            Nominal,
            fixedSchedule,
            usedFixedRate,
            fixedDayCount,
            floatSchedule,
            Index,
            FloatSpread,
            FloatDayCount,
            context,
            PaymentConvention,
            UseIndexedCoupons);
    }
}
