// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>Generic methods: one closed at its one class constraint, one whose constructor constraint keeps it withheld.</summary>
public static class GenericMembers
{
    /// <summary>Returns the day counter it takes, of its own type in C#.</summary>
    /// <typeparam name="T">The day counter's type.</typeparam>
    /// <param name="x">The day counter.</param>
    /// <returns>The day counter.</returns>
    public static T Same<T>(T x)
        where T : DayCounter => x;

    /// <summary>A new cost function of a type with a parameterless constructor: no type closes it.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <returns>A new one.</returns>
    public static T Make<T>()
        where T : CostFunction, new() => new();
}
