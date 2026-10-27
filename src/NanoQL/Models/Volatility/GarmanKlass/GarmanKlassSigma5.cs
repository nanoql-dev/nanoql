// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garmanklass.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>Garman and Klass's fifth estimator, on the interval's open, close, high and low.</summary>
/// <remarks>Immutable.</remarks>
public sealed class GarmanKlassSigma5 : GarmanKlassAbstract
{
    /// <summary>Initializes a new instance of the <see cref="GarmanKlassSigma5"/> class.</summary>
    /// <param name="y">The year fraction of an interval.</param>
    public GarmanKlassSigma5(double y)
        : base(y)
    {
    }

    /// <inheritdoc/>
    protected override double CalculatePoint(IntervalPrice p) => Point(p);

    /// <summary>(u − d)²/2 − (2 ln 2 − 1) c² for u, d and c the logs of the high, the low and the close over the open.</summary>
    /// <param name="p">The interval's prices.</param>
    /// <returns>The point variance.</returns>
    internal static double Point(IntervalPrice p)
    {
        double u = Math.Log(p.High / p.Open);
        double d = Math.Log(p.Low / p.Open);
        double c = Math.Log(p.Close / p.Open);
        return (0.5 * (u - d) * (u - d)) - (((2.0 * Math.Log(2.0)) - 1.0) * c * c);
    }
}
