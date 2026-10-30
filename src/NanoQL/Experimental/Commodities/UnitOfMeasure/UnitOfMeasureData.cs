// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A unit of measure preset's data, which <see cref="ConventionProvider.ResolveUnitOfMeasure"/> returns.</summary>
/// <remarks>
/// The code is the unit's identity, which its equality and the conversions' lookups read, so a preset refuses data that
/// changes it; the name, the type and the triangulation unit are configuration.
/// </remarks>
/// <param name="Name">The unit's name, such as "Barrels".</param>
/// <param name="Code">Its code, such as "BBL".</param>
/// <param name="UnitType">What it measures.</param>
/// <param name="TriangulationUnitOfMeasure">The unit conversions go through when no direct one is known, or
/// <see langword="null"/> for none.</param>
[Experimental("NQL0001")]
public sealed record UnitOfMeasureData(string Name, string Code, UnitOfMeasureType UnitType, UnitOfMeasure? TriangulationUnitOfMeasure);
