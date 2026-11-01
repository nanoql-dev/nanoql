// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/riskynotional.hpp ql/experimental/catbonds/riskynotional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A path of a cat bond's notional: the fraction of the original notional left, from the reductions added in
/// date order.</summary>
/// <remarks>A working type (XR5's form): a notional risk writes each simulated path into it in place.</remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class NotionalPath
{
    // QuantLib's Date() for the full notional at the beginning: before every date
    private readonly List<KeyValuePair<Date?, double>> notionalRate = [new(null, 1.0)];

    /// <summary>The fraction of the original notional left on a date: the last reduction's on or before it.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The fraction.</returns>
    public double NotionalRate(Date date)
    {
        int i = 0;

        // TODO do we take notional after reductions or before?
        for (; i < notionalRate.Count && (notionalRate[i].Key is not Date d || d <= date); ++i)
        {
        }

        return notionalRate[i - 1].Value;
    }

    /// <summary>Clears the reductions, leaving the full notional.</summary>
    public void Reset() => notionalRate.RemoveRange(1, notionalRate.Count - 1);

    /// <summary>Adds a reduction, after the others.</summary>
    /// <param name="date">The reduction's date.</param>
    /// <param name="newRate">The fraction of the original notional left from it.</param>
    public void AddReduction(Date date, double newRate) => notionalRate.Add(new KeyValuePair<Date?, double>(date, newRate));

    /// <summary>The loss: the fraction of the original notional the last reduction leaves out.</summary>
    /// <returns>The loss.</returns>
    public double Loss() => 1.0 - notionalRate[^1].Value;
}
