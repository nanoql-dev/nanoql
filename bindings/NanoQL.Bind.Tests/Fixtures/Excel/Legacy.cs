// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>A deprecated member: a list keeping it is refused (XLD34).</summary>
public static class Legacy
{
    /// <summary>The old way.</summary>
    /// <param name="x">A value.</param>
    /// <returns>The value.</returns>
    [Obsolete("use Square")]
    public static double Old(double x) => x;
}
