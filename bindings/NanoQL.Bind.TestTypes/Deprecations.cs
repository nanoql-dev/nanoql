// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.TestTypes;

/// <summary>A deprecated member, whose mark every layer carries.</summary>
public static class Deprecations
{
    /// <summary>A value, by a member nanoql deprecates.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    [Obsolete("use Echo.Double")]
    public static double Old(double x) => x;
}
