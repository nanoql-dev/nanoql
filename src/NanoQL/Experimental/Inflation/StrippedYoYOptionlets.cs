// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/yoyoptionletstripper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Year-on-year optionlet volatilities stripped along each strike: a volatility curve per strike, what QuantLib's
/// <c>YoYOptionletStripper</c> holds after its <c>initialize</c>.
/// </summary>
/// <remarks>
/// <see cref="Slice(Date)"/> is QuantLib's <c>slice</c>, each curve read at the date through its own observation lag;
/// <see cref="Slice(double)"/> reads each curve at a time on its own clock, with no lag, which
/// <see cref="KInterpolatedYoYOptionletVolatilitySurface{TInterpolator}"/> reads.
/// </remarks>
[Experimental("NQL0001")]
public sealed class StrippedYoYOptionlets
{
    /// <summary>Initializes a new instance of the <see cref="StrippedYoYOptionlets"/> class.</summary>
    /// <param name="strikes">The strikes, increasing.</param>
    /// <param name="curves">The volatility curve stripped at each strike, one per strike.</param>
    /// <exception cref="ArgumentNullException">An argument or a curve is null.</exception>
    /// <exception cref="ArgumentException">The curves aren't one per strike, or there is no strike.</exception>
    public StrippedYoYOptionlets(IEnumerable<double> strikes, IEnumerable<YoYOptionletVolatilitySurface> curves)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(curves);
        Strikes = [.. strikes];
        Curves = [.. curves];
        if (Curves.Any(c => c is null))
        {
            throw new ArgumentNullException(nameof(curves), "null volatility curve");
        }

        if (Strikes.IsEmpty || Curves.Length != Strikes.Length)
        {
            throw new ArgumentException(
                $"one curve per strike needed: {Curves.Length} curves for {Strikes.Length} strikes", nameof(curves));
        }
    }

    /// <summary>Gets the strikes.</summary>
    public ImmutableArray<double> Strikes { get; }

    /// <summary>Gets the lowest strike.</summary>
    public double MinStrike => Strikes[0];

    /// <summary>Gets the highest strike.</summary>
    public double MaxStrike => Strikes[^1];

    /// <summary>Gets the volatility curve stripped at each strike, one per strike.</summary>
    public ImmutableArray<YoYOptionletVolatilitySurface> Curves { get; }

    /// <summary>The volatilities at a date, each strike's curve read at the date and its strike: QuantLib's
    /// <c>slice</c>.</summary>
    /// <param name="d">The date, observed through each curve's lag.</param>
    /// <returns>The strikes and the volatilities.</returns>
    /// <exception cref="NanoQLException">A curve can't give a volatility at the date.</exception>
    public (ImmutableArray<double> Strikes, ImmutableArray<double> Volatilities) Slice(Date d) =>
        (Strikes, [.. Strikes.Select((k, i) => Curves[i].Volatility(d, k))]);

    /// <summary>The volatilities at a time, each strike's curve read at the time and its strike.</summary>
    /// <param name="t">The time on the curves' clock, already observed: no lag is applied.</param>
    /// <returns>The strikes and the volatilities.</returns>
    /// <exception cref="NanoQLException">A curve can't give a volatility at the time.</exception>
    public (ImmutableArray<double> Strikes, ImmutableArray<double> Volatilities) Slice(double t) =>
        (Strikes, [.. Strikes.Select((k, i) => Curves[i].Volatility(t, k))]);
}
