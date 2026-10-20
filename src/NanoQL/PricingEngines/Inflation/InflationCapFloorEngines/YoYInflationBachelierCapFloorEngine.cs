// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/inflation/inflationcapfloorengines.hpp ql/pricingengines/inflation/inflationcapfloorengines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>The Bachelier-formula year-on-year inflation cap/floor engine.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) and returns the instrument's results,
/// with QuantLib's additional results, read by <see cref="InstrumentResults.Result{T}"/>, each per optionlet and an
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/> of <see cref="double"/>: <c>optionletsPrice</c>,
/// <c>optionletsAtmForward</c> (0 for an optionlet paid on or before the nominal curve's reference date) and
/// <c>optionletsStdDev</c>, which a collar's results don't hold, as QuantLib's don't. QuantLib's <c>setVolatility</c>
/// has no form: a call takes the volatility.
/// </para>
/// <para>
/// Each optionlet's forward is its coupon's own fixing: the published fixing once it is out, the forecast before, where
/// QuantLib's reads the index's curve at the fixing date, a documented divergence. The index needs its year-on-year
/// curve only where a forecast is needed, and refuses with QuantLib's empty-handle text there.
/// </para>
/// </remarks>
public static class YoYInflationBachelierCapFloorEngine
{
    /// <summary>Prices a year-on-year cap, floor or collar.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="index">The year-on-year index the forwards are observed on.</param>
    /// <param name="volatility">The optionlet volatility, normal.</param>
    /// <param name="nominalTermStructure">The nominal curve the optionlets are discounted on.</param>
    /// <returns>The results: the expired results when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A cash flow isn't a year-on-year inflation coupon, a forward can't be fixed or
    /// forecast, or the volatility refuses a fixing date or a strike.</exception>
    /// <exception cref="ArgumentException">The formula refuses its inputs.</exception>
    public static InstrumentResults Calculate(
        YoYInflationCapFloor capFloor,
        EvaluationContext context,
        YoYInflationIndex index,
        YoYOptionletVolatilitySurface volatility,
        YieldTermStructure nominalTermStructure) =>
        YoYInflationCapFloorEngine<YoYInflationBachelierFormula>.Calculate(capFloor, context, index, volatility, nominalTermStructure);
}
