// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>Where a name in a set comes from: the record that defines it, and its file or QuantLib's presets.</summary>
/// <param name="Kind">The name's value kind.</param>
/// <param name="Name">The name.</param>
/// <param name="Element">The defining record's element (<c>CalendarPreset</c>, <c>Calendar</c>, <c>Alias</c>).</param>
/// <param name="Source">The record's file, or <see langword="null"/> for QuantLib's shipped presets.</param>
public sealed record ConventionOrigin(ConventionKind Kind, string Name, string Element, string? Source);
