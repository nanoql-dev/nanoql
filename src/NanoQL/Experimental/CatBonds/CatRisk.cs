// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catrisk.hpp ql/experimental/catbonds/catrisk.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A catastrophe risk: a source of simulations of its events over a period.</summary>
/// <remarks>An extension point, as in QuantLib.</remarks>
[Experimental("NQL0001")]
public abstract class CatRisk
{
    /// <summary>A new simulation of the risk's events over a period.</summary>
    /// <param name="start">The period's start.</param>
    /// <param name="end">The period's end.</param>
    /// <returns>The simulation.</returns>
    public abstract CatSimulation NewSimulation(Date start, Date end);
}
