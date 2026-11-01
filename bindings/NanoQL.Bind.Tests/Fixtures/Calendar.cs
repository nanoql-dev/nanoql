// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A user's class named like one of nanoql's: each layer keeps the two apart.</summary>
/// <param name="name">The name.</param>
public sealed class Calendar(string name)
{
    /// <summary>Gets the name.</summary>
    public string Name { get; } = name;

    /// <summary>Whether nanoql's calendar of the same name holds a date as a business day, a member taking nanoql's.</summary>
    /// <param name="calendar">nanoql's calendar.</param>
    /// <param name="d">The date.</param>
    /// <returns>Whether it does.</returns>
    public static bool Holds(NanoQL.Time.Calendar calendar, NanoQL.Time.Date d) => calendar.IsBusinessDay(d);

    /// <summary>A walk: a stable class's member returning a class experimental through its interface.</summary>
    /// <returns>The walk.</returns>
    public static Walk MakeWalk() => new();
}
