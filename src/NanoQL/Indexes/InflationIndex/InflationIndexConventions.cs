// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// A zero or quoted year-on-year inflation index preset's conventions, which
/// <see cref="ConventionProvider.ResolveInflationIndex"/> returns: QuantLib's values for the preset, those it takes as
/// arguments (<see cref="AUCPI"/>'s frequency and revised flag) among them.
/// </summary>
/// <param name="Region">The region the index is published for.</param>
/// <param name="Revised">Whether its fixings are revised.</param>
/// <param name="Frequency">The publication frequency.</param>
/// <param name="AvailabilityLag">The availability lag.</param>
/// <param name="Currency">The index's currency.</param>
public sealed record InflationIndexConventions(
    Region Region,
    bool Revised,
    Frequency Frequency,
    Period AvailabilityLag,
    Currency Currency);
