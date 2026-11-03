// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>The convex-monotone interpolation factory and traits: QuantLib's <c>ConvexMonotone</c>.</summary>
/// <remarks>
/// A configuration struct: its zero value is QuantLib's default construction, a quadraticity of 0.3, a monotonicity of
/// 0.7 and positivity forced. Global, it needs two points, and its data hold one value fewer than a local bootstrap's
/// instruments (QuantLib's <c>dataSizeAdjustment</c>), the first node's value being ignored. A local interpolator (<see
/// cref="ILocalInterpolator"/>): it builds the local bootstrap's curve a section at a time, each later interpolation
/// keeping the earlier sections' helpers.
/// </remarks>
public readonly struct ConvexMonotone : ILocalInterpolator
{
    // Stored so that zero reads as QuantLib's defaults: null for 0.3 and 0.7, and positivity forced unless refused.
    private readonly double? quadraticity;
    private readonly double? monotonicity;
    private readonly bool notForcePositive;

    /// <summary>Initializes a new instance of the <see cref="ConvexMonotone"/> struct.</summary>
    /// <param name="quadraticity">The weight of the quadratic form, in [0, 1].</param>
    /// <param name="monotonicity">The monotonicity, in [0, 1].</param>
    /// <param name="forcePositive">Whether to keep the forwards non-negative.</param>
    /// <remarks>As QuantLib's factory, it checks nothing: the interpolation refuses a parameter outside [0, 1].</remarks>
    public ConvexMonotone(double quadraticity = 0.3, double monotonicity = 0.7, bool forcePositive = true)
    {
        this.quadraticity = quadraticity;
        this.monotonicity = monotonicity;
        notForcePositive = !forcePositive;
    }

    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>.</summary>
    public static bool Global => true;

    /// <summary>Gets the fewest points the interpolation takes: 2.</summary>
    public static int RequiredPoints => 2;

    /// <summary>Gets the weight of the quadratic form.</summary>
    public double Quadraticity => quadraticity ?? 0.3;

    /// <summary>Gets the monotonicity.</summary>
    public double Monotonicity => monotonicity ?? 0.7;

    /// <summary>Gets a value indicating whether the forwards are kept non-negative.</summary>
    public bool ForcePositive => !notForcePositive;

    /// <summary>Gets the data's size less the local bootstrap's instruments: 1, the first node's value ignored.</summary>
    public int DataSizeAdjustment => 1;

    /// <summary>Builds a <see cref="ConvexMonotoneInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The sections' averages, one per x value, the first ignored.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">The sizes differ; there are fewer than two points; or the monotonicity or
    /// the quadraticity lies outside [0, 1].</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        new ConvexMonotoneInterpolation(x, y, Quadraticity, Monotonicity, ForcePositive);

    /// <summary>
    /// Builds the interpolation over a local bootstrap's first nodes: QuantLib's <c>localInterpolate</c>. The first call,
    /// over one node more than the localisation, starts afresh; each later one keeps the previous interpolation's
    /// sections but its last. The last section is flat at its average until the curve has all its nodes.
    /// </summary>
    /// <param name="x">The first nodes' x values, which must be sorted.</param>
    /// <param name="y">Their sections' averages, the first ignored.</param>
    /// <param name="localisation">The local bootstrap's localisation.</param>
    /// <param name="previous">The previous step's interpolation, or <see langword="null"/> on the first.</param>
    /// <param name="finalSize">The curve's number of nodes.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">A later call's previous interpolation isn't a convex-monotone one; the sizes
    /// differ; or a parameter is out of its range.</exception>
    public Interpolation LocalInterpolate(
        ReadOnlySpan<double> x, ReadOnlySpan<double> y, int localisation, Interpolation? previous, int finalSize)
    {
        int length = x.Length;
        bool flatFinalPeriod = length != finalSize;
        if (length - localisation == 1)
        {
            // the first time this function is called
            return new ConvexMonotoneInterpolation(x, y, Quadraticity, Monotonicity, ForcePositive, flatFinalPeriod);
        }

        // QuantLib casts the previous interpolation to its own type
        if (previous is not ConvexMonotoneInterpolation interp)
        {
            throw new ArgumentException("the previous interpolation isn't a convex-monotone one", nameof(previous));
        }

        return new ConvexMonotoneInterpolation(
            x, y, Quadraticity, Monotonicity, ForcePositive, flatFinalPeriod, interp.ExistingHelpers());
    }

    /// <inheritdoc/>
    public void CheckPoints(int points)
    {
        Interpolation.CheckRequiredPoints(points, RequiredPoints);
        ConvexMonotoneInterpolation.CheckConfiguration(Quadraticity, Monotonicity);
    }
}
