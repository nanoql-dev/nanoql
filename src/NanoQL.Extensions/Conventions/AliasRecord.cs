// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An alias (<c>Alias</c>): another name for a value, its base, in the base's value kind: FpML's and ISDA's codes
/// (<c>ACT/360</c>, <c>EUTA</c>), QuantLib's own aliases (<c>Thirty360.ISMA</c>), or a firm's own codes.
/// </summary>
/// <remarks>
/// An alias of a business-day convention names a member of the core's enumeration, which a conventions file may
/// write in its place wherever the schema takes one.
/// </remarks>
public sealed record AliasRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="AliasRecord"/> class.</summary>
    /// <param name="name">The alias.</param>
    /// <param name="base">The name it stands for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="base"/> is null.</exception>
    public AliasRecord(string name, string @base)
        : base(name, @base ?? throw new ArgumentNullException(nameof(@base)))
    {
    }
}
