// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/constantestimator.hpp ql/models/volatility/constantestimator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The constant-estimator volatility model: at each date past the first <c>size</c>, the dispersion of the
/// <c>size</c> values before it. Volatilities are assumed to be expressed on an annual basis.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; calibration changes nothing, as QuantLib's is empty. The dispersion is QuantLib's,
/// √(Σu²/n − (Σu)²/(n(n + 1))), whose intent QuantLib doesn't document; it is kept as QuantLib's design.
/// </para>
/// <para>
/// A documented divergence: a window longer than the series is refused, where QuantLib advances past the series' end.
/// </para>
/// </remarks>
public sealed class ConstantEstimator : VolatilityCompositor
{
    private readonly int size;

    /// <summary>Initializes a new instance of the <see cref="ConstantEstimator"/> class.</summary>
    /// <param name="size">The number of values each volatility reads.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public ConstantEstimator(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        this.size = size;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="volatilitySeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The series is shorter than the window.</exception>
    public override TimeSeries<double> Calculate(TimeSeries<double> volatilitySeries)
    {
        ArgumentNullException.ThrowIfNull(volatilitySeries);
        if (size > volatilitySeries.Size)
        {
            throw new ArgumentException(
                $"the window of {size} values is longer than the series of {volatilitySeries.Size}", nameof(volatilitySeries));
        }

        ImmutableArray<double> u = volatilitySeries.Values();
        ImmutableArray<Date> dates = volatilitySeries.Dates();
        var outDates = new List<Date>(volatilitySeries.Size - size);
        var outValues = new List<double>(volatilitySeries.Size - size);

        // ICK.  This could probably be made a lot more efficient
        for (int i = size; i < volatilitySeries.Size; i++)
        {
            double sumu2 = 0.0, sumu = 0.0;
            for (int j = i - size; j < i; j++)
            {
                sumu += u[j];
                sumu2 += u[j] * u[j];
            }

            double s = Math.Sqrt((sumu2 / size) - (sumu * sumu / size / (size + 1)));
            outDates.Add(dates[i]);
            outValues.Add(s);
        }

        return new TimeSeries<double>(outDates, outValues);
    }

    /// <inheritdoc/>
    /// <returns>This estimator: calibration changes nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="volatilitySeries"/> is <see langword="null"/>.</exception>
    public override VolatilityCompositor Calibrate(TimeSeries<double> volatilitySeries)
    {
        ArgumentNullException.ThrowIfNull(volatilitySeries);
        return this;
    }
}
