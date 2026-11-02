// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>Optional parameters of types that don't cross: left at their defaults when null by default, else withheld.</summary>
public static class Optionals
{
    /// <summary>A value with a caller's function and a line search, each null by default.</summary>
    /// <param name="x">The value.</param>
    /// <param name="transform">The caller's function, or null.</param>
    /// <param name="lineSearch">A line search, or null.</param>
    /// <returns>The value.</returns>
    public static double Scaled(double x, Func<double, double>? transform = null, LineSearch? lineSearch = null) => transform?.Invoke(x) ?? x;

    /// <summary>A value with a BCL struct defaulted: withheld, as the default isn't null.</summary>
    /// <param name="x">The value.</param>
    /// <param name="span">A time span.</param>
    /// <returns>The value.</returns>
    public static double Spanned(double x, TimeSpan span = default) => x + span.TotalDays;

    /// <summary>A value with a caller's function it requires: withheld.</summary>
    /// <param name="x">The value.</param>
    /// <param name="transform">The caller's function.</param>
    /// <returns>The value.</returns>
    public static double Required(double x, Func<double, double> transform) => transform(x);
}
