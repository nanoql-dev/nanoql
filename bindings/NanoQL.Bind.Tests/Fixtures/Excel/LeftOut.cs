// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>Objects an argument left out gives as none (XLR2): optional, or annotated nullable though required.</summary>
public static class LeftOut
{
    /// <summary>An optional handle and an optional generator's snapshot, each none when left out.</summary>
    /// <param name="x">A value.</param>
    /// <param name="square">A square, or null.</param>
    /// <param name="rng">A generator, or null.</param>
    /// <returns>The value, the square's side and the first draw added.</returns>
    public static double Optional(double x, Square? square = null, UserRng? rng = null) => x + (square?.Side() ?? 0) + (rng?.Next().Value ?? 0);

    /// <summary>A required square C# lets be null.</summary>
    /// <param name="square">A square, or null.</param>
    /// <returns>Its side, or 0.</returns>
    public static double Maybe(Square? square) => square?.Side() ?? 0;

    /// <summary>A required square, which an empty argument refuses.</summary>
    /// <param name="square">A square.</param>
    /// <returns>Its side.</returns>
    public static double Must(Square square) => square.Side();
}
