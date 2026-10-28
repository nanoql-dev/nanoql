// SPDX-FileCopyrightText: Copyright (C) 2015 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/interpolatedaffinehazardratecurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The affine hazard-rate curve traits: a deterministic hazard rate interpolated on the nodes, added to a one-factor
/// affine model of the default intensity, bootstrapped so that the model's curve reprices the market.
/// </summary>
/// <remarks>
/// The curve kind takes its model: the model-aware members carry the formulas (the S-xcr plan's XR3 (c)), and the
/// model-free ones refuse, as QuantLib's piecewise curve fails to build on these traits without a model. The survival
/// probability is the deterministic part's times the model's bond price at its initial intensity, the dynamics' short
/// rate at the process's x0: QuantLib squares x0 (<c>interpolatedaffinehazardratecurve.hpp:297</c>), right for its 1.22
/// √r CIR helper and wrong since 1.23 put CIR's dynamics on the CIR process, 0.9089 for 0.8769 at five years (XR8
/// (xiii); <c>Divergences/AffineHazardRateTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public readonly struct AffineHazardRate : IDefaultProbabilityTraits
{
    // QuantLib's detail::minHazardRateComp
    private const double MinHazardRateComp = -1.0;

    /// <inheritdoc/>
    public static int MaxIterations => 30;

    /// <inheritdoc/>
    public static Date InitialDate(BootstrapState<DefaultProbabilityTermStructure> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Curve.ReferenceDate;
    }

    /// <inheritdoc/>
    public static double InitialValue(BootstrapState<DefaultProbabilityTermStructure> c) => ProbabilityTraitsDetail.AvgHazardRate;

    /// <inheritdoc/>
    public static double Guess(int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
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
            return 0.0001;
        }

        Date d = c.Dates[i];
        return c.Curve.HazardRate(d, true);
    }

    /// <inheritdoc/>
    public static double MinValueAfter(int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MinElement(c.Data);
            return r / 2.0;
        }

        return MinHazardRateComp;
    }

    /// <inheritdoc/>
    public static double MaxValueAfter(int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            double r = BootstrapTraitsDetail.MaxElement(c.Data);
            return r * 2.0;
        }

        return ProbabilityTraitsDetail.MaxHazardRate;
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

    /// <summary>Refused: the curve kind takes a model.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' data.</param>
    /// <param name="interpolation">The interpolation.</param>
    /// <param name="t">The time.</param>
    /// <returns>Never.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public static double SurvivalProbabilityImpl(ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
        throw NoModel();

    /// <summary>Refused: the curve kind takes a model.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' data.</param>
    /// <param name="interpolation">The interpolation.</param>
    /// <param name="t">The time.</param>
    /// <returns>Never.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public static double DefaultDensityImpl(ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
        throw NoModel();

    /// <summary>Refused: the curve kind takes a model.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' data.</param>
    /// <param name="interpolation">The interpolation.</param>
    /// <param name="t">The time.</param>
    /// <param name="curve">The curve.</param>
    /// <returns>Never.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public static double HazardRateImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t, DefaultProbabilityTermStructure curve) =>
        throw NoModel();

    /// <inheritdoc/>
    public static Date MaxDate(ReadOnlySpan<Date> dates) => dates[^1];

    /// <summary>The survival probability: the deterministic hazard rate's survival, flat past the last node, times the
    /// model's bond price at its initial intensity.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' hazard rates.</param>
    /// <param name="interpolation">The interpolation.</param>
    /// <param name="t">The time.</param>
    /// <param name="model">The model.</param>
    /// <returns>The probability.</returns>
    public static double SurvivalProbabilityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t, OneFactorAffineModel model)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        ArgumentNullException.ThrowIfNull(model);
        double initValHR = InitialIntensity(model);
        if (t == 0.0)
        {
            return model.DiscountBond(0.0, t, initValHR);
        }

        return Math.Exp(-Integral(times, data, interpolation, t)) * model.DiscountBond(0.0, t, initValHR);
    }

    /// <summary>The default density: the deterministic hazard rate times the survival over the model's bond price,
    /// QuantLib's base structure's (XR8 (xxxviii)).</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' hazard rates.</param>
    /// <param name="interpolation">The interpolation.</param>
    /// <param name="t">The time.</param>
    /// <param name="model">The model.</param>
    /// <returns>The density.</returns>
    public static double DefaultDensityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t, OneFactorAffineModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Rate(times, data, interpolation, t) * SurvivalProbabilityImpl(times, data, interpolation, t, model)
            / model.DiscountBond(0.0, t, InitialIntensity(model));
    }

    /// <summary>The deterministic hazard rate, flat past the last node.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' hazard rates.</param>
    /// <param name="interpolation">The interpolation.</param>
    /// <param name="t">The time.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="model">The model.</param>
    /// <returns>The rate.</returns>
    public static double HazardRateImpl(
        ReadOnlySpan<double> times,
        ReadOnlySpan<double> data,
        Interpolation interpolation,
        double t,
        DefaultProbabilityTermStructure curve,
        OneFactorAffineModel model) =>
        Rate(times, data, interpolation, t);

    // The model's initial intensity, its dynamics' short rate at the process's x0, where QuantLib squares x0.
    internal static double InitialIntensity(OneFactorAffineModel model)
    {
        OneFactorModelShortRateDynamics dynamics = model.Dynamics();
        return dynamics.ShortRate(0.0, dynamics.Process.X0);
    }

    // The deterministic hazard rate's integral from 0, flat past the last node.
    internal static double Integral(ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
        t <= times[^1]
            ? interpolation.Primitive(t, true)
            : interpolation.Primitive(times[^1], true) + (data[^1] * (t - times[^1]));

    internal static double Rate(ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        ArgumentNullException.ThrowIfNull(interpolation);
        return t <= times[^1] ? interpolation.Evaluate(t, true) : data[^1];
    }

    private static NanoQLException NoModel() => new("the affine hazard-rate traits take a model: build the curve on one");
}
