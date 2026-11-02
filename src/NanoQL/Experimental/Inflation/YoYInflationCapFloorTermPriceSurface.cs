// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 Bernd Engelmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/yoycapfloortermpricesurface.hpp ql/experimental/inflation/yoycapfloortermpricesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// The year-on-year cap and floor price surface on bicubic interpolation of its prices and cubic interpolation of its
/// swap rates: QuantLib-Python's <c>YoYInflationCapFloorTermPriceSurface</c>.
/// </summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for
/// <see cref="InterpolatedYoYCapFloorTermPriceSurface{TInterpolator2D, TInterpolator1D}"/> on <see cref="Bicubic"/>
/// and <see cref="Cubic"/>: its own <see cref="Create"/> returns it.
/// </remarks>
[Experimental("NQL0001")]
public sealed class YoYInflationCapFloorTermPriceSurface : InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic>
{
    private YoYInflationCapFloorTermPriceSurface(
        Date referenceDate,
        int fixingDays,
        Period yyLag,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure nominal,
        DayCounter dc,
        Calendar cal,
        BusinessDayConvention bdc,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice,
        Bicubic interpolator2d,
        Cubic interpolator1d,
        EvaluationContext context)
        : base(referenceDate, fixingDays, yyLag, yii, interpolation, nominal, dc, cal, bdc, cStrikes, fStrikes, cfMaturities, cPrice, fPrice, interpolator2d, interpolator1d, context)
    {
    }

    /// <summary>
    /// Builds the surface on the evaluation date's reference date, its year-on-year curve bootstrapped in the context.
    /// </summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="yyLag">The quoted instruments' observation lag.</param>
    /// <param name="yii">The year-on-year index.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="nominal">The nominal curve.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="cal">The calendar, which adjusts the reference date and the helpers' payments.</param>
    /// <param name="bdc">The business-day convention of the helpers' payments.</param>
    /// <param name="cStrikes">The cap strikes, increasing.</param>
    /// <param name="fStrikes">The floor strikes, increasing.</param>
    /// <param name="cfMaturities">The maturities, increasing.</param>
    /// <param name="cPrice">The cap prices, a row per cap strike and a column per maturity, non-increasing in
    /// strike.</param>
    /// <param name="fPrice">The floor prices, a row per floor strike and a column per maturity, non-decreasing in
    /// strike.</param>
    /// <param name="context">The evaluation context: its evaluation date, and the market the helpers are priced
    /// in.</param>
    /// <param name="interpolator2d">The 2-D interpolator.</param>
    /// <param name="interpolator1d">The swap rates' interpolator.</param>
    /// <returns>The surface.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The data are too few, mis-sized, non-positive or not monotonic; an
    /// intersection past five years fails; or the curve can't be bootstrapped or doesn't reprice its helpers; with
    /// QuantLib's texts.</exception>
    public static new YoYInflationCapFloorTermPriceSurface Create(
        int fixingDays,
        Period yyLag,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure nominal,
        DayCounter dc,
        Calendar cal,
        BusinessDayConvention bdc,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice,
        EvaluationContext context,
        Bicubic interpolator2d = default,
        Cubic interpolator1d = default)
    {
        ArgumentNullException.ThrowIfNull(cal);
        ArgumentNullException.ThrowIfNull(context);
        Date referenceDate = cal.Advance(context.EvaluationDate, 0, TimeUnit.Days);
        return new YoYInflationCapFloorTermPriceSurface(
            referenceDate, fixingDays, yyLag, yii, interpolation, nominal, dc, cal, bdc, cStrikes, fStrikes,
            cfMaturities, cPrice, fPrice, interpolator2d, interpolator1d, context);
    }
}
