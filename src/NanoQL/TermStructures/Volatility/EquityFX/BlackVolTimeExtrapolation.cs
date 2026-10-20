// SPDX-FileCopyrightText: Copyright (C) 2025 AcadiaSoft Inc.
// SPDX-FileCopyrightText: Copyright (C) 2026 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.hpp ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// The time extrapolation of Black volatility structures beyond their last time, for a strike-dependent surface and
/// for an at-the-money curve, by one of <see cref="BlackVolTimeExtrapolationType"/>'s strategies.
/// </summary>
public static class BlackVolTimeExtrapolation
{
    /// <summary>A surface's variance at a time beyond its last one, at a strike.</summary>
    /// <param name="type">The extrapolation strategy.</param>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="times">The surface's times.</param>
    /// <param name="varianceSurface">The surface's variance at a time and strike.</param>
    /// <returns>The extrapolated variance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="varianceSurface"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The strategy needs more times than given (one for <see cref="BlackVolTimeExtrapolationType.FlatVolatility"/>,
    /// two for <see cref="BlackVolTimeExtrapolationType.LinearVariance"/>), or the time or the times don't suit the
    /// linear extrapolation.
    /// </exception>
    /// <exception cref="NanoQLException">The linear extrapolation's variances decrease.</exception>
    /// <remarks>
    /// QuantLib reads <c>times.back()</c> of an empty list in the flat strategy, and <c>times[N-2]</c> of a list of
    /// one time in the linear one, both undefined (<c>blackvoltimeextrapolation.cpp:39</c>, <c>:49-50</c>); nanoql
    /// refuses them, the second with the text QuantLib's curve overload refuses it with (<c>:87</c>).
    /// </remarks>
    public static double ExtrapolatedVariance(
        BlackVolTimeExtrapolationType type,
        double t,
        double strike,
        ReadOnlySpan<double> times,
        Func<double, double, double> varianceSurface)
    {
        ArgumentNullException.ThrowIfNull(varianceSurface);
        switch (type)
        {
            case BlackVolTimeExtrapolationType.FlatVolatility:
                RequireOneTime(times);
                return Max0(varianceSurface(times[^1], strike)) / times[^1] * t;
            case BlackVolTimeExtrapolationType.UseInterpolator:
                return Max0(varianceSurface(t, strike));
            case BlackVolTimeExtrapolationType.LinearVariance:
            {
                RequireTwoTimes(times);
                int n = times.Length;
                double t1 = times[n - 2], t2 = times[n - 1];
                double var1 = varianceSurface(t1, strike), var2 = varianceSurface(t2, strike);
                return LinearExtrapolation(t, t1, t2, var1, var2);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "unknown extrapolation type");
        }
    }

    /// <summary>A curve's variance at a time beyond its last one.</summary>
    /// <param name="type">The extrapolation strategy.</param>
    /// <param name="t">The time.</param>
    /// <param name="times">The curve's times.</param>
    /// <param name="varianceCurve">The curve's variance at a time.</param>
    /// <returns>The extrapolated variance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="varianceCurve"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The strategy needs more times than given (one for <see cref="BlackVolTimeExtrapolationType.FlatVolatility"/>,
    /// two for <see cref="BlackVolTimeExtrapolationType.LinearVariance"/>), or the time or the times don't suit the
    /// linear extrapolation.
    /// </exception>
    /// <exception cref="NanoQLException">The linear extrapolation's variances decrease.</exception>
    /// <remarks>
    /// QuantLib reads <c>times.back()</c> of an empty list in the flat strategy, undefined
    /// (<c>blackvoltimeextrapolation.cpp:44</c>); nanoql refuses it.
    /// </remarks>
    public static double ExtrapolatedVariance(
        BlackVolTimeExtrapolationType type, double t, ReadOnlySpan<double> times, Func<double, double> varianceCurve)
    {
        ArgumentNullException.ThrowIfNull(varianceCurve);
        switch (type)
        {
            case BlackVolTimeExtrapolationType.FlatVolatility:
                RequireOneTime(times);
                return Max0(varianceCurve(times[^1])) / times[^1] * t;
            case BlackVolTimeExtrapolationType.UseInterpolator:
                return Max0(varianceCurve(t));
            case BlackVolTimeExtrapolationType.LinearVariance:
            {
                RequireTwoTimes(times);
                int n = times.Length;
                double t1 = times[n - 2], t2 = times[n - 1];
                double var1 = varianceCurve(t1), var2 = varianceCurve(t2);
                return LinearExtrapolation(t, t1, t2, var1, var2);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "unknown extrapolation type");
        }
    }

    private static double LinearExtrapolation(double t, double t1, double t2, double v1, double v2)
    {
        if (!(t > 0.0))
        {
            throw new ArgumentException("t must be greater than 0.0", nameof(t));
        }

        if (!(t > t2))
        {
            throw new ArgumentException("t must be greater than times[1]", nameof(t));
        }

        if (!(t2 > t1))
        {
            throw new ArgumentException("times must be sorted", "times");
        }

        if (!(v2 >= v1))
        {
            throw new NanoQLException("variances must be non-decreasing");
        }

        return v1 + ((t - t1) * (v2 - v1) / (t2 - t1));
    }

    private static void RequireOneTime(ReadOnlySpan<double> times)
    {
        if (times.IsEmpty)
        {
            throw new ArgumentException("at least one time required for volatility extrapolation", nameof(times));
        }
    }

    private static void RequireTwoTimes(ReadOnlySpan<double> times)
    {
        if (!(times.Length >= 2))
        {
            throw new ArgumentException("at least two times required for volatility extrapolation", nameof(times));
        }
    }

    // std::max(x, 0.0), which keeps a NaN.
    private static double Max0(double x) => x < 0.0 ? 0.0 : x;
}
