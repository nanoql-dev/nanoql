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
/// The survival-probability curve traits: the bootstrap of survival probabilities, and the formulas of
/// <see cref="InterpolatedSurvivalProbabilityCurve{TInterpolator}"/>.
/// </summary>
/// <remarks>A stateless configuration struct, as every bootstrap traits struct is.</remarks>
public readonly struct SurvivalProbability : IDefaultProbabilityTraits
{
    /// <summary>Gets the upper bound of the bootstrap's convergence loop: 50.</summary>
    public static int MaxIterations => 50;

    /// <summary>The start of the curve's data: the reference date.</summary>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The date.</returns>
    public static Date InitialDate(BootstrapState<DefaultProbabilityTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Curve.ReferenceDate;
    }

    /// <summary>The value at the reference date: 1.</summary>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The value.</returns>
    public static double InitialValue(BootstrapState<DefaultProbabilityTermStructure> c) => 1.0;

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
            return 1.0 / (1.0 + (ProbabilityTraitsDetail.AvgHazardRate * 0.25));
        }

        // extrapolate
        Date d = c.Dates[i];
        return c.Curve.SurvivalProbability(d, true);
    }

    /// <inheritdoc/>
    public static double MinValueAfter(
        int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            return c.Data[^1] / 2.0;
        }

        double dt = c.Times[i] - c.Times[i - 1];
        return c.Data[i - 1] * Math.Exp(-ProbabilityTraitsDetail.MaxHazardRate * dt);
    }

    /// <inheritdoc/>
    public static double MaxValueAfter(
        int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);

        // survival probability cannot increase
        return c.Data[i - 1];
    }

    /// <inheritdoc/>
    public static void UpdateGuess(double[] data, double p, int i)
    {
        ArgumentNullException.ThrowIfNull(data);
        data[i] = p;
    }

    /// <summary>The survival probability: the interpolation's, at the last node's hazard rate past it.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' survival probabilities.</param>
    /// <param name="interpolation">The interpolation of the probabilities.</param>
    /// <param name="t">The time.</param>
    /// <returns>The probability.</returns>
    public static double SurvivalProbabilityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        if (t <= times[^1])
        {
            return interpolation.Evaluate(t, true);
        }

        // flat hazard rate extrapolation
        double tMax = times[^1];
        double sMax = data[^1];
        double hazardMax = -interpolation.Derivative(tMax) / sMax;
        return sMax * Math.Exp(-hazardMax * (t - tMax));
    }

    /// <summary>The default density: minus the interpolation's derivative, at the last node's hazard rate past
    /// it.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' survival probabilities.</param>
    /// <param name="interpolation">The interpolation of the probabilities.</param>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    public static double DefaultDensityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        if (t <= times[^1])
        {
            return -interpolation.Derivative(t, true);
        }

        // flat hazard rate extrapolation
        double tMax = times[^1];
        double sMax = data[^1];
        double hazardMax = -interpolation.Derivative(tMax) / sMax;
        return sMax * hazardMax * Math.Exp(-hazardMax * (t - tMax));
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
