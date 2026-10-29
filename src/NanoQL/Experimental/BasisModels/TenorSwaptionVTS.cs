// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/tenorswaptionvts.hpp ql/experimental/basismodels/tenorswaptionvts.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A normal swaption volatility on a target floating tenor and fixed frequency transformed from one on a base
/// floating tenor and fixed frequency: QuantLib's <c>TenorSwaptionVTS</c>.
/// </summary>
/// <remarks>
/// <para>
/// A target swaption's smile is the base smile at a strike mapped through the tenor basis spread, under an affine
/// terminal swap rate model fitted to the base swaption's deterministic flows, scaled by the target swap's annuity
/// against the final swap's. A smile section is built per query, as QuantLib's, from the swaps' fair rates on the
/// discount curve; the swaps are built in the context the structure was built with, which it keeps (S7R2's form,
/// master Revision 43). The target floating schedule is on the base index's calendar, as QuantLib's.
/// </para>
/// <para>
/// The swaps run the swap length rounded to whole months (the S-xrt plan's XRR5 (xv);
/// <c>Divergences/TenorSwaptionLengthTests.cs</c>): QuantLib casts the length to whole years before it multiplies by
/// twelve, so an 18-month tenor builds 12-month swaps; on whole years both are QuantLib's. A swap with no coupon on or
/// after the discount curve's reference date leaves its flows empty, which QuantLib then reads the last of, undefined;
/// here it is refused (XRR5 (xvi)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class TenorSwaptionVTS : SwaptionVolatilityStructure
{
    /// <summary>Initializes a new instance of the <see cref="TenorSwaptionVTS"/> class.</summary>
    /// <param name="baseVTS">The base swaption volatility.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="baseIndex">The base floating index.</param>
    /// <param name="targIndex">The target floating index.</param>
    /// <param name="baseFixedFreq">The base swaps' fixed-leg tenor.</param>
    /// <param name="targFixedFreq">The target swaps' fixed-leg tenor.</param>
    /// <param name="baseFixedDC">The base swaps' fixed-leg day counter.</param>
    /// <param name="targFixedDC">The target swaps' fixed-leg day counter.</param>
    /// <param name="context">The evaluation context the swaps are built and priced in.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public TenorSwaptionVTS(
        SwaptionVolatilityStructure baseVTS,
        YieldTermStructure discountCurve,
        IborIndex baseIndex,
        IborIndex targIndex,
        Period baseFixedFreq,
        Period targFixedFreq,
        DayCounter baseFixedDC,
        DayCounter targFixedDC,
        EvaluationContext context)
        : base(
            (baseVTS ?? throw new ArgumentNullException(nameof(baseVTS))).ReferenceDate,
            baseVTS.Calendar,
            baseVTS.BusinessDayConvention,
            baseVTS.DayCounter)
    {
        BaseVTS = baseVTS;
        DiscountCurve = discountCurve ?? throw new ArgumentNullException(nameof(discountCurve));
        BaseIndex = baseIndex ?? throw new ArgumentNullException(nameof(baseIndex));
        TargIndex = targIndex ?? throw new ArgumentNullException(nameof(targIndex));
        BaseFixedFreq = baseFixedFreq;
        TargFixedFreq = targFixedFreq;
        BaseFixedDC = baseFixedDC ?? throw new ArgumentNullException(nameof(baseFixedDC));
        TargFixedDC = targFixedDC ?? throw new ArgumentNullException(nameof(targFixedDC));
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    /// <remarks>The base structure's.</remarks>
    public override Date MaxDate => BaseVTS.MaxDate;

    /// <inheritdoc/>
    /// <remarks>The base structure's.</remarks>
    public override Period MaxSwapTenor => BaseVTS.MaxSwapTenor;

    /// <inheritdoc/>
    /// <remarks>Normal.</remarks>
    public override VolatilityType VolatilityType => VolatilityType.Normal;

    /// <summary>Gets the base swaption volatility.</summary>
    public SwaptionVolatilityStructure BaseVTS { get; }

    /// <summary>Gets the discount curve.</summary>
    public YieldTermStructure DiscountCurve { get; }

    /// <summary>Gets the base floating index.</summary>
    public IborIndex BaseIndex { get; }

    /// <summary>Gets the target floating index.</summary>
    public IborIndex TargIndex { get; }

    /// <summary>Gets the base swaps' fixed-leg tenor.</summary>
    public Period BaseFixedFreq { get; }

    /// <summary>Gets the target swaps' fixed-leg tenor.</summary>
    public Period TargFixedFreq { get; }

    /// <summary>Gets the base swaps' fixed-leg day counter.</summary>
    public DayCounter BaseFixedDC { get; }

    /// <summary>Gets the target swaps' fixed-leg day counter.</summary>
    public DayCounter TargFixedDC { get; }

    /// <summary>Gets the evaluation context the swaps are built and priced in.</summary>
    public EvaluationContext Context { get; }

    /// <inheritdoc/>
    /// <remarks>The base structure's.</remarks>
    public override double MinStrike() => BaseVTS.MinStrike();

    /// <inheritdoc/>
    /// <remarks>The base structure's.</remarks>
    public override double MaxStrike() => BaseVTS.MaxStrike();

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(double optionTime, double swapLength) =>
        new TenorSwaptionSmileSection(this, optionTime, swapLength);

    /// <inheritdoc/>
    protected override double VolatilityImpl(double optionTime, double swapLength, double strike) =>
        SmileSectionImpl(optionTime, swapLength).Volatility(strike, VolatilityType.Normal, 0.0);
}
