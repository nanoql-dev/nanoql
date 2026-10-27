// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.hpp ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// Pseudo-root bumps that each move one instrument's implied volatility by one percent and leave the others fixed:
/// the instruments' one-percent bumps projected orthogonally to each other, an instrument too correlated with the
/// others discarded.
/// </summary>
/// <remarks>Immutable (the S-mmp plan's MMPR1 (ii)): <see cref="GetVegaBumps"/> returns a new array.</remarks>
public sealed class OrthogonalizedBumpFinder
{
    private readonly VolatilityBumpInstrumentJacobian derivativesProducer;
    private readonly double multiplierCutOff;
    private readonly double tolerance;

    /// <summary>Initializes a new instance of the <see cref="OrthogonalizedBumpFinder"/> class.</summary>
    /// <param name="bumps">The vega bumps, on the model the instruments' implied volatilities are read in.</param>
    /// <param name="swaptions">The swaptions.</param>
    /// <param name="caps">The caps.</param>
    /// <param name="multiplierCutOff">An instrument whose bump grows by more than this factor is discarded.</param>
    /// <param name="tolerance">An instrument whose projection before scaling is below this is discarded.</param>
    public OrthogonalizedBumpFinder(
        VegaBumpCollection bumps,
        IEnumerable<VolatilityBumpInstrumentJacobianSwaption> swaptions,
        IEnumerable<VolatilityBumpInstrumentJacobianCap> caps,
        double multiplierCutOff,
        double tolerance)
    {
        derivativesProducer = new VolatilityBumpInstrumentJacobian(bumps, swaptions, caps);
        this.multiplierCutOff = multiplierCutOff;
        this.tolerance = tolerance;
    }

    /// <summary>
    /// The bumps, by step and by valid instrument, each of the pseudo-root's size: precisely the bumps
    /// <see cref="PathwiseVegasAccountingEngine"/> takes.
    /// </summary>
    /// <returns>A new array: a list per step, a bump per valid instrument.</returns>
    /// <exception cref="ArgumentException">An instrument's pseudo-derivative refuses its inputs.</exception>
    /// <exception cref="NanoQLException">A cap's implied volatility can't be solved.</exception>
    public Matrix[][] GetVegaBumps()
    {
        var projector = new OrthogonalProjections(derivativesProducer.GetAllOnePercentBumps(), multiplierCutOff, tolerance);

        int numberRestrictedBumps = projector.NumberValidVectors;

        MarketModel marketmodel = derivativesProducer.InputBumps.AssociatedModel;
        EvolutionDescription evolution = marketmodel.Evolution;

        int numberSteps = evolution.NumberOfSteps;
        int numberRates = evolution.NumberOfRates;
        int factors = marketmodel.NumberOfFactors;

        // recall that the bumps: We do the outermost vector by time step and inner one by which vega.
        var theBumps = new Matrix[numberSteps][];
        for (int i = 0; i < numberSteps; ++i)
        {
            theBumps[i] = new Matrix[numberRestrictedBumps];
            for (int j = 0; j < numberRestrictedBumps; ++j)
            {
                theBumps[i][j] = new Matrix(numberRates, factors, 0.0);
            }
        }

        ImmutableArray<VegaBumpCluster> bumpClusters = derivativesProducer.InputBumps.AllBumps;

        int bumpIndex = 0;

        for (int instrument = 0; instrument < projector.ValidVectors.Length; ++instrument)
        {
            if (projector.ValidVectors[instrument])
            {
                ImmutableArray<double> vector = projector.GetVector(instrument);
                for (int cluster = 0; cluster < bumpClusters.Length; ++cluster)
                {
                    VegaBumpCluster c = bumpClusters[cluster];
                    double magnitude = vector[cluster];

                    for (int step = c.StepBegin; step < c.StepEnd; ++step)
                    {
                        for (int rate = c.RateBegin; rate < c.RateEnd; ++rate)
                        {
                            for (int factor = c.FactorBegin; factor < c.FactorEnd; ++factor)
                            {
                                theBumps[step][bumpIndex][rate, factor] = magnitude;
                            }
                        }
                    }
                }

                ++bumpIndex;
            }
        }

        return theBumps;
    }
}
