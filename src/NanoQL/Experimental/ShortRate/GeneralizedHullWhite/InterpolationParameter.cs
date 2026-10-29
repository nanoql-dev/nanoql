// SPDX-FileCopyrightText: Copyright (C) 2010 SunTrust Bank
// SPDX-FileCopyrightText: Copyright (C) 2010, 2014 Cavit Hafizoglu
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/shortrate/generalizedhullwhite.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A parameter whose value is an interpolation of its parameters over fixed times, extrapolated past them: QuantLib's
/// <c>InterpolationParameter</c>.
/// </summary>
/// <remarks>
/// Immutable, as every <see cref="Parameter"/>. QuantLib's holds an interpolation that reads its model's parameter
/// array in place and is reset onto it; here the parameter holds its times and its interpolation factory, and its value
/// at a time interpolates its own parameters, so a copy with another parameter (<see cref="Parameter.SetParam"/>)
/// interpolates that one. Each evaluation builds the interpolation; a model that reads the parameter often builds it
/// once from the parameter's current values.
/// </remarks>
[Experimental("NQL0001")]
public sealed class InterpolationParameter : Parameter
{
    private InterpolationParameter(ReadOnlySpan<double> values, InterpolatingImpl impl, Constraint constraint)
        : base(values, impl, constraint)
    {
    }

    /// <summary>A parameter interpolating its values over the given times.</summary>
    /// <typeparam name="TInterpolator">The interpolation factory's type.</typeparam>
    /// <param name="times">The times, which must be sorted.</param>
    /// <param name="values">The parameters, one per time.</param>
    /// <param name="constraint">The constraint on the parameters, or <see langword="null"/> for none.</param>
    /// <param name="interpolator">The interpolation factory.</param>
    /// <returns>The parameter.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="times"/> or <paramref name="values"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The sizes differ, or the interpolation can't be built over that many points.
    /// </exception>
    public static InterpolationParameter Create<TInterpolator>(
        IEnumerable<double> times,
        IEnumerable<double> values,
        Constraint? constraint = null,
        TInterpolator interpolator = default)
        where TInterpolator : struct, IInterpolator
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(values);
        ImmutableArray<double> x = [.. times];
        double[] y = [.. values];
        if (y.Length != x.Length)
        {
            throw new ArgumentException($"{y.Length} values given for {x.Length} times", nameof(values));
        }

        interpolator.CheckPoints(x.Length);
        return new InterpolationParameter(
            y, new InterpolatingImpl<TInterpolator>(x, interpolator), constraint ?? new NoConstraint());
    }

    // The interpolation of the current parameters over the times, extrapolation enabled.
    internal Interpolation Interpolate()
    {
        var impl = (InterpolatingImpl)Implementation();
        Vector p = Params();
        double[] y = new double[p.Size];
        for (int i = 0; i < y.Length; i++)
        {
            y[i] = p[i];
        }

        return impl.Interpolate(y);
    }

    private abstract class InterpolatingImpl : Impl
    {
        public abstract Interpolation Interpolate(ReadOnlySpan<double> parameters);

        public override double Value(ImmutableArray<double> parameters, double t) =>
            Interpolate(parameters.AsSpan()).Evaluate(t);
    }

    private sealed class InterpolatingImpl<TInterpolator>(ImmutableArray<double> times, TInterpolator interpolator)
        : InterpolatingImpl
        where TInterpolator : struct, IInterpolator
    {
        public override Interpolation Interpolate(ReadOnlySpan<double> parameters) =>
            interpolator.Interpolate(times.AsSpan(), parameters).EnableExtrapolation();
    }
}
