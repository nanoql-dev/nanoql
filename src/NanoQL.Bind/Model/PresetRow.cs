// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Model;

/// <summary>
/// A withheld preset constructor and its lookup by name: the convention set's function that gives the same
/// preset, and how the constructor's arguments map to it. The recorder writes such a constructor's call as the lookup.
/// </summary>
/// <param name="Factory">The preset as the factories hold it.</param>
/// <param name="Construction">One of the lambda's calls of the preset's constructor.</param>
/// <param name="Lookup">
/// The convention set's lookup function, or null when none takes the lambda's parameters the construction passes.
/// </param>
internal sealed record PresetRow(PresetFactory Factory, PresetConstruction Construction, CFunction? Lookup);
