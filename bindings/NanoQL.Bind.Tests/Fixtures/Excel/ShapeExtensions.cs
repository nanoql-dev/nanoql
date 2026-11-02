// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>An extension method on the base, kept with a listed derived type (XLD33).</summary>
public static class ShapeExtensions
{
    /// <summary>Twice the area.</summary>
    /// <param name="s">The shape.</param>
    /// <returns>Twice its area.</returns>
    public static double Twice(this Shape s) => 2 * s.Area;
}
