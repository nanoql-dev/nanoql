// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>A consumer of a <see cref="Plate"/>: withheld where nothing listed makes one.</summary>
public static class PlateUser
{
    /// <summary>The plate's size, doubled.</summary>
    /// <param name="plate">The plate.</param>
    /// <returns>Twice its size.</returns>
    public static double Use(Plate plate) => 2 * plate.Size;

    /// <summary>A size it needs no plate for, so the listed type keeps a function.</summary>
    /// <returns>A size.</returns>
    public static double Standard() => 1.0;
}
