// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/yoyoptionlethelpers.hpp ql/experimental/inflation/yoyoptionlethelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A year-on-year inflation volatility bootstrap helper: a cap or floor priced off the volatility under construction,
/// quoting its price: QuantLib's <c>YoYOptionletHelper</c>.
/// </summary>
/// <remarks>
/// <para>
/// The instrument is built once, at construction, from the context's evaluation date, as QuantLib's; its earliest and
/// latest dates are its first and last coupons' fixing dates, the index values that fix it. The engine is a function of
/// the instrument, the context and the trial volatility, the caller binding the index and the nominal curve, as <c>(c,
/// ctx, v) =&gt; YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, index, v, nominal)</c>: QuantLib resets
/// the shared engine's volatility to the bound surface instead.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class YoYOptionletHelper : BootstrapHelper<YoYOptionletVolatilitySurface>
{
    private readonly Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> pricer;
    private readonly YoYInflationCapFloor yoyCapFloor;

    /// <summary>Initializes a new instance of the <see cref="YoYOptionletHelper"/> class.</summary>
    /// <param name="price">The quoted price.</param>
    /// <param name="notional">The notional, which gets the price level right, e.g. 10,000 for basis points.</param>
    /// <param name="capFloorType">Cap or floor.</param>
    /// <param name="lag">The observation lag.</param>
    /// <param name="yoyDayCounter">The payment day counter.</param>
    /// <param name="paymentCalendar">The payment calendar.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="index">The year-on-year index.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="n">The number of payments, in years.</param>
    /// <param name="pricer">The engine, a function of the instrument, the context and the trial volatility.</param>
    /// <param name="context">The evaluation context the instrument is dated in.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> or <paramref name="fixingDays"/> is negative,
    /// or an enum argument isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The instrument can't be built.</exception>
    public YoYOptionletHelper(
        double price,
        double notional,
        YoYInflationCapFloorType capFloorType,
        Period lag,
        DayCounter yoyDayCounter,
        Calendar paymentCalendar,
        int fixingDays,
        YoYInflationIndex index,
        CPIInterpolationType interpolation,
        double strike,
        int n,
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> pricer,
        EvaluationContext context)
        : base(price)
    {
        ArgumentNullException.ThrowIfNull(yoyDayCounter);
        ArgumentNullException.ThrowIfNull(paymentCalendar);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(pricer);
        ArgumentNullException.ThrowIfNull(context);
        this.pricer = pricer;

        // build the instrument to reprice (only need do this once)
        yoyCapFloor = new MakeYoYInflationCapFloor(capFloorType, index, n, paymentCalendar, lag, interpolation)
            .WithNominal(notional)
            .WithFixingDays(fixingDays)
            .WithPaymentDayCounter(yoyDayCounter)
            .WithStrike(strike)
            .ToYoYInflationCapFloor(context);

        // dates already build in lag of index/instrument
        // these are the dates of the values of the index
        // that fix the capfloor
        earliestDate = ((YoYInflationCoupon)yoyCapFloor.YoYLeg[0]).FixingDate();
        latestDate = ((YoYInflationCoupon)yoyCapFloor.YoYLeg[^1]).FixingDate();
    }

    /// <summary>
    /// A copy of the helper bound to a volatility: QuantLib's <c>setTermStructure</c>, which binds the helper and
    /// resets the shared engine's volatility in place. The copy prices against the volatility; the helper it is called
    /// on stays unbound.
    /// </summary>
    /// <param name="v">The volatility.</param>
    /// <returns>The bound copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="v"/> is null.</exception>
    public YoYOptionletHelper SetTermStructure(YoYOptionletVolatilitySurface v) => (YoYOptionletHelper)WithTermStructure(v);

    /// <inheritdoc/>
    /// <remarks>The instrument's NPV off the bound volatility.</remarks>
    /// <exception cref="NanoQLException">The helper isn't bound to a volatility, or the engine gives no NPV.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        return pricer(yoyCapFloor, context, TermStructure!).NPV ?? throw new NanoQLException("NPV not provided");
    }
}
