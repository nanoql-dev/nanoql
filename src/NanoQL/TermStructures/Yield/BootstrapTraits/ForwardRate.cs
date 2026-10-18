// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The forward-curve traits: the curve's data are instantaneous forward rates, interpolated as an interpolated forward
/// curve's are.
/// </summary>
public readonly struct ForwardRate : IYieldTraits
{
    /// <inheritdoc/>
    public static int MaxIterations => 100;

    /// <inheritdoc/>
    public static bool HasGlobalGuess => true;

    /// <inheritdoc/>
    public static Date InitialDate(BootstrapState<YieldTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Curve.ReferenceDate;
    }

    /// <inheritdoc/>
    public static double InitialValue(BootstrapState<YieldTermStructure> c) => BootstrapTraitsDetail.AvgRate;

    /// <inheritdoc/>
    public static double Guess(int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);

        // previous iteration value
        if (validData)
        {
            return c.Data[i];
        }

        // first pillar
        if (i == 1)
        {
            return BootstrapTraitsDetail.AvgRate;
        }

        // extrapolate
        Date d = c.Dates[i];
        return c.Curve.ForwardRate(d, d, c.Curve.DayCounter, Compounding.Continuous, Frequency.Annual, true).Rate;
    }

    /// <inheritdoc/>
    public static (Vector Guess, bool NeedsTransform) GlobalGuess(BootstrapState<YieldTermStructure> c, bool validData)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            return (new Vector(c.Data.AsSpan(1)), true);
        }

        return (new Vector(c.Times.Length - 1, BootstrapTraitsDetail.AvgRate), true);
    }

    /// <inheritdoc/>
    public static double MinValueAfter(int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MinElement(c.Data);
            return r < 0.0 ? r * 2.0 : r / 2.0;
        }

        // no constraints.
        // We choose as min a value very unlikely to be exceeded.
        return -BootstrapTraitsDetail.MaxRate;
    }

    /// <inheritdoc/>
    public static double MaxValueAfter(int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MaxElement(c.Data);
            return r < 0.0 ? r / 2.0 : r * 2.0;
        }

        // no constraints.
        // We choose as max a value very unlikely to be exceeded.
        return BootstrapTraitsDetail.MaxRate;
    }

    /// <inheritdoc/>
    public static void UpdateGuess(double[] data, double forward, int i)
    {
        ArgumentNullException.ThrowIfNull(data);
        data[i] = forward;

        // first point is updated as well
        if (i == 1)
        {
            data[0] = forward;
        }
    }

    /// <summary>
    /// The forward curve's continuously compounded zero rate at a time, the average of the interpolated forwards up to
    /// it, extrapolating past the last node with the last forward: QuantLib's
    /// <c>InterpolatedForwardCurve::zeroYieldImpl</c>.
    /// </summary>
    /// <param name="times">The nodes' times; the last one bounds the interpolation's range.</param>
    /// <param name="data">The nodes' forward rates.</param>
    /// <param name="interpolation">The interpolation of the forwards.</param>
    /// <param name="t">The time.</param>
    /// <returns>The zero rate.</returns>
    public static double ZeroYieldImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
        ForwardCurveFormula.ZeroYieldImpl(times, data, interpolation, t);

    /// <inheritdoc/>
    /// <remarks>QuantLib's <c>ZeroYieldStructure::discountImpl</c> over <see cref="ZeroYieldImpl"/>.</remarks>
    public static double DiscountImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        // a safe guard in cases where the zero yield at 0 would throw
        if (t == 0.0)
        {
            return 1.0;
        }

        double r = ZeroYieldImpl(times, data, interpolation, t);
        return Math.Exp(-r * t);
    }

    /// <inheritdoc/>
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) => ForwardCurveFormula.MaxDate(dates, maxDate);
}
