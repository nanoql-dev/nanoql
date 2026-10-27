// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/utilities.hpp ql/models/marketmodels/utilities.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The result of <see cref="UtilitiesFunctions.MergeTimes"/>: QuantLib's <c>mergeTimes</c> outputs, the merged times
/// and each set's presence among them.
/// </summary>
public sealed class MergedTimes
{
    /// <summary>Initializes a new instance of the <see cref="MergedTimes"/> class.</summary>
    /// <param name="times">The merged times, sorted, each once.</param>
    /// <param name="isPresent">Each set's presence over the merged times.</param>
    public MergedTimes(ImmutableArray<double> times, ImmutableArray<ImmutableArray<bool>> isPresent)
    {
        Times = times;
        IsPresent = isPresent;
    }

    /// <summary>Gets the merged times, sorted, each once.</summary>
    public ImmutableArray<double> Times { get; }

    /// <summary>Gets each set's presence: for the i-th set, whether it holds each of the merged times.</summary>
    public ImmutableArray<ImmutableArray<bool>> IsPresent { get; }
}
