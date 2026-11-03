// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catrisk.hpp ql/experimental/catbonds/catrisk.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A simulation of catastrophe events over a period: each call to <see cref="NextPath"/> writes the next path's events,
/// their dates and losses.
/// </summary>
/// <remarks>A working type: a simulation steps through its paths in place, one caller at a time. An extension point, as
/// in QuantLib.</remarks>
[Experimental("NQL0001")]
[WorkingType]
public abstract class CatSimulation
{
    /// <summary>Initializes a new instance of the <see cref="CatSimulation"/> class.</summary>
    /// <param name="start">The period's start.</param>
    /// <param name="end">The period's end.</param>
    protected CatSimulation(Date start, Date end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets the period's start.</summary>
    protected Date Start { get; }

    /// <summary>Gets the period's end.</summary>
    protected Date End { get; }

    /// <summary>Writes the next path's events into a list, cleared first.</summary>
    /// <param name="path">The list the events are written into.</param>
    /// <returns>Whether a path was written; <see langword="false"/> when the simulation has run out of paths.</returns>
    public abstract bool NextPath(List<KeyValuePair<Date, double>> path);
}
