// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>A type whose only maker needs one already: nothing makes the first, so the check withholds it.</summary>
public sealed class Selfish
{
    /// <summary>Initializes a new instance of the <see cref="Selfish"/> class from another.</summary>
    /// <param name="other">The other.</param>
    public Selfish(Selfish other) => Value = other.Value + 1;

    /// <summary>Gets the value.</summary>
    public double Value { get; }

    /// <summary>The first value, which needs no instance, so the listed type keeps a function.</summary>
    /// <returns>The value.</returns>
    public static double First() => 1.0;
}
