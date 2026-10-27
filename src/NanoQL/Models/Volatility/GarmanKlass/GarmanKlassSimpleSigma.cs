// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garmanklass.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>The simple Garman-Klass estimator: the squared log of the interval's close over its open.</summary>
/// <remarks>Immutable.</remarks>
public sealed class GarmanKlassSimpleSigma : GarmanKlassAbstract
{
    /// <summary>Initializes a new instance of the <see cref="GarmanKlassSimpleSigma"/> class.</summary>
    /// <param name="y">The year fraction of an interval.</param>
    public GarmanKlassSimpleSigma(double y)
        : base(y)
    {
    }

    /// <inheritdoc/>
    protected override double CalculatePoint(IntervalPrice p) => Point(p);

    /// <summary>c² for c = ln(close/open).</summary>
    /// <param name="p">The interval's prices.</param>
    /// <returns>The point variance.</returns>
    internal static double Point(IntervalPrice p)
    {
        double c = Math.Log(p.Close / p.Open);
        return c * c;
    }
}
