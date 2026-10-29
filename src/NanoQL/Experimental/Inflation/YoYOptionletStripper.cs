// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/yoyoptionletstripper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// Strips year-on-year optionlet volatilities from a cap and floor price surface along each strike: QuantLib's
/// <c>YoYOptionletStripper</c>.
/// </summary>
/// <remarks>
/// <para>
/// An extension point (U13): <see cref="InterpolatedYoYOptionletStripper{TInterpolator}"/> derives from it. QuantLib's
/// <c>initialize</c> strips and stores the curves in the stripper, which then answers for its strikes and slices;
/// here <see cref="Strip"/> returns them, an immutable <see cref="StrippedYoYOptionlets"/>, and the stripper holds
/// nothing (the S-xrt plan's XRD8).
/// </para>
/// <para>
/// The engine is a function of the instrument, the context and the trial volatility, the caller binding the index and
/// the nominal curve, as <c>(c, ctx, v) =&gt; YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, index, v,
/// nominal)</c>: QuantLib resets the shared engine's volatility on each trial instead.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class YoYOptionletStripper
{
    /// <summary>Strips the volatilities along each of the surface's strikes.</summary>
    /// <param name="surface">The cap and floor price surface.</param>
    /// <param name="engine">The cap and floor engine, a function of the instrument, the context and the trial
    /// volatility.</param>
    /// <param name="slope">The assumption on the initial optionlet volatilities' change over time, in proportion to
    /// their level.</param>
    /// <param name="context">The evaluation context the instruments are dated and priced in.</param>
    /// <returns>The stripped volatilities.</returns>
    public abstract StrippedYoYOptionlets Strip(
        YoYCapFloorTermPriceSurface surface,
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> engine,
        double slope,
        EvaluationContext context);
}
