// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garmanklass.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>Garman and Klass's sixth estimator: the overnight jump beside the fourth.</summary>
/// <remarks>Immutable. QuantLib's <c>GarmanKlassOpenClose&lt;GarmanKlassSigma4&gt;</c> with a = 0.012.</remarks>
public sealed class GarmanKlassSigma6 : GarmanKlassAbstract
{
    private const double A = 0.012;
    private readonly double f;

    /// <summary>Initializes a new instance of the <see cref="GarmanKlassSigma6"/> class.</summary>
    /// <param name="y">The year fraction of an interval.</param>
    /// <param name="marketOpenFraction">The fraction of the interval the market is open.</param>
    public GarmanKlassSigma6(double y, double marketOpenFraction)
        : base(y)
    {
        f = marketOpenFraction;
    }

    /// <inheritdoc/>
    /// <returns>A volatility for each date after the first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="quoteSeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The series is empty.</exception>
    public override TimeSeries<double> Calculate(TimeSeries<IntervalPrice> quoteSeries) =>
        OpenClose(quoteSeries, YearFraction, f, A, GarmanKlassSigma4.Point);

    /// <inheritdoc/>
    protected override double CalculatePoint(IntervalPrice p) => GarmanKlassSigma4.Point(p);
}
