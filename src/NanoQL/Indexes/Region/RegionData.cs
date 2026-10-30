// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Indexes;

/// <summary>A region preset's data, which <see cref="ConventionProvider.ResolveRegion"/> returns.</summary>
/// <remarks>
/// The name is the region's identity, which its equality and its inflation indexes' names read, so a preset refuses
/// data that changes it; the code is configuration.
/// </remarks>
/// <param name="Name">The region's name.</param>
/// <param name="Code">Its code.</param>
public sealed record RegionData(string Name, string Code);
