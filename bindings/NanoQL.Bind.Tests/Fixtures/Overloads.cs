// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>Overloads for the names' rules: a C signature two forms share crosses once, the span preferred.</summary>
public static class Overloads
{
    /// <summary>A sum over a span.</summary>
    /// <param name="x">The values.</param>
    /// <returns>Their sum.</returns>
    public static double Sum(ReadOnlySpan<double> x)
    {
        double s = 0;
        foreach (double v in x)
        {
            s += v;
        }

        return s;
    }

    /// <summary>A sum over a sequence, the same C signature as the span's.</summary>
    /// <param name="x">The values.</param>
    /// <returns>Their sum.</returns>
    public static double Sum(IEnumerable<double> x) => x.Sum();

    /// <summary>A sum over an immutable array, the same C signature again.</summary>
    /// <param name="x">The values.</param>
    /// <returns>Their sum.</returns>
    public static double Sum(ImmutableArray<double> x) => x.Sum();

    /// <summary>A date moved by days.</summary>
    /// <param name="d">The date.</param>
    /// <param name="days">The days.</param>
    /// <returns>The date moved.</returns>
    public static Date Move(Date d, int days) => d + days;

    /// <summary>A date moved by a period.</summary>
    /// <param name="d">The date.</param>
    /// <param name="p">The period.</param>
    /// <returns>The date moved.</returns>
    public static Date Move(Date d, Period p) => d + p;

    /// <summary>A date moved by a period and days, telling it from the others needs both.</summary>
    /// <param name="d">The date.</param>
    /// <param name="p">The period.</param>
    /// <param name="days">The days.</param>
    /// <returns>The date moved.</returns>
    public static Date Move(Date d, Period p, int days) => d + p + days;
}
