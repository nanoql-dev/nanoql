// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: Discount's globalGuess, transformDirect and transformInverse, the global bootstrap's hooks, which S5's second commit ports with GlobalBootstrap

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The discount-curve traits: the curve's data are discount factors, interpolated as an
/// <see cref="InterpolatedDiscountCurve{TInterpolator}"/>'s are.
/// </summary>
public readonly struct Discount : IYieldTraits
{
    /// <inheritdoc/>
    public static int MaxIterations => 100;

    /// <inheritdoc/>
    public static Date InitialDate(BootstrapState<YieldTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Curve.ReferenceDate;
    }

    /// <inheritdoc/>
    public static double InitialValue(BootstrapState<YieldTermStructure> c) => 1.0;

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
            return 1.0 / (1.0 + (BootstrapTraitsDetail.AvgRate * c.Times[1]));
        }

        // flat rate extrapolation
        double r = -Math.Log(c.Data[i - 1]) / c.Times[i - 1];
        return Math.Exp(-r * c.Times[i]);
    }

    /// <inheritdoc/>
    public static double MinValueAfter(int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            return BootstrapTraitsDetail.MinElement(c.Data) / 2.0;
        }

        double dt = c.Times[i] - c.Times[i - 1];
        return c.Data[i - 1] * Math.Exp(-BootstrapTraitsDetail.MaxRate * dt);
    }

    /// <inheritdoc/>
    public static double MaxValueAfter(int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        double dt = c.Times[i] - c.Times[i - 1];
        return c.Data[i - 1] * Math.Exp(BootstrapTraitsDetail.MaxRate * dt);
    }

    /// <inheritdoc/>
    public static void UpdateGuess(double[] data, double discount, int i)
    {
        ArgumentNullException.ThrowIfNull(data);
        data[i] = discount;
    }

    /// <inheritdoc/>
    public static double DiscountImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
        DiscountCurveFormula.DiscountImpl(times, data, interpolation, t);

    /// <inheritdoc/>
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) => DiscountCurveFormula.MaxDate(dates, maxDate);
}
