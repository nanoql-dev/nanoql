// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/inflationtraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>The zero-inflation curve's bootstrap traits: the curve's data are zero-coupon inflation rates, from the base date.</summary>
public readonly struct ZeroInflationTraits : IZeroInflationTraits
{
    /// <inheritdoc/>
    public static int MaxIterations => 40;

    /// <inheritdoc/>
    public static bool HasGlobalGuess => true;

    /// <inheritdoc/>
    public static Date InitialDate(BootstrapState<ZeroInflationTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Curve.BaseDate;
    }

    /// <inheritdoc/>
    public static double InitialValue(BootstrapState<ZeroInflationTermStructure> c) =>
        // this will be overwritten during bootstrap
        InflationTraitsDetail.AvgInflation;

    /// <inheritdoc/>
    public static double Guess(int i, BootstrapState<ZeroInflationTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            // previous iteration value
            return c.Data[i];
        }

        return InflationTraitsDetail.AvgInflation;
    }

    /// <inheritdoc/>
    public static (Vector Guess, bool NeedsTransform) GlobalGuess(BootstrapState<ZeroInflationTermStructure> c, bool validData)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            return (new Vector(c.Data.AsSpan(1)), true);
        }

        return (new Vector(c.Times.Length - 1, InflationTraitsDetail.AvgInflation), true);
    }

    /// <inheritdoc/>
    public static double MinValueAfter(int i, BootstrapState<ZeroInflationTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MinElement(c.Data);
            return r < 0.0 ? r * 2.0 : r / 2.0;
        }

        return -InflationTraitsDetail.MaxInflation;
    }

    /// <inheritdoc/>
    public static double MaxValueAfter(int i, BootstrapState<ZeroInflationTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MaxElement(c.Data);
            return r < 0.0 ? r / 2.0 : r * 2.0;
        }

        // no constraints.
        // We choose as max a value very unlikely to be exceeded.
        return InflationTraitsDetail.MaxInflation;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> isn't a point of <paramref name="data"/>, where QuantLib writes past its end.
    /// </exception>
    public static void UpdateGuess(double[] data, double level, int i)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, data.Length);
        data[i] = level;
        if (i == 1)
        {
            data[0] = level; // the first point is updated as well
        }
    }

    /// <inheritdoc/>
    /// <remarks>The interpolation's value, extrapolated.</remarks>
    public static double ZeroRateImpl(Interpolation interpolation, double t)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        return interpolation.Evaluate(t, true);
    }

    /// <inheritdoc/>
    /// <remarks>The latest date the helpers need, or else the last node's.</remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="maxDate"/> isn't given and <paramref name="dates"/> is empty, where QuantLib reads past its end.
    /// </exception>
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) =>
        maxDate ?? CurveData.Last(dates, nameof(dates));
}
