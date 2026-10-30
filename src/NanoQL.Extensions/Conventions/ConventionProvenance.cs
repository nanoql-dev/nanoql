// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/ConventionSetTests.cs (nanoql's own report of where a set's conventions come from)

using System.Collections.Immutable;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A set's provenance report (<see cref="ConventionSet.Provenance"/>): where each name comes from, and a warning for
/// each record that overrides no name and that nothing references, as a likely misspelling.
/// </summary>
public sealed record ConventionProvenance
{
    /// <summary>Initializes a new instance of the <see cref="ConventionProvenance"/> class.</summary>
    /// <param name="origins">Each name's origin.</param>
    /// <param name="warnings">The warnings.</param>
    /// <exception cref="ArgumentNullException">A sequence is null.</exception>
    public ConventionProvenance(IEnumerable<ConventionOrigin> origins, IEnumerable<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(warnings);
        Origins = [.. origins];
        Warnings = [.. warnings];
    }

    /// <summary>Gets each name's origin, by kind and name.</summary>
    public ImmutableArray<ConventionOrigin> Origins { get; }

    /// <summary>Gets the warnings.</summary>
    public ImmutableArray<string> Warnings { get; }

    /// <inheritdoc/>
    public bool Equals(ConventionProvenance? other) =>
        other is not null && Origins.SequenceEqual(other.Origins) && Warnings.SequenceEqual(other.Warnings, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        RecordLists.Add(ref hash, Origins);
        RecordLists.Add(ref hash, Warnings);
        return hash.ToHashCode();
    }
}
