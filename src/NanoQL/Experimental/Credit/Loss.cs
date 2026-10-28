// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/loss.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A loss's time and amount, ordered by time.</summary>
/// <remarks>
/// Equality and order are QuantLib's: by time alone, so two losses at one time are equal whatever their amounts.
/// </remarks>
/// <param name="Time">The loss's time.</param>
/// <param name="Amount">The loss's amount.</param>
[Experimental("NQL0001")]
public readonly record struct Loss(double Time, double Amount) : IComparable<Loss>
{
    /// <summary>Whether a loss is earlier than another.</summary>
    /// <param name="left">The first loss.</param>
    /// <param name="right">The second loss.</param>
    /// <returns><see langword="true"/> when the first's time is earlier.</returns>
    public static bool operator <(Loss left, Loss right) => left.Time < right.Time;

    /// <summary>Whether a loss is later than another.</summary>
    /// <param name="left">The first loss.</param>
    /// <param name="right">The second loss.</param>
    /// <returns><see langword="true"/> when the first's time is later.</returns>
    public static bool operator >(Loss left, Loss right) => left.Time > right.Time;

    /// <summary>Whether a loss is no later than another.</summary>
    /// <param name="left">The first loss.</param>
    /// <param name="right">The second loss.</param>
    /// <returns><see langword="true"/> when the first's time is no later.</returns>
    public static bool operator <=(Loss left, Loss right) => left.Time <= right.Time;

    /// <summary>Whether a loss is no earlier than another.</summary>
    /// <param name="left">The first loss.</param>
    /// <param name="right">The second loss.</param>
    /// <returns><see langword="true"/> when the first's time is no earlier.</returns>
    public static bool operator >=(Loss left, Loss right) => left.Time >= right.Time;

    /// <summary>Whether two losses are at one time.</summary>
    /// <param name="other">The other loss.</param>
    /// <returns><see langword="true"/> when their times are equal.</returns>
    public bool Equals(Loss other) => Time == other.Time;

    /// <inheritdoc/>
    public override int GetHashCode() => Time.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(Loss other) => Time.CompareTo(other.Time);
}
