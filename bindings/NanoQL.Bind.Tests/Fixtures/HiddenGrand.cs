// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A class two levels below the generic base declaring the statics: it carries none of them.</summary>
public sealed class HiddenGrand : HiddenOverride
{
    /// <summary>Initializes a new instance of the <see cref="HiddenGrand"/> class.</summary>
    /// <param name="value">The value.</param>
    public HiddenGrand(double value)
        : base(value)
    {
    }
}
