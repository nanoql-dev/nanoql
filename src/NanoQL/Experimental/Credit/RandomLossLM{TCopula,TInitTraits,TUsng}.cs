// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomlosslatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The random spot recovery rate loss model simulation for any copula: each path's defaults and their recoveries, on a
/// spot-recovery latent model's default and recovery variables.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <typeparam name="TUsng">The uniform sequence generator, which the copula inverts.</typeparam>
/// <remarks>
/// A default's recovery is kept in 256ths, QuantLib's rounding, as a value: QuantLib's stores the count in 8 bits, so a
/// recovery of 1, 256/256, reads 0, a full loss for no loss, and its 12-bit name and day fields wrap past 4,095 (the
/// S-xcr plan's XR8 (xxv)). Bound to a basket, the model simulates the live names (XR8 (ii)). The simulation's rules
/// are <see cref="RandomLM{TCopula, TInitTraits, TUsng}"/>'s.
/// </remarks>
[Experimental("NQL0001")]
public class RandomLossLM<TCopula, TInitTraits, TUsng> : RandomLM<TCopula, TInitTraits, TUsng>
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
    where TUsng : class, ISeedableRsg<TUsng>
{
    private const double RrGranular = 1.0 / 256.0; // 2^8

    // for time inversion:
    private readonly double accuracy;
    private SpotRecoveryLatentModel<TCopula, TInitTraits> copula;

    // Default probabilities for each name at the time of the maximun
    //   horizon date. Cached for perf.
    private double[] horizonDefaultPs = [];

    /// <summary>Initializes a new instance of the <see cref="RandomLossLM{TCopula, TInitTraits, TUsng}"/>
    /// class.</summary>
    /// <param name="copula">The spot-recovery latent model.</param>
    /// <param name="nSims">The number of paths, at least 1.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295]; QuantLib's default is 2863311530.</param>
    /// <param name="accuracy">The default day's solver accuracy.</param>
    /// <param name="maxHorizon">The simulation's horizon in days, QuantLib's 4,050 by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copula"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count or the seed lies outside its range.</exception>
    public RandomLossLM(
        SpotRecoveryLatentModel<TCopula, TInitTraits> copula, int nSims, long seed, double accuracy = 1.0e-6, int maxHorizon = 4050)
        : base(nSims, seed, maxHorizon)
    {
        ArgumentNullException.ThrowIfNull(copula);
        this.copula = copula;
        this.accuracy = accuracy;
    }

    /// <inheritdoc/>
    private protected override TCopula SimulationCopula => copula.Copula;

    /// <inheritdoc/>
    private protected override void InitDates(Date maxHorizonDate)
    {
        /* Precalculate horizon time default probabilities (used to
          determine if the default took place and subsequently compute its
          event time)
        */
        Basket basket = BoundBasket;
        horizonDefaultPs = new double[basket.Size];
        for (int iName = 0; iName < basket.Size; ++iName)
        {
            horizonDefaultPs[iName] = copula.Curve(iName).DefaultProbability(maxHorizonDate, true);
        }
    }

    /// <inheritdoc/>
    private protected override void NextSample(ReadOnlySpan<double> values, Date today, List<RandomLMEvent> events)
    {
        // half the model is defaults, the other half are RRs...
        for (int iName = 0; iName < copula.Size / 2; iName++)
        {
            // ...but samples must be full
            double latentVarSample = copula.LatentVarValue(values, iName);
            double simDefaultProb = copula.CumulativeY(latentVarSample, iName);

            // If the default simulated lies before the max date:
            if (horizonDefaultPs[iName] >= simDefaultProb)
            {
                DefaultProbabilityTermStructure dfts = copula.Curve(iName);

                // compute and store default time with respect to the
                //  evaluation date:
                int dateSTride = DefaultDay(dfts, today, simDefaultProb, accuracy);

                // Determine the realized recovery rate: 'conditionalRecovery' reads
                // the pdef on the realized def event date, moved to the curve's date
                // if before it
                Date eventDate = today + new Period(dateSTride, TimeUnit.Days);
                if (eventDate < dfts.ReferenceDate)
                {
                    eventDate = dfts.ReferenceDate;
                }

                double latentRRVarSample = copula.LatentRRVarValue(values, iName);
                double recovery = copula.ConditionalRecovery(latentRRVarSample, iName, eventDate);
                events.Add(new RandomLMEvent(iName, dateSTride, RrGranular * Math.Round(recovery / RrGranular, MidpointRounding.AwayFromZero)));
            }
        }
    }

    /// <inheritdoc/>
    protected override void ResetModel()
    {
        Basket source = SourceBasket;
        copula = copula.ResetBasket(source);
        if (2 * BoundBasket.Size != copula.Size)
        {
            throw new NanoQLException("Incompatible basket and model sizes.");
        }

        PerformCalculations();
    }
}
