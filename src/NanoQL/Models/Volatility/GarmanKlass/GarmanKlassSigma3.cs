// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garmanklass.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>Garman and Klass's third estimator: the overnight jump beside Parkinson's.</summary>
/// <remarks>Immutable. QuantLib's <c>GarmanKlassOpenClose&lt;ParkinsonSigma&gt;</c> with a = 0.17.</remarks>
public sealed class GarmanKlassSigma3 : GarmanKlassAbstract
{
    private const double A = 0.17;
    private readonly double f;

    /// <summary>Initializes a new instance of the <see cref="GarmanKlassSigma3"/> class.</summary>
    /// <param name="y">The year fraction of an interval.</param>
    /// <param name="marketOpenFraction">The fraction of the interval the market is open.</param>
    public GarmanKlassSigma3(double y, double marketOpenFraction)
        : base(y)
    {
        f = marketOpenFraction;
    }

    /// <inheritdoc/>
    /// <returns>A volatility for each date after the first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="quoteSeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The series is empty.</exception>
    public override TimeSeries<double> Calculate(TimeSeries<IntervalPrice> quoteSeries) =>
        OpenClose(quoteSeries, YearFraction, f, A, ParkinsonSigma.Point);

    /// <inheritdoc/>
    protected override double CalculatePoint(IntervalPrice p) => ParkinsonSigma.Point(p);
}
