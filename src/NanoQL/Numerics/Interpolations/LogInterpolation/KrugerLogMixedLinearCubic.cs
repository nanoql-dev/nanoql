// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's preset log-mixed-linear-cubic factory: Kruger's approximation of the logarithms, not monotonic, with second-derivative boundary
/// conditions of 0.
/// </summary>
/// <remarks>
/// A configuration struct holding QuantLib's two parameters, the switch point and the behavior, so that it can be a
/// type argument where QuantLib names it as one. QuantLib derives it from <c>LogMixedLinearCubic</c>: here it converts
/// to the <see cref="LogMixedLinearCubic"/> it is. QuantLib's has no default constructor, since its switch point has no
/// default, so its zero value (<c>default</c>, <c>new KrugerLogMixedLinearCubic()</c>, an unassigned field or array element) is
/// refused by every member and by the conversion with <see cref="InvalidOperationException"/>.
/// </remarks>
public readonly struct KrugerLogMixedLinearCubic : IInterpolator
{
    private readonly int n;

    // Null only in the zero value, which the constructor never makes.
    private readonly MixedInterpolationBehavior? behavior;

    /// <summary>Initializes a new instance of the <see cref="KrugerLogMixedLinearCubic"/> struct.</summary>
    /// <param name="n">The index of the switch point, one of the points.</param>
    /// <param name="behavior">Whether the two interpolations share the points' range or split it.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> is negative, or <paramref name="behavior"/> isn't a member of its enum.
    /// </exception>
    public KrugerLogMixedLinearCubic(int n, MixedInterpolationBehavior behavior = MixedInterpolationBehavior.ShareRanges)
    {
        // The constructor stores its arguments, so it refuses a negative n and a value outside the enum on entry.
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        EnumArguments.Check(behavior, nameof(behavior));
        this.n = n;
        this.behavior = behavior;
    }

    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>, as <see cref="LogMixedLinearCubic"/>'s.</summary>
    public static bool Global => LogMixedLinearCubic.Global;

    /// <summary>Gets the fewest points the interpolation takes: 3, as <see cref="LogMixedLinearCubic"/>'s.</summary>
    public static int RequiredPoints => LogMixedLinearCubic.RequiredPoints;

    /// <summary>The <see cref="LogMixedLinearCubic"/> the preset is.</summary>
    /// <param name="preset">The preset.</param>
    /// <returns>
    /// A log-mixed-linear-cubic factory with the preset's switch point and behavior, Kruger's approximation, not monotonic, and
    /// second-derivative conditions of 0.
    /// </returns>
    /// <exception cref="InvalidOperationException">The preset is its zero value, which QuantLib can't construct.</exception>
    public static implicit operator LogMixedLinearCubic(KrugerLogMixedLinearCubic preset) =>
        new(
            preset.n,
            preset.behavior ?? throw new InvalidOperationException(
                "a KrugerLogMixedLinearCubic has no default: build it with its switch point"),
            CubicInterpolationDerivativeApprox.Kruger,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);

    /// <summary>Builds the preset's <see cref="LogMixedLinearCubicInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    /// <exception cref="ArgumentException">
    /// The sizes differ, there are fewer than 3 points, the switch point isn't one of the points, a part has too few
    /// points, or a y value isn't positive.
    /// </exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        ((LogMixedLinearCubic)this).Interpolate(x, y);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    public void CheckPoints(int points) => ((LogMixedLinearCubic)this).CheckPoints(points);
}
