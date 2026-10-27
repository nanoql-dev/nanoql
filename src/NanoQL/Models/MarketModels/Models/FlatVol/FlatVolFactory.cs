// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/flatvol.hpp ql/models/marketmodels/models/flatvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>
/// The factory of <see cref="FlatVol"/> models on a yield curve: the initial rates the curve's simple forwards, the
/// volatilities interpolated linearly in the rates' fixing times and displaced, and the exponential correlation made
/// time-homogeneous.
/// </summary>
/// <remarks>
/// Immutable (the S-mm plan's MR1 (i)): it holds its curve (R1), and <see cref="Create"/> returns a new model. QuantLib's
/// factory observes its curve's handle; here a new curve is a new factory.
/// </remarks>
public sealed class FlatVolFactory : MarketModelFactory
{
    private readonly double longTermCorrelation;
    private readonly double beta;
    private readonly LinearInterpolation volatility;
    private readonly YieldTermStructure yieldCurve;
    private readonly double displacement;

    /// <summary>Initializes a new instance of the <see cref="FlatVolFactory"/> class.</summary>
    /// <param name="longTermCorrelation">The exponential correlation's long-term correlation.</param>
    /// <param name="beta">The exponential correlation's decay.</param>
    /// <param name="times">The volatilities' times, sorted, at least two.</param>
    /// <param name="vols">The volatilities, one per time.</param>
    /// <param name="yieldCurve">The curve the initial rates are read from.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <exception cref="ArgumentException">The volatilities aren't one per time, or there are fewer than two.</exception>
    public FlatVolFactory(
        double longTermCorrelation,
        double beta,
        ReadOnlySpan<double> times,
        ReadOnlySpan<double> vols,
        YieldTermStructure yieldCurve,
        double displacement)
    {
        ArgumentNullException.ThrowIfNull(yieldCurve);
        this.longTermCorrelation = longTermCorrelation;
        this.beta = beta;
        this.yieldCurve = yieldCurve;
        this.displacement = displacement;
        volatility = new LinearInterpolation(times, vols);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The model's inputs are refused (<see cref="FlatVol"/>).</exception>
    /// <exception cref="NanoQLException">A rate's fixing time is outside the volatilities' times.</exception>
    public override MarketModel Create(EvolutionDescription evolution, int numberOfFactors)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        ImmutableArray<double> rateTimes = evolution.RateTimes;
        int numberOfRates = rateTimes.Length - 1;

        double[] initialRates = new double[numberOfRates];
        for (int i = 0; i < numberOfRates; ++i)
        {
            initialRates[i] = yieldCurve.ForwardRate(rateTimes[i], rateTimes[i + 1], Compounding.Simple).Rate;
        }

        double[] displacedVolatilities = new double[numberOfRates];
        for (int i = 0; i < numberOfRates; ++i)
        {
            double vol = volatility.Evaluate(rateTimes[i]); // to be changed
            displacedVolatilities[i] = initialRates[i] * vol / (initialRates[i] + displacement);
        }

        double[] displacements = new double[numberOfRates];
        Array.Fill(displacements, displacement);

        Matrix correlations = ExpCorrelationsFunctions.ExponentialCorrelations(
            evolution.RateTimes.AsSpan(), longTermCorrelation, beta);
        var corr = new TimeHomogeneousForwardCorrelation(correlations, rateTimes.AsSpan());
        return new FlatVol(displacedVolatilities, corr, evolution, numberOfFactors, initialRates, displacements);
    }
}
