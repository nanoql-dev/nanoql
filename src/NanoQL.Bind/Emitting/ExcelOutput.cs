// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Emitting;

/// <summary>An Excel build's outputs, rendered while the inputs' metadata is open.</summary>
/// <param name="Selection">The build's functions.</param>
/// <param name="Files">The generated C#, each file by its name.</param>
/// <param name="Templates">The repro templates' JSON.</param>
/// <param name="Manifest">The build's <c>functions.json</c>.</param>
/// <param name="Summary">The counts the build prints.</param>
internal sealed record ExcelOutput(ExcelSelection Selection, IReadOnlyDictionary<string, string> Files, string Templates, string Manifest, string Summary)
{
    /// <summary>Gets the functions the add-in registers: the generated and its own.</summary>
    public int Registered { get; init; }
}
