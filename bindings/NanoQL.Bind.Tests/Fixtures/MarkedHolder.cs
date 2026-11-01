// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>Members taking and returning a marked struct, directly and nullable: none crosses.</summary>
public static class MarkedHolder
{
    /// <summary>Takes the struct.</summary>
    /// <param name="w">The value.</param>
    /// <returns>Its X.</returns>
    public static double TakeDirect(MarkedValue w) => w.X;

    /// <summary>Takes the struct or none.</summary>
    /// <param name="w">The value.</param>
    /// <returns>Its X, or 0.</returns>
    public static double TakeNullable(MarkedValue? w) => w?.X ?? 0;

    /// <summary>Returns the struct or none.</summary>
    /// <param name="x">Its X.</param>
    /// <returns>The value.</returns>
    public static MarkedValue? GiveNullable(double x) => new MarkedValue(x);
}
