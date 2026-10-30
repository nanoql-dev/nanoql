// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A record of a conventions file: one value's definition, an element named by its kind with <see cref="Name"/> and an
/// optional <see cref="Base"/> as attributes and every business field a child element.
/// </summary>
/// <remarks>
/// <para>
/// A record may name a base record of its kind and give only the fields it changes. A base of the record's own name
/// is the record it replaces in the set below it; any other base is resolved through the whole set. A field comes from
/// the record, then its base chain, then its builder's default record, then QuantLib's reference.
/// </para>
/// <para>
/// The kinds are closed: each is a sealed record of this assembly. A record keeps the foreign namespaced attributes
/// and elements it was read with (<see cref="Foreign"/>), so another tool's annotations survive a read and a write.
/// </para>
/// </remarks>
public abstract record ConventionRecord
{
    private protected ConventionRecord(string name, string? @base)
    {
        Name = ConventionNames.Checked(name, nameof(name));
        Base = @base is null ? null : ConventionNames.Checked(@base, nameof(@base));
    }

    /// <summary>Gets the record's name, unique within its value kind in a snapshot.</summary>
    public string Name { get; }

    /// <summary>Gets the name of the record this one is based on, or <see langword="null"/>.</summary>
    public string? Base { get; }

    /// <summary>Gets the foreign namespaced attributes and elements the record was read with, written back as read.</summary>
    public ForeignContent Foreign { get; init; } = ForeignContent.None;

    // The names the record's fields refer to, an instrument convention's; the other kinds' the set reads itself.
    internal virtual IEnumerable<string?> References() => [];

    // The fields the record gives, an instrument convention's, by name.
    internal virtual IEnumerable<string> Given() => [];
}
