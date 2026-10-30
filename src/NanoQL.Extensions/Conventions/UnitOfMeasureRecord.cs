// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NanoQL.Experimental;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A unit of measure (<c>UnitOfMeasure</c>): one of QuantLib's eight presets, by its class's name, or a unit of a
/// user's own, which <see cref="NanoQL.Experimental.UnitOfMeasure"/> takes.
/// </summary>
[Experimental("NQL0001")]
public sealed record UnitOfMeasureRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="UnitOfMeasureRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public UnitOfMeasureRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the unit's name, or <see langword="null"/> to take the base's: the record's by default.</summary>
    public string? UnitName { get; init; }

    /// <summary>
    /// Gets its code, its identity: a preset's QuantLib's, or <see langword="null"/> to take the base's.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>Gets what it measures, or <see langword="null"/> to take the base's.</summary>
    public UnitOfMeasureType? UnitType { get; init; }

    /// <summary>
    /// Gets the unit of measure conversions go through when no direct one is known, or <see langword="null"/> to take
    /// the base's: a preset's only.
    /// </summary>
    public string? TriangulationUnit { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal UnitOfMeasureRecord Over(UnitOfMeasureRecord lower) =>
        this with
        {
            UnitName = UnitName ?? lower.UnitName,
            Code = Code ?? lower.Code,
            UnitType = UnitType ?? lower.UnitType,
            TriangulationUnit = TriangulationUnit ?? lower.TriangulationUnit,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        TriangulationUnit,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(UnitName), UnitName is not null),
            (nameof(Code), Code is not null),
            (nameof(UnitType), UnitType is not null),
            (nameof(TriangulationUnit), TriangulationUnit is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal UnitOfMeasureRecord Named(string name) =>
        new(name)
        {
            UnitName = UnitName,
            Code = Code,
            UnitType = UnitType,
            TriangulationUnit = TriangulationUnit,
        };
}
