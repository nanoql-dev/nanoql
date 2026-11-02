// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A named class (D12) whose own <c>Create</c> hides its closing's, carrying its closing's other statics.</summary>
public sealed class HiddenNamed : Hidable<Linear>
{
    private HiddenNamed(double value)
        : base(value)
    {
    }

    /// <summary>Builds one of this type, as C#'s lookup binds a call through this class.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The object.</returns>
    public static new HiddenNamed Create(double value) => new(value);
}
