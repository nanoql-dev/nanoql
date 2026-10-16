// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/extrapolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// <c>Extrapolator</c>'s modifiers, which return a modified copy of the receiver, keeping its static type, where
/// QuantLib's change the object.
/// </summary>
public static class ExtrapolatorExtensions
{
    /// <summary>A copy of the object with extrapolation enabled in subsequent calls.</summary>
    /// <typeparam name="T">The object's type.</typeparam>
    /// <param name="x">The object.</param>
    /// <param name="b">Whether to enable it; <see langword="false"/> disables it.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public static T EnableExtrapolation<T>(this T x, bool b = true)
        where T : Extrapolator
    {
        ArgumentNullException.ThrowIfNull(x);
        return (T)x.WithExtrapolation(b);
    }

    /// <summary>A copy of the object with extrapolation disabled in subsequent calls.</summary>
    /// <typeparam name="T">The object's type.</typeparam>
    /// <param name="x">The object.</param>
    /// <param name="b">Whether to disable it; <see langword="false"/> enables it.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public static T DisableExtrapolation<T>(this T x, bool b = true)
        where T : Extrapolator
    {
        ArgumentNullException.ThrowIfNull(x);
        return (T)x.WithExtrapolation(!b);
    }
}
