// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garmanklass.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>Parkinson's estimator: the squared log range of the interval over 4 ln 2.</summary>
/// <remarks>Immutable.</remarks>
public sealed class ParkinsonSigma : GarmanKlassAbstract
{
    /// <summary>Initializes a new instance of the <see cref="ParkinsonSigma"/> class.</summary>
    /// <param name="y">The year fraction of an interval.</param>
    public ParkinsonSigma(double y)
        : base(y)
    {
    }

    /// <inheritdoc/>
    protected override double CalculatePoint(IntervalPrice p) => Point(p);

    /// <summary>(u − d)²/(4 ln 2) for u = ln(high/open) and d = ln(low/open).</summary>
    /// <param name="p">The interval's prices.</param>
    /// <returns>The point variance.</returns>
    internal static double Point(IntervalPrice p)
    {
        double u = Math.Log(p.High / p.Open);
        double d = Math.Log(p.Low / p.Open);
        return (u - d) * (u - d) / 4.0 / Math.Log(2.0);
    }
}
