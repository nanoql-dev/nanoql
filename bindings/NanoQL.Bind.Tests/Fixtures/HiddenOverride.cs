// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A class over a closed generic base that overrides one member and hides another; no static of its base's.</summary>
public class HiddenOverride : Hidable<Cubic>
{
    /// <summary>Initializes a new instance of the <see cref="HiddenOverride"/> class.</summary>
    /// <param name="value">The value.</param>
    public HiddenOverride(double value)
        : base(value)
    {
    }

    /// <summary>Twice the value, overridden: its call sites name the base's declaration.</summary>
    /// <returns>The value doubled.</returns>
    public override double Twice() => base.Twice();

    /// <summary>Half the value, hidden: a call through this class binds it.</summary>
    /// <returns>The value halved.</returns>
    public new double Half() => Value * 0.5;
}
