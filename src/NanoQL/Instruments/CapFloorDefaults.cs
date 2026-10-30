// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeCapFloor"/> takes for an option it isn't given, which
/// <see cref="ConventionProvider.ResolveCapFloorDefaults"/> returns: QuantLib's. The leg's schedule is its index's,
/// through <see cref="MakeVanillaSwap"/>'s floating leg and that builder's defaults, so it isn't a default here.
/// </summary>
/// <param name="FirstCapletExcluded">Whether the first caplet is excluded, with no forward start or effective date
/// given.</param>
public sealed record CapFloorDefaults(bool FirstCapletExcluded);
