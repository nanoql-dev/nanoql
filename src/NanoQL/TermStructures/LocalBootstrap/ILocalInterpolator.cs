// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/localbootstrap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// An interpolation factory that builds a curve a section at a time: the interpolator <see cref="LocalBootstrap"/>
/// needs, QuantLib's <c>localInterpolate</c> and <c>dataSizeAdjustment</c> (of its interpolators, only
/// <see cref="ConvexMonotone"/> has them).
/// </summary>
/// <remarks>
/// The members are instance members, as <see cref="IBootstrap.Calculate"/> holds the interpolator's instance and checks
/// it at run time, where QuantLib's template fails to compile. An extension point: an interpolator of one's own
/// implements it.
/// </remarks>
public interface ILocalInterpolator : IInterpolator
{
    /// <summary>
    /// Gets the number of nodes whose data the interpolation ignores: the local bootstrap solves one value fewer than
    /// its localisation per step by it.
    /// </summary>
    int DataSizeAdjustment { get; }

    /// <summary>Builds the interpolation over the curve's first nodes, keeping what it can of the previous one.</summary>
    /// <param name="x">The first nodes' x values, which must be sorted.</param>
    /// <param name="y">Their y values.</param>
    /// <param name="localisation">The local bootstrap's localisation.</param>
    /// <param name="previous">The previous step's interpolation, or <see langword="null"/> on the first.</param>
    /// <param name="finalSize">The curve's number of nodes.</param>
    /// <returns>The interpolation.</returns>
    Interpolation LocalInterpolate(
        ReadOnlySpan<double> x, ReadOnlySpan<double> y, int localisation, Interpolation? previous, int finalSize);
}
