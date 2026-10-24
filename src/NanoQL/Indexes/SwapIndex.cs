// SPDX-FileCopyrightText: Copyright (C) 2006, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/swapindex.hpp ql/indexes/swapindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The base class of swap-rate indexes: the fair fixed rate of a vanilla swap starting at the value date.
/// </summary>
/// <remarks>
/// <para>
/// Unsealed, as in QuantLib, where the presets (<see cref="EuriborSwapIsdaFixA"/>, ...) and
/// <see cref="OvernightIndexedSwapIndex"/> derive from it. Its fixing is forecast from the swap
/// <see cref="UnderlyingSwap"/> builds, priced by a <see cref="DiscountingSwapEngine"/> without the settlement date's
/// flows: on the exogenous discounting curve when the index has one, and on the Ibor index's forwarding curve
/// otherwise.
/// </para>
/// <para>
/// The constructor taking a discounting curve makes the index's discounting exogenous even when the curve is
/// <see langword="null"/>, QuantLib's empty handle: a forecast then refuses "discounting term structure handle is
/// empty", as QuantLib's does.
/// </para>
/// <para>
/// QuantLib caches the last swap built, keyed by its fixing date only, so a swap built under one at-par switch keeps
/// serving forecasts after the switch changes. nanoql keeps no cache: each forecast builds its swap from the context
/// it is given, and so reads the context's switch as a QuantLib index built afresh would.
/// </para>
/// </remarks>
public class SwapIndex : InterestRateIndex
{
    // QuantLib's SwapIndex keeps the tenor as given in a member of its own, which hides the base's normalized one, and
    // builds its swap on it.
    private readonly Period tenor;
    private readonly YieldTermStructure? discount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwapIndex"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="settlementDays">The fixing days: the business days from a fixing to the swap's start.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="fixingCalendar">The fixing calendar, which is the fixed leg's calendar too.</param>
    /// <param name="fixedLegTenor">The fixed leg's tenor.</param>
    /// <param name="fixedLegConvention">The fixed leg's business-day convention.</param>
    /// <param name="fixedLegDayCounter">The fixed leg's day counter, which is the index's.</param>
    /// <param name="iborIndex">The floating leg's index.</param>
    /// <exception cref="ArgumentNullException">An argument but the currency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or <paramref name="fixedLegConvention"/> isn't a member of its
    /// type.
    /// </exception>
    public SwapIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        Period fixedLegTenor,
        BusinessDayConvention fixedLegConvention,
        DayCounter fixedLegDayCounter,
        IborIndex iborIndex)
        : this(
            familyName,
            tenor,
            settlementDays,
            currency,
            fixingCalendar,
            fixedLegTenor,
            fixedLegConvention,
            fixedLegDayCounter,
            iborIndex,
            false,
            null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwapIndex"/> class with exogenous discounting, on the curve given
    /// even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="settlementDays">The fixing days: the business days from a fixing to the swap's start.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="fixingCalendar">The fixing calendar, which is the fixed leg's calendar too.</param>
    /// <param name="fixedLegTenor">The fixed leg's tenor.</param>
    /// <param name="fixedLegConvention">The fixed leg's business-day convention.</param>
    /// <param name="fixedLegDayCounter">The fixed leg's day counter, which is the index's.</param>
    /// <param name="iborIndex">The floating leg's index.</param>
    /// <param name="discountingTermStructure">
    /// The discounting curve, or <see langword="null"/>, QuantLib's empty handle, on which a forecast is refused.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// An argument but the currency and the discounting curve is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or <paramref name="fixedLegConvention"/> isn't a member of its
    /// type.
    /// </exception>
    public SwapIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        Period fixedLegTenor,
        BusinessDayConvention fixedLegConvention,
        DayCounter fixedLegDayCounter,
        IborIndex iborIndex,
        YieldTermStructure? discountingTermStructure)
        : this(
            familyName,
            tenor,
            settlementDays,
            currency,
            fixingCalendar,
            fixedLegTenor,
            fixedLegConvention,
            fixedLegDayCounter,
            iborIndex,
            true,
            discountingTermStructure)
    {
    }

    private SwapIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        Period fixedLegTenor,
        BusinessDayConvention fixedLegConvention,
        DayCounter fixedLegDayCounter,
        IborIndex iborIndex,
        bool exogenousDiscount,
        YieldTermStructure? discount)
        : base(familyName, tenor, settlementDays, currency, fixingCalendar, fixedLegDayCounter)
    {
        // QuantLib accepts a null Ibor index and dereferences it later, which is undefined.
        ArgumentNullException.ThrowIfNull(iborIndex);
        EnumArguments.Check(fixedLegConvention, nameof(fixedLegConvention));
        this.tenor = tenor;
        IborIndex = iborIndex;
        FixedLegTenor = fixedLegTenor;
        FixedLegConvention = fixedLegConvention;
        ExogenousDiscount = exogenousDiscount;
        this.discount = discount;
    }

    /// <summary>Gets the fixed leg's tenor.</summary>
    public Period FixedLegTenor { get; }

    /// <summary>Gets the fixed leg's business-day convention.</summary>
    public BusinessDayConvention FixedLegConvention { get; }

    /// <summary>Gets the floating leg's index.</summary>
    public IborIndex IborIndex { get; }

    /// <summary>Gets the Ibor index's forwarding curve, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? ForwardingTermStructure => IborIndex.ForwardingTermStructure;

    /// <summary>
    /// Gets the exogenous discounting curve: <see langword="null"/> when the discounting isn't exogenous, or when it is
    /// on QuantLib's empty handle.
    /// </summary>
    public YieldTermStructure? DiscountingTermStructure => discount;

    /// <summary>
    /// Gets a value indicating whether the index discounts on a curve of its own, given at construction.
    /// </summary>
    public bool ExogenousDiscount { get; }

    /// <summary>The tenor the swap is built on, as given at construction: QuantLib's <c>tenor_</c>.</summary>
    private protected Period SwapTenor => tenor;

    /// <summary>
    /// The maturity date of a value date: the maturity of the swap fixing on the value date's fixing date, computed
    /// from the swap's schedules.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The maturity date.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="valueDate"/> is <c>default(Date)</c>, or its fixing date isn't a valid fixing date.
    /// </exception>
    /// <remarks>
    /// QuantLib builds the underlying swap, which reads the at-par switch, though the swap's dates don't depend on it:
    /// they come from its schedules. The swap is built here with an explicit coupon choice and its effective date, so
    /// its builder reads neither the switch nor the evaluation date, and the method takes no context.
    /// </remarks>
    public override Date MaturityDate(Date valueDate)
    {
        Date fixDate = FixingDate(valueDate);
        var datesOnly = new EvaluationContext { EvaluationDate = fixDate };
        return Builder(fixDate).WithIndexedCoupons(true).ToVanillaSwap(datesOnly).MaturityDate();
    }

    /// <summary>
    /// The swap whose fair rate is the fixing: from the fixing date's value date, at a zero fixed rate, with the fixed
    /// leg's conventions and the Ibor index's floating leg.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose at-par switch the floating leg reads.</param>
    /// <returns>The swap, which holds no engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="fixingDate"/> is <c>default(Date)</c> or isn't a valid fixing date.
    /// </exception>
    /// <exception cref="NanoQLException">The swap can't be built.</exception>
    public VanillaSwap UnderlyingSwap(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Builder(fixingDate).ToVanillaSwap(context);
    }

    /// <summary>The forecast fixing: the fair rate of the swap fixing on the date.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The forecast.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="fixingDate"/> is <c>default(Date)</c> or isn't a valid fixing date.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The swap can't be built or priced: its discounting curve is missing, or its fair rate isn't available, as for
    /// an expired swap.
    /// </exception>
    public override double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        VanillaSwap swap = UnderlyingSwap(fixingDate, context);
        return UnderlyingSwapResults(swap, context).FairRate ?? throw new NanoQLException("result not available");
    }

    // An underlying swap's results on the engine QuantLib's builder attaches to it: discounting on the exogenous curve,
    // or on the forwarding curve. The CMS pricers read the fair rate and the fixed leg's BPS from it, as QuantLib's
    // read them from the swap.
    internal FixedVsFloatingSwapResults UnderlyingSwapResults(FixedVsFloatingSwap swap, EvaluationContext context)
    {
        YieldTermStructure? curve = ExogenousDiscount ? discount : ForwardingTermStructure;
        return curve is null
            ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
            : DiscountingSwapEngine.Calculate(swap, context, curve, includeSettlementDateFlows: false);
    }

    /// <summary>
    /// A copy of the index on another forwarding curve: a plain <see cref="SwapIndex"/>, its Ibor index cloned on the
    /// curve, keeping the exogenous discounting curve if it has one.
    /// </summary>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public virtual SwapIndex Clone(YieldTermStructure? forwarding) =>
        ExogenousDiscount
            ? new SwapIndex(
                FamilyName,
                Tenor,
                FixingDays,
                Currency,
                FixingCalendar,
                FixedLegTenor,
                FixedLegConvention,
                DayCounter,
                IborIndex.Clone(forwarding),
                discount)
            : new SwapIndex(
                FamilyName,
                Tenor,
                FixingDays,
                Currency,
                FixingCalendar,
                FixedLegTenor,
                FixedLegConvention,
                DayCounter,
                IborIndex.Clone(forwarding));

    /// <summary>
    /// A copy of the index on other curves: a plain <see cref="SwapIndex"/>, its Ibor index cloned on the forwarding
    /// curve, whose discounting is exogenous, on the discounting curve, even when that is <see langword="null"/>.
    /// </summary>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <returns>The copy.</returns>
    public virtual SwapIndex Clone(YieldTermStructure? forwarding, YieldTermStructure? discounting) =>
        new(
            FamilyName,
            Tenor,
            FixingDays,
            Currency,
            FixingCalendar,
            FixedLegTenor,
            FixedLegConvention,
            DayCounter,
            IborIndex.Clone(forwarding),
            discounting);

    /// <summary>
    /// A copy of the index with another tenor: a plain <see cref="SwapIndex"/> on the same Ibor index, keeping the
    /// exogenous discounting curve if it has one.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns>The copy.</returns>
    public virtual SwapIndex Clone(Period tenor) =>
        ExogenousDiscount
            ? new SwapIndex(
                FamilyName,
                tenor,
                FixingDays,
                Currency,
                FixingCalendar,
                FixedLegTenor,
                FixedLegConvention,
                DayCounter,
                IborIndex,
                DiscountingTermStructure)
            : new SwapIndex(
                FamilyName,
                tenor,
                FixingDays,
                Currency,
                FixingCalendar,
                FixedLegTenor,
                FixedLegConvention,
                DayCounter,
                IborIndex);

    // The underlying swap's builder, QuantLib's MakeVanillaSwap chain.
    private MakeVanillaSwap Builder(Date fixingDate)
    {
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        MakeVanillaSwap builder = new MakeVanillaSwap(tenor, IborIndex)
            .WithFixedRate(0.0)
            .WithEffectiveDate(ValueDate(fixingDate))
            .WithFixedLegCalendar(FixingCalendar)
            .WithFixedLegDayCount(DayCounter)
            .WithFixedLegTenor(FixedLegTenor)
            .WithFixedLegConvention(FixedLegConvention)
            .WithFixedLegTerminationDateConvention(FixedLegConvention);
        return ExogenousDiscount ? builder.WithDiscountingTermStructure(discount) : builder;
    }
}
