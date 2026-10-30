// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makeswaption.hpp ql/instruments/makeswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A builder of standard market swaptions on a swap index.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToSwaption"/> builds the swaption, in place of QuantLib's conversion operators. It computes the
/// fixing date from the context's evaluation date at every conversion, where QuantLib's builder keeps the fixing date
/// its first conversion computed: a reused builder follows the context it is converted under.
/// </para>
/// <para>
/// With no strike, the swaption is struck at the money: at the fair rate of the index's own underlying swap, priced
/// without the settlement date's flows on the index's exogenous discounting curve, or on its forwarding curve. That
/// swap is the index's, so it can differ from the swaption's underlying, which the builder's settings build, as in
/// QuantLib: an Ibor index's swap always reads the context's at-par switch, and an overnight index's takes its
/// telescopic value dates and averaging method, which the underlying doesn't.
/// </para>
/// <para>
/// <see cref="WithPricingEngine"/> is kept and changes nothing: QuantLib's builder only attaches the engine to the
/// swaption, computing nothing with it, and a nanoql swaption holds no engine. A builder has no value equality.
/// </para>
/// <para>
/// An option the builder isn't given that QuantLib's builder fixes takes the builder's provider's default
/// (<see cref="Defaults"/>), read when the swaption is built: QuantLib's values with nothing configured.
/// </para>
/// </remarks>
public sealed record MakeSwaption
{
    /// <summary>Initializes a new instance of the <see cref="MakeSwaption"/> record, from an option tenor.</summary>
    /// <param name="swapIndex">The swap index, whose swap and conventions the swaption takes.</param>
    /// <param name="optionTenor">The option tenor, from the evaluation date adjusted on the exercise calendar.</param>
    /// <param name="strike">The strike, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for the
    /// at-the-money strike.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swapIndex"/> is <see langword="null"/>, which QuantLib
    /// dereferences at the conversion.</exception>
    public MakeSwaption(SwapIndex swapIndex, Period optionTenor, double? strike = null)
    {
        ArgumentNullException.ThrowIfNull(swapIndex);
        Provider = swapIndex.Provider;
        SwapIndex = swapIndex;
        OptionTenor = optionTenor;
        Strike = strike;
    }

    /// <summary>Initializes a new instance of the <see cref="MakeSwaption"/> record, from a fixing date.</summary>
    /// <param name="swapIndex">The swap index, whose swap and conventions the swaption takes.</param>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="strike">The strike, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for the
    /// at-the-money strike.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swapIndex"/> is <see langword="null"/>, which QuantLib
    /// dereferences at the conversion.</exception>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is <c>default(Date)</c>.</exception>
    public MakeSwaption(SwapIndex swapIndex, Date fixingDate, double? strike = null)
    {
        ArgumentNullException.ThrowIfNull(swapIndex);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        Provider = swapIndex.Provider;
        SwapIndex = swapIndex;
        FixingDate = fixingDate;
        Strike = strike;
    }

    // The provider the builder takes its conventions from and builds inside: its swap index's.
    private ConventionProvider Provider { get; init; }

    private SwapIndex SwapIndex { get; init; }

    private SettlementType? Delivery { get; init; }

    private SettlementMethod? SettlementMethod { get; init; }

    // The date form's tenor is QuantLib's default Period(), never read.
    private Period OptionTenor { get; init; } = new(0, TimeUnit.Days);

    private BusinessDayConvention? OptionConvention { get; init; }

    private Date? FixingDate { get; init; }

    private Date? ExerciseDate { get; init; }

    private Calendar? ExerciseCalendar { get; init; }

    private double? Strike { get; init; }

    private SwapType UnderlyingType { get; init; } = SwapType.Payer;

    private double Nominal { get; init; } = 1.0;

    private bool? UseIndexedCoupons { get; init; }

    /// <summary>Sets the underlying swap's nominal.</summary>
    /// <param name="n">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public MakeSwaption WithNominal(double n) => this with { Nominal = n };

    /// <summary>Sets the settlement type.</summary>
    /// <param name="delivery">The settlement type.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a member of the enum.</exception>
    public MakeSwaption WithSettlementType(SettlementType delivery)
    {
        EnumArguments.Check(delivery, nameof(delivery));
        return this with { Delivery = delivery };
    }

    /// <summary>Sets the settlement method.</summary>
    /// <param name="settlementMethod">The settlement method.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The method isn't a member of the enum.</exception>
    public MakeSwaption WithSettlementMethod(SettlementMethod settlementMethod)
    {
        EnumArguments.Check(settlementMethod, nameof(settlementMethod));
        return this with { SettlementMethod = settlementMethod };
    }

    /// <summary>Sets the convention the option tenor's fixing date is adjusted with.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeSwaption WithOptionConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { OptionConvention = bdc };
    }

    /// <summary>Sets the exercise date, on or before the fixing date; by default the exercise is on the fixing date.</summary>
    /// <param name="date">The exercise date, or <see langword="null"/>, QuantLib's <c>Date()</c>, for the fixing
    /// date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public MakeSwaption WithExerciseDate(Date? date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return this with { ExerciseDate = date };
    }

    /// <summary>
    /// Sets the calendar the evaluation date is adjusted on and the option tenor advanced on; by default the index's
    /// fixing calendar.
    /// </summary>
    /// <param name="cal">The calendar, or <see langword="null"/>, QuantLib's empty calendar, for the index's fixing
    /// calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeSwaption WithExerciseCalendar(Calendar? cal) => this with { ExerciseCalendar = cal };

    /// <summary>Sets the underlying swap's type: payer or receiver of the fixed rate.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a member of the enum.</exception>
    public MakeSwaption WithUnderlyingType(SwapType type)
    {
        EnumArguments.Check(type, nameof(type));
        return this with { UnderlyingType = type };
    }

    /// <summary>
    /// Sets whether an Ibor underlying's floating coupons use indexed coupons rather than par coupons;
    /// <see langword="null"/> for the context's switch, the default.
    /// </summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeSwaption WithIndexedCoupons(bool? b = true) => this with { UseIndexedCoupons = b };

    /// <summary>Sets whether an Ibor underlying's floating coupons use par coupons rather than indexed coupons.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeSwaption WithAtParCoupons(bool b = true) => this with { UseIndexedCoupons = !b };

    /// <summary>
    /// Kept from QuantLib, where it attaches the engine to the swaption: it changes nothing, since nothing at build
    /// time uses the engine and a swaption holds none. Price the swaption by calling the engine.
    /// </summary>
    /// <param name="engine">The engine, a function of the swaption and the context, such as
    /// <c>(s, c) =&gt; BlackSwaptionEngine.Calculate(s, c, curve, volatility)</c>.</param>
    /// <returns>The builder, unchanged.</returns>
    public MakeSwaption WithPricingEngine(Func<Swaption, EvaluationContext, InstrumentResults>? engine) => this;

    /// <summary>The swaption: QuantLib's conversion to <c>ext::shared_ptr&lt;Swaption&gt;</c>.</summary>
    /// <param name="context">The evaluation context: the fixing date is computed from its evaluation date, and an Ibor
    /// underlying's floating leg and an at-the-money strike's Ibor swap read its at-par switch.</param>
    /// <returns>The swaption.</returns>
    /// <exception cref="ArgumentNullException">The context is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise date is after the fixing date; or, with no strike, the index has no forwarding curve, or its swap
    /// can't be priced.
    /// </exception>
    /// <exception cref="ArgumentException">The fixing date isn't a valid fixing date of the index.</exception>
    public Swaption ToSwaption(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built inside the builder's provider, so what the build constructs takes its conventions.
        using IDisposable scope = ConventionProvider.Use(Provider);

        // the provider's defaults, read once an option needs one
        SwaptionDefaults? defaults = null;
        SwaptionDefaults Defaults() => defaults ??= MakeSwaption.Defaults(Provider);

        Calendar calendar = ExerciseCalendar ?? SwapIndex.FixingCalendar;
        Date refDate = context.EvaluationDate;

        // if the evaluation date is not a business day
        // then move to the next business day
        refDate = calendar.Adjust(refDate);
        Date fixingDate = FixingDate ?? calendar.Advance(refDate, OptionTenor, OptionConvention ?? Defaults().OptionConvention);
        Exercise exercise;
        if (ExerciseDate is not Date exerciseDate)
        {
            exercise = new EuropeanExercise(fixingDate);
        }
        else
        {
            if (!(exerciseDate <= fixingDate))
            {
                throw new NanoQLException(
                    $"exercise date ({exerciseDate}) must be less than or equal to fixing date ({fixingDate})");
            }

            exercise = new EuropeanExercise(exerciseDate);
        }

        double usedStrike;
        var oisSwapIndex = SwapIndex as OvernightIndexedSwapIndex;
        if (Strike is not double strike)
        {
            // ATM on curve(s) attached to index
            if (SwapIndex.ForwardingTermStructure is null)
            {
                throw new NanoQLException($"null term structure set to this instance of {SwapIndex.Name}");
            }

            FixedVsFloatingSwap temp = oisSwapIndex is not null
                ? oisSwapIndex.UnderlyingSwap(fixingDate, context)
                : SwapIndex.UnderlyingSwap(fixingDate, context);
            YieldTermStructure? curve = SwapIndex.ExogenousDiscount
                ? SwapIndex.DiscountingTermStructure
                : SwapIndex.ForwardingTermStructure;
            FixedVsFloatingSwapResults results = curve is null
                ? DiscountingSwapEngine.CalculateWithoutCurve(temp, context)
                : DiscountingSwapEngine.Calculate(temp, context, curve, includeSettlementDateFlows: false);
            usedStrike = results.FairRate ?? throw new NanoQLException("result not available");
        }
        else
        {
            usedStrike = strike;
        }

        BusinessDayConvention bdc = SwapIndex.FixedLegConvention;
        FixedVsFloatingSwap underlyingSwap = oisSwapIndex is not null
            ? new MakeOIS(SwapIndex.Tenor, oisSwapIndex.OvernightIndex, Provider)
                .WithFixedRate(usedStrike)
                .WithEffectiveDate(SwapIndex.ValueDate(fixingDate))
                .WithPaymentCalendar(SwapIndex.FixingCalendar)
                .WithFixedLegDayCount(SwapIndex.DayCounter)
                .WithPaymentAdjustment(bdc)
                .WithFixedLegConvention(bdc)
                .WithFixedLegTerminationDateConvention(bdc)
                .WithType(UnderlyingType)
                .WithNominal(Nominal)
                .ToOvernightIndexedSwap(context)
            : new MakeVanillaSwap(SwapIndex.Tenor, SwapIndex.IborIndex, Provider)
                .WithFixedRate(usedStrike)
                .WithEffectiveDate(SwapIndex.ValueDate(fixingDate))
                .WithFixedLegCalendar(SwapIndex.FixingCalendar)
                .WithFixedLegDayCount(SwapIndex.DayCounter)
                .WithFixedLegTenor(SwapIndex.FixedLegTenor)
                .WithFixedLegConvention(bdc)
                .WithFixedLegTerminationDateConvention(bdc)
                .WithType(UnderlyingType)
                .WithNominal(Nominal)
                .WithIndexedCoupons(UseIndexedCoupons)
                .ToVanillaSwap(context);
        return new Swaption(underlyingSwap, exercise, Delivery ?? Defaults().SettlementType, SettlementMethod ?? Defaults().SettlementMethod);
    }

    /// <summary>The defaults a builder takes under a provider for the options it isn't given.</summary>
    /// <param name="provider">The provider, the index's, which a builder on the index takes.</param>
    /// <returns>The provider's defaults; QuantLib's under <see cref="ConventionProvider.QuantLib"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The provider has no defaults (a set without QuantLib's presets).</exception>
    public static SwaptionDefaults Defaults(ConventionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ConventionProvider.ReadDefaults(provider, p => p.ResolveSwaptionDefaults(QuantLibDefaults), "swaptions");
    }

    // QuantLib's constants: physical settlement, OTC, the option tenor advanced Modified Following.
    private static SwaptionDefaults QuantLibDefaults() =>
        new(SettlementType.Physical, NanoQL.Instruments.SettlementMethod.PhysicalOTC, BusinessDayConvention.ModifiedFollowing);
}
