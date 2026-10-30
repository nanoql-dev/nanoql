// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makecapfloor.hpp ql/instruments/makecapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A builder of standard market caps and floors on an Ibor index.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToCapFloor"/> builds the instrument, in place of QuantLib's conversion operators. The leg is the
/// floating leg of the <see cref="MakeVanillaSwap"/> the builder holds, built at 0% with a yearly Actual/365 (Fixed)
/// fixed leg, which only spares that builder its currency's default fixed tenor; the first caplet is excluded unless a
/// forward start or the effective date's flag keeps it, and an optionlet keeps the last coupon alone.
/// </para>
/// <para>
/// With no strike, the instrument is struck at the money: at the leg's at-the-money rate on a discount curve, at the
/// curve's reference date and without the flows paid on it. QuantLib reads that curve out of the Black or Bachelier
/// engine given to <c>withPricingEngine</c>; an engine here is a function, which hides its curve, so the builder takes
/// the curve itself through <see cref="WithDiscountingTermStructure"/>, which computes the strike and is never stored.
/// <see cref="WithPricingEngine"/> is kept and changes nothing: the instrument holds no engine. The strike comes before
/// the instrument is built, so a <see cref="CapFloorType.Collar"/> is refused by the instrument's constructor after the
/// at-the-money rate, as QuantLib refuses it. A builder has no value equality.
/// </para>
/// <para>
/// An option the builder isn't given that QuantLib's builder fixes takes the builder's provider's default
/// (<see cref="Defaults"/>), read when the instrument is built: QuantLib's values with nothing configured.
/// </para>
/// </remarks>
public sealed record MakeCapFloor
{
    /// <summary>Initializes a new instance of the <see cref="MakeCapFloor"/> record.</summary>
    /// <param name="capFloorType">The type: a cap or a floor, which the conversion builds.</param>
    /// <param name="capFloorTenor">The instrument's tenor, the underlying swap's.</param>
    /// <param name="iborIndex">The index, whose conventions are the leg's defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="iborIndex"/> is <see langword="null"/>, which QuantLib
    /// dereferences.</exception>
    public MakeCapFloor(CapFloorType capFloorType, Period capFloorTenor, IborIndex iborIndex)
    {
        ArgumentNullException.ThrowIfNull(iborIndex);
        Provider = iborIndex.Provider;
        CapFloorType = capFloorType;

        // setting the fixed leg tenor avoids that MakeVanillaSwap throws
        // because of an unknown fixed leg default tenor for a currency,
        // notice that only the floating leg of the swap is used anyway
        SwapBuilder = new MakeVanillaSwap(capFloorTenor, iborIndex, Provider)
            .WithFixedRate(0.0)
            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
            .WithFixedLegDayCount(new Actual365Fixed());
    }

    // The provider the builder takes its conventions from and builds inside: its index's.
    private ConventionProvider Provider { get; init; }

    private CapFloorType CapFloorType { get; init; }

    private double? Strike { get; init; }

    private bool? FirstCapletExcluded { get; init; }

    private bool IsOptionlet { get; init; }

    private MakeVanillaSwap SwapBuilder { get; init; }

    private YieldTermStructure? DiscountCurve { get; init; }

    /// <summary>Sets the nominal.</summary>
    /// <param name="n">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithNominal(double n) => this with { SwapBuilder = SwapBuilder.WithNominal(n) };

    /// <summary>Sets the strike.</summary>
    /// <param name="k">The strike, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c> and its default,
    /// for the at-the-money strike.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithStrike(double? k) => this with { Strike = k };

    /// <summary>
    /// Sets the forward start, after the spot date; the first caplet is excluded for a zero forward start and kept
    /// otherwise.
    /// </summary>
    /// <param name="f">The forward start.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithForwardStart(Period f) =>
        this with { SwapBuilder = SwapBuilder.WithForwardStart(f), FirstCapletExcluded = f == new Period(0, TimeUnit.Days) };

    /// <summary>Sets an explicit effective date, and whether the first caplet is excluded.</summary>
    /// <param name="effectiveDate">The effective date, or <see langword="null"/> for none.</param>
    /// <param name="firstCapletExcluded">Whether the first caplet is excluded.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeCapFloor WithEffectiveDate(Date? effectiveDate, bool firstCapletExcluded) =>
        this with { SwapBuilder = SwapBuilder.WithEffectiveDate(effectiveDate), FirstCapletExcluded = firstCapletExcluded };

    /// <summary>Sets the leg's tenor; the index's by default.</summary>
    /// <param name="t">The tenor.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithTenor(Period t) => this with { SwapBuilder = SwapBuilder.WithFloatingLegTenor(t) };

    /// <summary>Sets the leg's calendar; the index's fixing calendar by default.</summary>
    /// <param name="cal">The calendar, or <see langword="null"/> for QuantLib's empty calendar.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithCalendar(Calendar? cal) => this with { SwapBuilder = SwapBuilder.WithFloatingLegCalendar(cal) };

    /// <summary>Sets the leg's business-day convention; the index's by default.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeCapFloor WithConvention(BusinessDayConvention bdc) =>
        this with { SwapBuilder = SwapBuilder.WithFloatingLegConvention(bdc) };

    /// <summary>Sets the leg's termination-date convention; the index's convention by default.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeCapFloor WithTerminationDateConvention(BusinessDayConvention bdc) =>
        this with { SwapBuilder = SwapBuilder.WithFloatingLegTerminationDateConvention(bdc) };

    /// <summary>Sets the leg's date-generation rule; backward by default.</summary>
    /// <param name="r">The rule.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public MakeCapFloor WithRule(DateGenerationRule r) => this with { SwapBuilder = SwapBuilder.WithFloatingLegRule(r) };

    /// <summary>Sets whether the leg's schedule follows the end of the month.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithEndOfMonth(bool flag = true) =>
        this with { SwapBuilder = SwapBuilder.WithFloatingLegEndOfMonth(flag) };

    /// <summary>Sets the leg's first date, after a short or long front stub.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeCapFloor WithFirstDate(Date? d) => this with { SwapBuilder = SwapBuilder.WithFloatingLegFirstDate(d) };

    /// <summary>Sets the leg's next-to-last date, before a short or long back stub.</summary>
    /// <param name="d">The date, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    public MakeCapFloor WithNextToLastDate(Date? d) =>
        this with { SwapBuilder = SwapBuilder.WithFloatingLegNextToLastDate(d) };

    /// <summary>Sets the leg's day counter; the index's by default.</summary>
    /// <param name="dc">The day counter, or <see langword="null"/>, QuantLib's empty day counter.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithDayCount(DayCounter? dc) => this with { SwapBuilder = SwapBuilder.WithFloatingLegDayCount(dc) };

    /// <summary>Sets whether only the last coupon is kept: an optionlet.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor AsOptionlet(bool b = true) => this with { IsOptionlet = b };

    /// <summary>
    /// Sets the discount curve of the at-the-money strike, which QuantLib reads out of its Black or Bachelier engine:
    /// with no strike given, the conversion strikes the instrument at the leg's at-the-money rate on it. It is never
    /// stored on the instrument.
    /// </summary>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/>, the default, for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeCapFloor WithDiscountingTermStructure(YieldTermStructure? discountCurve) =>
        this with { DiscountCurve = discountCurve };

    /// <summary>
    /// Kept from QuantLib, where it sets the engine the instrument is priced with and whose curve the at-the-money
    /// strike reads: it changes nothing, since the instrument holds no engine; give the at-the-money curve through
    /// <see cref="WithDiscountingTermStructure"/>, and price the instrument by calling the engine.
    /// </summary>
    /// <param name="engine">The engine, a function of the instrument and the context, such as
    /// <c>(c, ctx) =&gt; BlackCapFloorEngine.Calculate(c, ctx, curve, 0.2)</c>.</param>
    /// <returns>The builder, unchanged.</returns>
    public MakeCapFloor WithPricingEngine(Func<CapFloor, EvaluationContext, InstrumentResults>? engine) => this;

    /// <summary>The instrument: QuantLib's conversion to <c>ext::shared_ptr&lt;CapFloor&gt;</c>.</summary>
    /// <param name="context">The evaluation context: the leg starts from its evaluation date and reads its at-par
    /// switch, and an at-the-money strike reads the coupons' fixings and forecasts.</param>
    /// <returns>The instrument, a <see cref="CapFloor"/> of the builder's type.</returns>
    /// <exception cref="ArgumentNullException">The context is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a cap or a floor.</exception>
    /// <exception cref="NanoQLException">
    /// With no strike, there is no discount curve, or the leg's at-the-money rate can't be computed; or the leg can't
    /// be built.
    /// </exception>
    public CapFloor ToCapFloor(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built inside the builder's provider, so what the build constructs takes its conventions.
        using IDisposable scope = ConventionProvider.Use(Provider);

        VanillaSwap swap = SwapBuilder.ToVanillaSwap(context);

        ImmutableArray<CashFlow> leg = swap.FloatingLeg;
        if (FirstCapletExcluded ?? Defaults(Provider).FirstCapletExcluded)
        {
            leg = leg.RemoveAt(0);
        }

        // only leaves the last coupon
        if (IsOptionlet && leg.Length > 1)
        {
            leg = [leg[^1]];
        }

        double strike;
        if (Strike is double k)
        {
            strike = k;
        }
        else
        {
            YieldTermStructure discountCurve = DiscountCurve
                ?? throw new NanoQLException("cannot calculate ATM without a BlackCapFloorEngine or BachelierCapFloorEngine");
            strike = CashFlows.AtmRate(leg, discountCurve, context, false, discountCurve.ReferenceDate);
        }

        return new CapFloor(CapFloorType, leg, [strike]);
    }

    /// <summary>The defaults a builder takes under a provider for the options it isn't given.</summary>
    /// <param name="provider">The provider, the index's, which a builder on the index takes.</param>
    /// <returns>The provider's defaults; QuantLib's under <see cref="ConventionProvider.QuantLib"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The provider has no defaults (a set without QuantLib's presets).</exception>
    public static CapFloorDefaults Defaults(ConventionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ConventionProvider.ReadDefaults(provider, p => p.ResolveCapFloorDefaults(QuantLibDefaults), "caps and floors");
    }

    // QuantLib's constant: the first caplet excluded.
    private static CapFloorDefaults QuantLibDefaults() =>
        new(true);
}
