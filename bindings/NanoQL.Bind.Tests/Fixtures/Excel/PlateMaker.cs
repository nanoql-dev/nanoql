// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>The only maker of a <see cref="Plate"/>.</summary>
public static class PlateMaker
{
    /// <summary>A plate.</summary>
    /// <param name="size">Its size.</param>
    /// <returns>The plate.</returns>
    public static Plate Make(double size) => new(size);
}
