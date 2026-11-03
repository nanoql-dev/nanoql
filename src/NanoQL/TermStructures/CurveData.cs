// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A curve's last time, datum or date, as the traits' and the curve formulas' helpers read it, refused on an empty
/// span, where QuantLib reads an empty vector's <c>back()</c> past its end.
/// </summary>
internal static class CurveData
{
    /// <summary>The last of a curve's times or data.</summary>
    /// <param name="values">The times or data.</param>
    /// <param name="paramName">Their parameter's name.</param>
    /// <returns>The last.</returns>
    /// <exception cref="ArgumentException"><paramref name="values"/> is empty.</exception>
    public static double Last(ReadOnlySpan<double> values, string paramName) =>
        !values.IsEmpty ? values[^1] : throw new ArgumentException("the curve has no points", paramName);

    /// <summary>The last of a curve's dates.</summary>
    /// <param name="dates">The dates.</param>
    /// <param name="paramName">Their parameter's name.</param>
    /// <returns>The last.</returns>
    /// <exception cref="ArgumentException"><paramref name="dates"/> is empty.</exception>
    public static Date Last(ReadOnlySpan<Date> dates, string paramName) =>
        !dates.IsEmpty ? dates[^1] : throw new ArgumentException("the curve has no dates", paramName);
}
