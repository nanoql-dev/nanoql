// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/piecewiseyoyinflationcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A year-on-year inflation term structure bootstrapped on inflation helpers, on linearly interpolated year-on-year
/// inflation rates.
/// </summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for
/// <see cref="PiecewiseYoYInflationCurve{TInterpolator, TBootstrap, TTraits}"/> on <see cref="Linear"/>,
/// <see cref="IterativeBootstrap"/> and <see cref="YoYInflationTraits"/>: it adds constructors only.
/// </remarks>
public sealed class PiecewiseYoYInflation : PiecewiseYoYInflationCurve<Linear, IterativeBootstrap, YoYInflationTraits>
{
    /// <summary>Initializes a new instance of the <see cref="PiecewiseYoYInflation"/> class, and bootstraps
    /// it.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="baseDate">The base date: the last date for which there is information.</param>
    /// <param name="baseYoYRate">The year-on-year rate at the base date, the first node's.</param>
    /// <param name="frequency">The frequency of the index the curve forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="accuracy">The bootstrap's accuracy.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instruments"/>, <paramref name="dayCounter"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">A helper is null, or <paramref name="baseDate"/> is
    /// <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the curve, or the bootstrap
    /// fails.</exception>
    public PiecewiseYoYInflation(
        Date referenceDate,
        Date baseDate,
        double baseYoYRate,
        Frequency frequency,
        DayCounter dayCounter,
        IEnumerable<BootstrapHelper<YoYInflationTermStructure>> instruments,
        EvaluationContext context,
        Seasonality? seasonality = null,
        double accuracy = 1.0e-12,
        Linear i = default,
        IterativeBootstrap bootstrap = default)
        : base(
            referenceDate,
            baseDate,
            baseYoYRate,
            frequency,
            dayCounter,
            instruments,
            context,
            seasonality,
            accuracy,
            i,
            bootstrap)
    {
    }
}
