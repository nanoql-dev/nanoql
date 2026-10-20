// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflationindex.hpp ql/indexes/inflationindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// Observations of inflation indexes with a lag, flat or interpolated: QuantLib's <c>CPI</c> struct, with its
/// interpolation type as <see cref="CPIInterpolationType"/>.
/// </summary>
/// <remarks>
/// An inflation coupon observes its index with a lag, interpolating between fixings or not; QuantLib's indexes no
/// longer interpolate, the coupons do, through these functions.
/// </remarks>
public static class CPI
{
    private static readonly Period OneDay = new(1, TimeUnit.Days);

    /// <summary>An inflation index's fixing observed with a lag, flat or interpolated.</summary>
    /// <param name="index">The index.</param>
    /// <param name="date">The date without lag, usually the payment date of an inflation coupon.</param>
    /// <param name="observationLag">The lag subtracted from the date: with a date in May and a three-month lag, the
    /// fixing of February is observed (and of March, when interpolating).</param>
    /// <param name="interpolationType">The interpolation type.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing of the lagged date's period; or, interpolated, the line between it and the next period's
    /// fixing, at the date's position in its own period.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interpolationType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">A fixing it reads is missing or can't be forecast.</exception>
    public static double LaggedFixing(
        ZeroInflationIndex index,
        Date date,
        Period observationLag,
        CPIInterpolationType interpolationType,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(date, nameof(date));
        EnumArguments.Check(interpolationType, nameof(interpolationType));
        (Date fixingStart, Date fixingEnd) =
            InflationTermStructureFunctions.InflationPeriod(date - observationLag, index.Frequency);
        if (interpolationType == CPIInterpolationType.Flat)
        {
            return index.Fixing(fixingStart, context);
        }

        (Date interpolationStart, Date interpolationEnd) =
            InflationTermStructureFunctions.InflationPeriod(date, index.Frequency);
        double i0 = index.Fixing(fixingStart, context);
        if (date == interpolationStart)
        {
            // special case; no interpolation.  This avoids asking for
            // the fixing at the end of the period, which might need a
            // forecast curve to be set.
            return i0;
        }

        double i1 = index.Fixing(fixingEnd + OneDay, context);
        return i0 + ((i1 - i0) * (date - interpolationStart) / (double)((interpolationEnd + OneDay) - interpolationStart));
    }

    /// <summary>A year-on-year inflation index's rate observed with a lag, flat or interpolated.</summary>
    /// <param name="index">The index.</param>
    /// <param name="date">The date without lag, usually the payment date of an inflation coupon.</param>
    /// <param name="observationLag">The lag subtracted from the date.</param>
    /// <param name="interpolationType">The interpolation type.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>
    /// The rate of the lagged date's period; or, interpolated, the line between it and the next period's. For a ratio
    /// index whose fixings are past, the interpolated underlying fixing over the one a year earlier, less one: the
    /// convention is to interpolate the underlying first, which differs from interpolating the ratios, and it needs
    /// past fixings, since forecasts go through the year-on-year curve.
    /// </returns>
    /// <remarks>
    /// QuantLib chooses between the two interpolated forms by whether the unlagged date needs a forecast, though the
    /// fixings it reads are those of the lagged date's period and the next one, so a fixing it never reads can change
    /// the rate it returns; and where the lag is shorter than the index's period, it can take the ratio form on a fixing
    /// not yet published and forecast the underlying index, which its own comment rules out. Here the choice reads the
    /// latest fixing the ratio form uses: the next period's first day, or the lagged period's own when the date starts
    /// its period. The first case gets QuantLib's value with that fixing added, the second the ratios forecast through
    /// the year-on-year curve: a documented divergence that changes no QuantLib case.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interpolationType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">A fixing it reads is missing or can't be forecast.</exception>
    public static double LaggedYoYRate(
        YoYInflationIndex index,
        Date date,
        Period observationLag,
        CPIInterpolationType interpolationType,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(date, nameof(date));
        EnumArguments.Check(interpolationType, nameof(interpolationType));
        (Date fixingStart, Date fixingEnd) =
            InflationTermStructureFunctions.InflationPeriod(date - observationLag, index.Frequency);
        if (interpolationType == CPIInterpolationType.Flat)
        {
            return index.Fixing(fixingStart, context);
        }

        (Date interpolationStart, Date interpolationEnd) =
            InflationTermStructureFunctions.InflationPeriod(date, index.Frequency);
        Date latestRead = date == interpolationStart ? fixingStart : fixingEnd + OneDay;
        if (index.UnderlyingIndex is { } underlying && !index.NeedsForecast(latestRead, context))
        {
            // in the case of a ratio, the convention seems to be to interpolate
            // the underlying index fixings first, then take the ratio.  This is
            // not the same as taking the ratios and then interpolate, which is
            // equivalent to what the else clause does.
            // However, we can only do this if the fixings we need are in the past,
            // because forecasts need to be done through the YoY forecast curve,
            // and not the underlying index.
            double z1 = LaggedFixing(underlying, date, observationLag, interpolationType, context);
            double z0 = LaggedFixing(
                underlying, date - new Period(1, TimeUnit.Years), observationLag, interpolationType, context);
            return (z1 / z0) - 1.0;
        }

        double y0 = index.Fixing(fixingStart, context);
        if (date == interpolationStart)
        {
            // special case; no interpolation anyway.
            return y0;
        }

        double y1 = index.Fixing(fixingEnd + OneDay, context);
        return y0 + ((y1 - y0) * (date - interpolationStart) / (double)((interpolationEnd + OneDay) - interpolationStart));
    }

    // QuantLib's detail::CPI::isInterpolated, whose only other branch is the deprecated AsIndex.
    internal static bool IsInterpolated(CPIInterpolationType type) => type == CPIInterpolationType.Linear;
}
