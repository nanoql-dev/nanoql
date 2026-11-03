// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Emitting;

/// <summary>What an Excel build generates (docs/excel.md, "Builds").</summary>
internal sealed record ExcelOptions
{
    /// <summary>Gets a value indicating whether the build holds the experimental API (NQL0001), or the stable API alone.</summary>
    public bool Experimental { get; init; }

    /// <summary>
    /// Gets each prefix's list: nanoql's prefix takes nanoql's unless told otherwise; a prefix absent here, or mapped
    /// to null, is generated whole, under Excel's filter.
    /// </summary>
    public IReadOnlyDictionary<string, ExcelList?> Lists { get; init; } = new Dictionary<string, ExcelList?>(StringComparer.Ordinal);

    /// <summary>Gets a value indicating whether the build is a test build, which registers <c>nqlxlStats</c>.</summary>
    public bool TestBuild { get; init; }

    /// <summary>Gets the generated classes' namespace.</summary>
    public string Namespace { get; init; } = "NanoQL.Excel.Functions";

    /// <summary>Gets the last release's <c>functions.shipped.json</c>, whose names stay frozen, or null before the first.</summary>
    public string? Shipped { get; init; }

    /// <summary>The options for nanoql's builds: nanoql's list on its prefix.</summary>
    /// <param name="experimental">Whether the build holds the experimental API.</param>
    /// <returns>The options.</returns>
    public static ExcelOptions Nanoql(bool experimental) => new()
    {
        Experimental = experimental,
        Lists = new Dictionary<string, ExcelList?>(StringComparer.Ordinal) { ["nql"] = ExcelList.Nanoql() },
    };
}
