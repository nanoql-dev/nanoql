// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.TestTypes;

/// <summary>An immutable object for the handle tests: its value and its name.</summary>
/// <param name="Value">The value.</param>
/// <param name="Name">The name.</param>
public sealed record EchoObject(double Value, string Name)
{
    /// <summary>A copy with its value scaled, a new object.</summary>
    /// <param name="factor">The factor.</param>
    /// <returns>The copy.</returns>
    public EchoObject Scaled(double factor) => this with { Value = Value * factor };
}
