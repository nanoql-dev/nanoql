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
/// The simple zero-curve traits: the curve's data are simply compounded zero rates, interpolated as an interpolated
/// simple zero curve's are.
/// </summary>
public readonly struct SimpleZeroYield : IYieldTraits
{
    /// <inheritdoc/>
    public static int MaxIterations => 100;

    /// <inheritdoc/>
    public static bool HasGlobalGuess => true;

    /// <inheritdoc/>
    public static bool HasTransform => true;

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
        return c.Curve.ZeroRate(d, c.Curve.DayCounter, Compounding.Simple, Frequency.Annual, true).Rate;
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
        double result;
        if (validData)
        {
            double r = BootstrapTraitsDetail.MinElement(c.Data);
            result = r < 0.0 ? r * 2.0 : r / 2.0;
        }
        else
        {
            // no constraints.
            // We choose as min a value very unlikely to be exceeded.
            result = -BootstrapTraitsDetail.MaxRate;
        }

        // std::max(result, ...): the first argument unless the second compares larger
        double bound = (-1.0 / c.Times[i]) + 1E-8;
        return result < bound ? bound : result;
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
    public static double TransformDirect(double x, int i, BootstrapState<YieldTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return Math.Exp(x) + ((-1.0 / NodeTime(c, i)) + 1E-8);
    }

    /// <inheritdoc/>
    public static double TransformInverse(double x, int i, BootstrapState<YieldTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return Math.Log(x - ((-1.0 / NodeTime(c, i)) + 1E-8));
    }

    /// <inheritdoc/>
    public static void UpdateGuess(double[] data, double rate, int i)
    {
        ArgumentNullException.ThrowIfNull(data);
        data[i] = rate;

        // first point is updated as well
        if (i == 1)
        {
            data[0] = rate;
        }
    }

    /// <inheritdoc/>
    public static double DiscountImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
        InterpolatedSimpleZeroCurveFormula.DiscountImpl(times, data, interpolation, t);

    /// <inheritdoc/>
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) =>
        InterpolatedSimpleZeroCurveFormula.MaxDate(dates, maxDate);

    // The time of the node a transform is for. Past the last node it reads a guess longer than the curve's pillars,
    // which QuantLib reads out of its times' range (bootstraptraits.hpp:408-416).
    private static double NodeTime(BootstrapState<YieldTermStructure> c, int i) =>
        i < c.Times.Length
            ? c.Times[i]
            : throw new NanoQLException(
                $"initial guess longer than the curve's {c.Times.Length - 1} pillars: "
                + $"the simple-zero transform reads node {i}");
}
