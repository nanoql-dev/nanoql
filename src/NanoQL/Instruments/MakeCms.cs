// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makecms.hpp ql/instruments/makecms.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A builder of standard market constant maturity swaps: a CMS leg against an Ibor leg.</summary>
/// <remarks>
/// <para>
/// An immutable builder, as <see cref="MakeVanillaSwap"/>: each method returns a modified copy, and
/// <see cref="ToSwap"/> builds the swap, in place of QuantLib's conversion operators; it takes the context for the
/// evaluation date the default start date counts from, and for the at-par switch the Ibor leg reads. A builder has no
/// value equality.
/// </para>
/// <para>
/// The engine, <see cref="DiscountingSwapEngine"/> on the swap index's forwarding curve unless
/// <see cref="WithDiscountingTermStructure"/> sets another curve, prices the at-the-money spread
/// (<see cref="WithAtmSpread"/>) and is never stored: the swap returned holds no engine, where QuantLib's builder
/// attaches it. QuantLib's two constructors taking the Ibor spread and the forward start, deprecated in 1.44, aren't
/// ported: <see cref="WithIborSpread"/> and <see cref="WithForwardStart"/> set them.
/// </para>
/// <para>
/// An option the builder isn't given that QuantLib's builder fixes takes the builder's provider's default
/// (<see cref="Defaults"/>), read when the swap is built: QuantLib's values with nothing configured.
/// </para>
/// </remarks>
public sealed record MakeCms
{
    /// <summary>Initializes a new instance of the <see cref="MakeCms"/> record.</summary>
    /// <param name="swapTenor">The swap's tenor.</param>
    /// <param name="swapIndex">The CMS leg's swap index.</param>
    /// <param name="iborIndex">The Ibor leg's index; the swap index's own when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">The swap index is <see langword="null"/>.</exception>
    public MakeCms(Period swapTenor, SwapIndex swapIndex, IborIndex? iborIndex = null)
    {
        // QuantLib reads the indexes' conventions here, which is undefined for a null swap index.
        ArgumentNullException.ThrowIfNull(swapIndex);
        Provider = swapIndex.Provider;
        SwapTenor = swapTenor;
        SwapIndex = swapIndex;
        IborIndex = iborIndex ?? swapIndex.IborIndex;
        CmsCalendar = swapIndex.FixingCalendar;
        FloatCalendar = IborIndex.FixingCalendar;
        FloatTenor = IborIndex.Tenor;
        FloatConvention = IborIndex.BusinessDayConvention;
        FloatTerminationDateConvention = IborIndex.BusinessDayConvention;
        FloatDayCount = IborIndex.DayCounter;
        YieldTermStructure? forwarding = swapIndex.ForwardingTermStructure;
        Engine = (swap, context) => forwarding is null
            ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
            : DiscountingSwapEngine.Calculate(swap, context, forwarding);
    }

    // The provider the builder takes its conventions from and builds inside: its swap index's.
    private ConventionProvider Provider { get; init; }

    private Period SwapTenor { get; init; }

    private SwapIndex SwapIndex { get; init; }

    private IborIndex IborIndex { get; init; }

    private double IborSpread { get; init; }

    private bool UseAtmSpread { get; init; }

    private Period ForwardStart { get; init; } = new(0, TimeUnit.Days);

    private double CmsSpread { get; init; }

    private double CmsGearing { get; init; } = 1.0;

    private double? CmsCap { get; init; }

    private double? CmsFloor { get; init; }

    private Date? EffectiveDate { get; init; }

    private Calendar CmsCalendar { get; init; }

    private Calendar FloatCalendar { get; init; }

    private bool PayCms { get; init; } = true;

    private double Nominal { get; init; } = 1.0;

    private Period? CmsTenor { get; init; }

    private Period FloatTenor { get; init; }

    private BusinessDayConvention? CmsConvention { get; init; }

    private BusinessDayConvention? CmsTerminationDateConvention { get; init; }

    private BusinessDayConvention FloatConvention { get; init; }

    private BusinessDayConvention FloatTerminationDateConvention { get; init; }

    private DateGenerationRule? CmsRule { get; init; }

    private DateGenerationRule? FloatRule { get; init; }

    private bool? CmsEndOfMonth { get; init; }

    private bool? FloatEndOfMonth { get; init; }

    private Date? CmsFirstDate { get; init; }

    private Date? CmsNextToLastDate { get; init; }

    private Date? FloatFirstDate { get; init; }

    private Date? FloatNextToLastDate { get; init; }

    private DayCounter? CmsDayCount { get; init; }

    private DayCounter FloatDayCount { get; init; }

    private Func<Swap, EvaluationContext, SwapResults> Engine { get; init; }

    private CmsCouponPricer? CouponPricer { get; init; }

    /// <summary>Sets whether the CMS leg is received (or paid).</summary>
    /// <param name="flag">Whether the CMS leg is received.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms ReceiveCms(bool flag = true) => this with { PayCms = !flag };

    /// <summary>Sets the nominal of both legs.</summary>
    /// <param name="n">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithNominal(double n) => this with { Nominal = n };

    /// <summary>Sets the Ibor leg's spread.</summary>
    /// <param name="s">The spread.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithIborSpread(double s) => this with { IborSpread = s };

    /// <summary>Sets the forward start from the spot date.</summary>
    /// <param name="f">The forward start.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithForwardStart(Period f) => this with { ForwardStart = f };

    /// <summary>Sets the effective date, in place of the spot date and the forward start.</summary>
    /// <param name="effectiveDate">The effective date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithEffectiveDate(Date? effectiveDate) => this with { EffectiveDate = effectiveDate };

    /// <summary>Sets the CMS leg's tenor.</summary>
    /// <param name="t">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithCmsLegTenor(Period t) => this with { CmsTenor = t };

    /// <summary>Sets the CMS leg's calendar.</summary>
    /// <param name="cal">The calendar.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException">The calendar is <see langword="null"/>.</exception>
    public MakeCms WithCmsLegCalendar(Calendar cal)
    {
        ArgumentNullException.ThrowIfNull(cal);
        return this with { CmsCalendar = cal };
    }

    /// <summary>Sets the CMS leg's business-day convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of its type.</exception>
    public MakeCms WithCmsLegConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { CmsConvention = bdc };
    }

    /// <summary>Sets the CMS leg's termination date convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of its type.</exception>
    public MakeCms WithCmsLegTerminationDateConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { CmsTerminationDateConvention = bdc };
    }

    /// <summary>Sets the CMS leg's date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of its type.</exception>
    public MakeCms WithCmsLegRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { CmsRule = r };
    }

    /// <summary>Sets whether the CMS leg's dates stay at month end.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithCmsLegEndOfMonth(bool flag = true) => this with { CmsEndOfMonth = flag };

    /// <summary>Sets the CMS leg's first date.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithCmsLegFirstDate(Date? d) => this with { CmsFirstDate = d };

    /// <summary>Sets the CMS leg's next-to-last date.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithCmsLegNextToLastDate(Date? d) => this with { CmsNextToLastDate = d };

    /// <summary>Sets the CMS leg's day counter.</summary>
    /// <param name="dc">The day counter.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException">The day counter is <see langword="null"/>.</exception>
    public MakeCms WithCmsLegDayCount(DayCounter dc)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return this with { CmsDayCount = dc };
    }

    /// <summary>Sets the Ibor leg's tenor.</summary>
    /// <param name="t">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithFloatingLegTenor(Period t) => this with { FloatTenor = t };

    /// <summary>Sets the Ibor leg's calendar.</summary>
    /// <param name="cal">The calendar.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException">The calendar is <see langword="null"/>.</exception>
    public MakeCms WithFloatingLegCalendar(Calendar cal)
    {
        ArgumentNullException.ThrowIfNull(cal);
        return this with { FloatCalendar = cal };
    }

    /// <summary>Sets the Ibor leg's business-day convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of its type.</exception>
    public MakeCms WithFloatingLegConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FloatConvention = bdc };
    }

    /// <summary>Sets the Ibor leg's termination date convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of its type.</exception>
    public MakeCms WithFloatingLegTerminationDateConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FloatTerminationDateConvention = bdc };
    }

    /// <summary>Sets the Ibor leg's date-generation rule.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of its type.</exception>
    public MakeCms WithFloatingLegRule(DateGenerationRule r)
    {
        EnumArguments.Check(r, nameof(r));
        return this with { FloatRule = r };
    }

    /// <summary>Sets whether the Ibor leg's dates stay at month end.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithFloatingLegEndOfMonth(bool flag = true) => this with { FloatEndOfMonth = flag };

    /// <summary>Sets the Ibor leg's first date.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithFloatingLegFirstDate(Date? d) => this with { FloatFirstDate = d };

    /// <summary>Sets the Ibor leg's next-to-last date.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithFloatingLegNextToLastDate(Date? d) => this with { FloatNextToLastDate = d };

    /// <summary>Sets the Ibor leg's day counter.</summary>
    /// <param name="dc">The day counter.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException">The day counter is <see langword="null"/>.</exception>
    public MakeCms WithFloatingLegDayCount(DayCounter dc)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return this with { FloatDayCount = dc };
    }

    /// <summary>Sets whether the Ibor leg's spread is the one at which the swap is worth 0, in place of the given
    /// one.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithAtmSpread(bool flag = true) => this with { UseAtmSpread = flag };

    /// <summary>Sets the engine's discounting curve, which prices the at-the-money spread.</summary>
    /// <param name="discountingTermStructure">The curve, or <see langword="null"/>, QuantLib's empty handle: the engine
    /// then refuses to price.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithDiscountingTermStructure(YieldTermStructure? discountingTermStructure) =>
        this with
        {
            Engine = (swap, context) => discountingTermStructure is null
                ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
                : DiscountingSwapEngine.Calculate(swap, context, discountingTermStructure),
        };

    /// <summary>Sets the CMS leg's coupon pricer.</summary>
    /// <param name="couponPricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeCms WithCmsCouponPricer(CmsCouponPricer? couponPricer) => this with { CouponPricer = couponPricer };

    /// <summary>The swap: QuantLib's conversion to <c>Swap</c>, the CMS leg first when it is paid.</summary>
    /// <param name="context">The evaluation context: the default start date counts from its evaluation date, the Ibor
    /// leg reads its at-par switch, and the at-the-money spread is priced under it.</param>
    /// <returns>The swap, which holds no engine.</returns>
    /// <exception cref="ArgumentNullException">The context is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// With the at-the-money spread, an index has no forwarding curve, no CMS coupon pricer is set, or the engine can't
    /// price the swap.
    /// </exception>
    public Swap ToSwap(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built inside the builder's provider, so what the build constructs takes its conventions.
        using IDisposable scope = ConventionProvider.Use(Provider);

        // the provider's defaults, read once an option needs one
        CmsDefaults? defaults = null;
        CmsDefaults Defaults() => defaults ??= MakeCms.Defaults(Provider);
        BusinessDayConvention cmsConvention = CmsConvention ?? Defaults().CmsLegConvention;

        Date startDate;
        if (EffectiveDate is Date effective)
        {
            startDate = effective;
        }
        else
        {
            int fixingDays = IborIndex.FixingDays;
            Date refDate = context.EvaluationDate;

            // if the evaluation date is not a business day
            // then move to the next business day
            refDate = FloatCalendar.Adjust(refDate);
            Date spotDate = FloatCalendar.Advance(refDate, fixingDays, TimeUnit.Days);
            startDate = spotDate + ForwardStart;
        }

        Date terminationDate = startDate + SwapTenor;

        var cmsSchedule = new Schedule(
            startDate,
            terminationDate,
            CmsTenor ?? Defaults().CmsLegTenor,
            CmsCalendar,
            cmsConvention,
            CmsTerminationDateConvention ?? Defaults().CmsLegTerminationDateConvention,
            CmsRule ?? Defaults().CmsLegRule,
            CmsEndOfMonth ?? Defaults().CmsLegEndOfMonth,
            context,
            CmsFirstDate,
            CmsNextToLastDate);

        var floatSchedule = new Schedule(
            startDate,
            terminationDate,
            FloatTenor,
            FloatCalendar,
            FloatConvention,
            FloatTerminationDateConvention,
            FloatRule ?? Defaults().FloatingLegRule,
            FloatEndOfMonth ?? Defaults().FloatingLegEndOfMonth,
            context,
            FloatFirstDate,
            FloatNextToLastDate);

        ImmutableArray<CashFlow> cmsLeg = new CmsLeg(cmsSchedule, SwapIndex)
            .WithNotionals(Nominal)
            .WithPaymentDayCounter(CmsDayCount ?? Defaults().CmsLegDayCounter)
            .WithPaymentAdjustment(cmsConvention)
            .WithFixingDays(SwapIndex.FixingDays)
            .WithGearings(CmsGearing)
            .WithSpreads(CmsSpread)
            .WithCaps(CmsCap)
            .WithFloors(CmsFloor)
            .ToLeg();
        if (CouponPricer is not null)
        {
            cmsLeg = CouponPricerFunctions.SetCouponPricer(cmsLeg, CouponPricer);
        }

        double usedSpread = IborSpread;
        if (UseAtmSpread)
        {
            if (IborIndex.ForwardingTermStructure is null)
            {
                throw new NanoQLException($"null term structure set to this instance of {IborIndex.Name}");
            }

            if (SwapIndex.ForwardingTermStructure is null)
            {
                throw new NanoQLException($"null term structure set to this instance of {SwapIndex.Name}");
            }

            if (CouponPricer is null)
            {
                throw new NanoQLException("no CmsCouponPricer set (yet)");
            }

            ImmutableArray<CashFlow> atmFloatLeg = new IborLeg(floatSchedule, IborIndex)
                .WithNotionals(Nominal)
                .WithPaymentDayCounter(FloatDayCount)
                .WithPaymentAdjustment(FloatConvention)
                .WithFixingDays(IborIndex.FixingDays)
                .ToLeg(context);

            var temp = new Swap(cmsLeg, atmFloatLeg);
            SwapResults results = Engine(temp, context);

            double npv = Leg(results.LegNPV, 0) + Leg(results.LegNPV, 1);

            usedSpread = -npv / Leg(results.LegBPS, 1) * 1e-4;
        }

        ImmutableArray<CashFlow> floatLeg = new IborLeg(floatSchedule, IborIndex)
            .WithNotionals(Nominal)
            .WithPaymentDayCounter(FloatDayCount)
            .WithPaymentAdjustment(FloatConvention)
            .WithFixingDays(IborIndex.FixingDays)
            .WithSpreads(usedSpread)
            .ToLeg(context);

        return PayCms ? new Swap(cmsLeg, floatLeg) : new Swap(floatLeg, cmsLeg);
    }

    /// <summary>The defaults a builder takes under a provider for the options it isn't given.</summary>
    /// <param name="provider">The provider, the index's, which a builder on the index takes.</param>
    /// <returns>The provider's defaults; QuantLib's under <see cref="ConventionProvider.QuantLib"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The provider has no defaults (a set without QuantLib's presets).</exception>
    public static CmsDefaults Defaults(ConventionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ConventionProvider.ReadDefaults(provider, p => p.ResolveCmsDefaults(QuantLibDefaults), "CMS swaps");
    }

    // QuantLib's constants: a 3M CMS leg, Modified Following, backward, Actual/360; the floating leg backward.
    private static CmsDefaults QuantLibDefaults() =>
        new(
            new Period(3, TimeUnit.Months),
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            new Actual360(),
            DateGenerationRule.Backward,
            false);

    // A leg's NPV or BPS from the results, refused where it isn't available, as QuantLib's legNPV(j) and legBPS(j).
    private static double Leg(ImmutableArray<double?> values, int j) =>
        (j < values.Length ? values[j] : null) ?? throw new NanoQLException("result not available");
}
