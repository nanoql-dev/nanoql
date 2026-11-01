// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.TestTypes.Collisions;

/// <summary>A user's class named like one of nanoql's: each layer keeps the two apart.</summary>
/// <param name="name">The name.</param>
public sealed class Calendar(string name)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Calendar"/> class named after a walk: a stable class's experimental
    /// constructor, which the opt-in adds.
    /// </summary>
    /// <param name="walk">The walk.</param>
    public Calendar(Walk walk)
        : this(walk is null ? "no walk" : "walk")
    {
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; } = name;

    /// <summary>nanoql's calendar of this one's name, from QuantLib's convention set: a member returning nanoql's.</summary>
    /// <returns>nanoql's calendar.</returns>
    public NanoQL.Time.Calendar Nanoqls() => NanoQL.Extensions.Conventions.ConventionSet.QuantLib.Calendar(Name);

    /// <summary>A walk: a stable class's experimental member, attached on the opt-in before the walk's class is defined.</summary>
    /// <returns>The walk.</returns>
    public static Walk MakeWalk() => new();
}
