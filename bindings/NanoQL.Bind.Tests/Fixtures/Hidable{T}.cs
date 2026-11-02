// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A generic base with a static factory, a constant and instance members, which its derived classes hide or inherit.</summary>
/// <typeparam name="T">A type argument its closings name.</typeparam>
public class Hidable<T>
{
    /// <summary>A constant, a static of this type alone.</summary>
    public const int Limit = 100;

    /// <summary>Initializes a new instance of the <see cref="Hidable{T}"/> class.</summary>
    /// <param name="value">The value.</param>
    protected Hidable(double value) => Value = value;

    /// <summary>Gets the value.</summary>
    public double Value { get; }

    /// <summary>Builds one of this type.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The object.</returns>
    public static Hidable<T> Create(double value) => new(value);

    /// <summary>Twice the value, which a derived class may override.</summary>
    /// <returns>The value doubled.</returns>
    public virtual double Twice() => 2 * Value;

    /// <summary>Half the value, which a derived class may hide.</summary>
    /// <returns>The value halved.</returns>
    public double Half() => Value / 2;
}
