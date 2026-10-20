// SPDX-FileCopyrightText: Copyright (C) 2008, 2016 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/probabilitytraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The default-density curve traits: the bootstrap of default densities, and the formulas of
/// <see cref="InterpolatedDefaultDensityCurve{TInterpolator}"/>.
/// </summary>
/// <remarks>A stateless configuration struct, as every bootstrap traits struct is.</remarks>
public readonly struct DefaultDensity : IDefaultProbabilityTraits
{
    /// <summary>Gets the upper bound of the bootstrap's convergence loop: 30.</summary>
    public static int MaxIterations => 30;

    /// <summary>The start of the curve's data: the reference date.</summary>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The date.</returns>
    public static Date InitialDate(BootstrapState<DefaultProbabilityTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Curve.ReferenceDate;
    }

    /// <summary>The value at the reference date, which the first pillar's value overwrites.</summary>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The value.</returns>
    public static double InitialValue(BootstrapState<DefaultProbabilityTermStructure> c) =>
        ProbabilityTraitsDetail.AvgHazardRate;

    /// <inheritdoc/>
    public static double Guess(
        int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
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
            return ProbabilityTraitsDetail.AvgHazardRate;
        }

        // extrapolate
        Date d = c.Dates[i];
        return c.Curve.DefaultDensity(d, true);
    }

    /// <inheritdoc/>
    public static double MinValueAfter(
        int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MinElement(c.Data);
            return r / 2.0;
        }

        return NumericLimits.QL_EPSILON;
    }

    /// <inheritdoc/>
    public static double MaxValueAfter(
        int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MaxElement(c.Data);
            return r * 2.0;
        }

        // no constraints.
        // We choose as max a value very unlikely to be exceeded.
        return ProbabilityTraitsDetail.MaxHazardRate;
    }

    /// <inheritdoc/>
    public static void UpdateGuess(double[] data, double density, int i)
    {
        ArgumentNullException.ThrowIfNull(data);
        data[i] = density;

        // first point is updated as well
        if (i == 1)
        {
            data[0] = density;
        }
    }

    /// <summary>The survival probability: one less the density's integral, at a flat density past the last node,
    /// floored at 0.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' default densities.</param>
    /// <param name="interpolation">The interpolation of the densities.</param>
    /// <param name="t">The time.</param>
    /// <returns>The probability.</returns>
    public static double SurvivalProbabilityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        if (t == 0.0)
        {
            return 1.0;
        }

        double integral;
        if (t <= times[^1])
        {
            integral = interpolation.Primitive(t, true);
        }
        else
        {
            // flat default density extrapolation
            integral = interpolation.Primitive(times[^1], true) + (data[^1] * (t - times[^1]));
        }

        double p = 1.0 - integral;
        return Math.Max(p, 0.0);
    }

    /// <summary>The default density: the interpolation's, flat past the last node.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' default densities.</param>
    /// <param name="interpolation">The interpolation of the densities.</param>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    public static double DefaultDensityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        if (t <= times[^1])
        {
            return interpolation.Evaluate(t, true);
        }

        // flat default density extrapolation
        return data[^1];
    }

    /// <summary>The hazard rate: the base's, the default density over the survival probability, read through the
    /// curve.</summary>
    /// <param name="times">The nodes' times, which this kind doesn't read.</param>
    /// <param name="data">The nodes' data, which this kind doesn't read.</param>
    /// <param name="interpolation">The interpolation, which this kind doesn't read.</param>
    /// <param name="t">The time.</param>
    /// <param name="curve">The curve.</param>
    /// <returns>The rate.</returns>
    public static double HazardRateImpl(
        ReadOnlySpan<double> times,
        ReadOnlySpan<double> data,
        Interpolation interpolation,
        double t,
        DefaultProbabilityTermStructure curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        return ProbabilityTraitsDetail.BaseHazardRate(curve, t);
    }

    /// <summary>The maximum date: the last node's.</summary>
    /// <param name="dates">The nodes' dates.</param>
    /// <returns>The date.</returns>
    public static Date MaxDate(ReadOnlySpan<Date> dates) => dates[^1];
}
