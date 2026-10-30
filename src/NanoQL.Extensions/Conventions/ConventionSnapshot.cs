// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// One conventions document's records, in order: what <see cref="ConventionsXml.Read(string)"/> returns and
/// <see cref="ConventionSet.With"/> layers onto a set. A name is unique within its value kind (calendars, day
/// counters, currencies), an alias's among all of them.
/// </summary>
/// <remarks>
/// The snapshot keeps its root's foreign attributes and elements, each element at its place among the records, and
/// each record's source (the file it was read from) for <see cref="ConventionSet.Provenance"/>. Two snapshots are
/// equal when they hold equal records, sources and foreign content in the same order.
/// </remarks>
public sealed class ConventionSnapshot : IEquatable<ConventionSnapshot>
{
    private readonly int hash;

    private ConventionSnapshot(
        ImmutableArray<ConventionRecord> records,
        ImmutableArray<string?> sources,
        ImmutableArray<ForeignAttribute> rootAttributes,
        ImmutableArray<(int Position, string Element)> rootElements)
    {
        Records = records;
        Sources = sources;
        RootAttributes = rootAttributes;
        RootElements = rootElements;
        var h = default(HashCode);
        for (int i = 0; i < records.Length; ++i)
        {
            h.Add(records[i]);
            h.Add(sources[i], StringComparer.Ordinal);
        }

        foreach (ForeignAttribute a in rootAttributes)
        {
            h.Add(a);
        }

        foreach ((int position, string element) in rootElements)
        {
            h.Add(position);
            h.Add(element, StringComparer.Ordinal);
        }

        hash = h.ToHashCode();
    }

    /// <summary>Gets a snapshot of no records.</summary>
    public static ConventionSnapshot Empty { get; } = new([], [], [], []);

    /// <summary>Gets the records, in order.</summary>
    public ImmutableArray<ConventionRecord> Records { get; }

    /// <summary>Gets each record's source, the file it was read from, or <see langword="null"/>; aligned with
    /// <see cref="Records"/>.</summary>
    public ImmutableArray<string?> Sources { get; }

    /// <summary>Gets the root's foreign attributes, in order.</summary>
    public ImmutableArray<ForeignAttribute> RootAttributes { get; }

    /// <summary>Gets the root's foreign elements' XML text, each with the number of records before it.</summary>
    public ImmutableArray<(int Position, string Element)> RootElements { get; }

    /// <summary>A copy with the record added last.</summary>
    /// <param name="record">The record.</param>
    /// <param name="source">The file it was read from, or <see langword="null"/>.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentException">The snapshot already holds the record's name in its value kind.</exception>
    public ConventionSnapshot Add(ConventionRecord record, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ConventionKind kind = ConventionKinds.Of(record);
        for (int i = 0; i < Records.Length; ++i)
        {
            ConventionRecord other = Records[i];
            if (other.Name == record.Name && ConventionKinds.Clash(kind, ConventionKinds.Of(other)))
            {
                throw new ArgumentException(
                    $"the name {record.Name} is defined twice{Where(Sources[i])}{Where(source)}", nameof(record));
            }
        }

        return new(Records.Add(record), Sources.Add(source), RootAttributes, RootElements);
    }

    /// <summary>A copy with a foreign element added to the root after the records so far.</summary>
    /// <param name="element">The element's XML text, in a namespace other than the conventions'.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentException">The text isn't one element in a foreign namespace.</exception>
    public ConventionSnapshot AddRootElement(string element)
    {
        _ = new ForeignContent([], [element]);
        return new(Records, Sources, RootAttributes, RootElements.Add((Records.Length, element)));
    }

    // A copy with a root element placed after the first position records (several files read into one).
    internal ConventionSnapshot InsertRootElement(int position, string element) =>
        new(Records, Sources, RootAttributes, RootElements.Add((position, element)));

    /// <summary>A copy with the root's foreign attributes replaced.</summary>
    /// <param name="attributes">The attributes, each in a namespace other than the conventions'.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentException">An attribute isn't in a foreign namespace.</exception>
    public ConventionSnapshot WithRootAttributes(IEnumerable<ForeignAttribute> attributes) =>
        new(Records, Sources, new ForeignContent(attributes, []).Attributes, RootElements);

    /// <inheritdoc/>
    public bool Equals(ConventionSnapshot? other) =>
        other is not null && (ReferenceEquals(this, other) || (hash == other.hash
            && Records.SequenceEqual(other.Records) && Sources.SequenceEqual(other.Sources, StringComparer.Ordinal)
            && RootAttributes.SequenceEqual(other.RootAttributes) && RootElements.SequenceEqual(other.RootElements)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ConventionSnapshot other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    private static string Where(string? source) => source is null ? "" : $" ({source})";
}
