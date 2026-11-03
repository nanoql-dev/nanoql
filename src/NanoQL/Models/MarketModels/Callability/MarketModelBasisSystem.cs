// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/marketmodelbasissystem.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The basis functions a Longstaff-Schwartz regression reads at each exercise: a node-data provider whose data are the
/// functions' values.
/// </summary>
/// <remarks>A working type and an extension point.</remarks>
[WorkingType]
public abstract class MarketModelBasisSystem : MarketModelNodeDataProvider
{
    /// <summary>The number of basis functions at each exercise, possibly different for each.</summary>
    /// <returns>One count an exercise.</returns>
    public abstract ImmutableArray<int> NumberOfFunctions();

    /// <summary>The number of data at each exercise: the basis functions'.</summary>
    /// <returns>One count an exercise.</returns>
    public override ImmutableArray<int> NumberOfData() => NumberOfFunctions();

    /// <summary>A deep copy of the basis system, its state included.</summary>
    /// <returns>The copy.</returns>
    public abstract MarketModelBasisSystem Clone();
}
