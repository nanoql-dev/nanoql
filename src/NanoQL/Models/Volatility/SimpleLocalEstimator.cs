// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/simplelocalestimator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The local-estimator volatility model: at each date after the first, |ln(Sᵢ/Sᵢ₋₁)|/√y for a year fraction y between
/// quotes. Volatilities are assumed to be expressed on an annual basis.
/// </summary>
/// <remarks>
/// <para>Immutable.</para>
/// <para>
/// A documented divergence (the S-bar plan's BR5 (xxxv)): an empty series is refused, where QuantLib increments its
/// begin past its end.
/// </para>
/// </remarks>
public sealed class SimpleLocalEstimator : LocalVolatilityEstimator<double>
{
    private readonly double yearFraction;

    /// <summary>Initializes a new instance of the <see cref="SimpleLocalEstimator"/> class.</summary>
    /// <param name="y">The year fraction between quotes.</param>
    public SimpleLocalEstimator(double y)
    {
        yearFraction = y;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="quoteSeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The series is empty.</exception>
    public override TimeSeries<double> Calculate(TimeSeries<double> quoteSeries)
    {
        ArgumentNullException.ThrowIfNull(quoteSeries);
        if (quoteSeries.Empty)
        {
            throw new ArgumentException("the quote series is empty", nameof(quoteSeries));
        }

        ImmutableArray<Date> dates = quoteSeries.Dates();
        ImmutableArray<double> values = quoteSeries.Values();
        var outValues = new double[dates.Length - 1];
        for (int cur = 1; cur < dates.Length; ++cur)
        {
            int prev = cur - 1;
            outValues[prev] = Math.Abs(Math.Log(values[cur] / values[prev])) / Math.Sqrt(yearFraction);
        }

        return new TimeSeries<double>(dates[1..], outValues);
    }
}
