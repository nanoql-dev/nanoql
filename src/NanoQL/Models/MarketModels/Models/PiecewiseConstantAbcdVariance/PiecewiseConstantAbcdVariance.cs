// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/piecewiseconstantabcdvariance.hpp ql/models/marketmodels/models/piecewiseconstantabcdvariance.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>
/// The piecewise-constant variance of the rate resetting at a given rate time under an abcd volatility: each step to
/// the reset the abcd function's variance over the step, 0 after it.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's <c>getABCD</c> out-parameters are <see cref="GetABCD"/>'s record. Rate times QuantLib
/// allocates its vectors from before checking them, an empty set among them, are refused with QuantLib's check of the
/// times.
/// </remarks>
public sealed class PiecewiseConstantAbcdVariance : PiecewiseConstantVariance
{
    private readonly double a;
    private readonly double b;
    private readonly double c;
    private readonly double d;

    /// <summary>Initializes a new instance of the <see cref="PiecewiseConstantAbcdVariance"/> class.</summary>
    /// <param name="a">The abcd function's a.</param>
    /// <param name="b">The abcd function's b.</param>
    /// <param name="c">The abcd function's c.</param>
    /// <param name="d">The abcd function's d.</param>
    /// <param name="resetIndex">The rate time the rate resets at, below the number of rate times less one.</param>
    /// <param name="rateTimes">The rate times, at least two, strictly increasing and positive.</param>
    /// <exception cref="ArgumentException">
    /// The rate times aren't strictly increasing positive times, or fewer than two, or the abcd parameters are outside
    /// their domain.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resetIndex"/> isn't below the rate times less
    /// one.</exception>
    public PiecewiseConstantAbcdVariance(double a, double b, double c, double d, int resetIndex, ReadOnlySpan<double> rateTimes)
    {
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (!(rateTimes.Length > 1))
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        if (!(resetIndex >= 0 && resetIndex < rateTimes.Length - 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetIndex),
                resetIndex,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"resetIndex ({resetIndex}) must be less than rateTimes.size()-1 ({rateTimes.Length - 1})"));
        }

        this.a = a;
        this.b = b;
        this.c = c;
        this.d = d;
        RateTimes = [.. rateTimes];
        double[] variances = new double[rateTimes.Length - 1];
        double[] volatilities = new double[rateTimes.Length - 1];
        var abcd = new AbcdFunction(a, b, c, d);
        for (int i = 0; i <= resetIndex; ++i)
        {
            double startTime = i == 0 ? 0.0 : rateTimes[i - 1];
            variances[i] = abcd.Variance(startTime, rateTimes[i], rateTimes[resetIndex]);
            double totTime = rateTimes[i] - startTime;
            volatilities[i] = Math.Sqrt(variances[i] / totTime);
        }

        Variances = ImmutableArray.Create(variances);
        Volatilities = ImmutableArray.Create(volatilities);
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> Variances { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> Volatilities { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> RateTimes { get; }

    /// <summary>The abcd function's parameters.</summary>
    /// <returns>The parameters, QuantLib's <c>getABCD</c> out-parameters.</returns>
    public AbcdCoefficients GetABCD() => new(a, b, c, d);
}
