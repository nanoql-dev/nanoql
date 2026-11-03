// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Model;

namespace NanoQL.Bind.Emitting;

/// <summary>A listed function the completeness check withholds, with what it lacks.</summary>
/// <param name="Function">The function.</param>
/// <param name="Name">Its Excel name.</param>
/// <param name="Lacks">The types no kept function makes, and "an engine no listed engine binds" for an unfilled engine.</param>
internal sealed record ExcelWithheld(CFunction Function, string Name, IReadOnlyList<string> Lacks);
