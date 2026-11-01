// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>
/// A member that gained its first overload after a release: with the release's surface, the released form keeps its
/// short name and the newcomer takes the suffix.
/// </summary>
public static class Lone
{
    /// <summary>The released form.</summary>
    /// <returns>A value.</returns>
    public static double Value() => 1.0;

    /// <summary>The overload added after the release.</summary>
    /// <param name="scale">A scale.</param>
    /// <returns>A value.</returns>
    public static double Value(int scale) => scale;
}
