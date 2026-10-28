// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomdefaultlatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The default-only latent model simulation with fixed recoveries: each path's defaults, by name, on a default latent
/// model's variables.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <typeparam name="TUsng">The uniform sequence generator, which the copula inverts.</typeparam>
/// <remarks>
/// Bound to a basket, the model simulates the basket's live names on the latent model restricted to their rows, their
/// recoveries kept, and tranches against the restated amounts: QuantLib's simulates the defaulted names too against
/// the inception amounts (<c>randomdefaultlatentmodel.hpp:352-353</c>, <c>:927-936</c>; the S-xcr plan's XR8 (ii)).
/// The simulation's rules are <see cref="RandomLM{TCopula, TInitTraits, TUsng}"/>'s.
/// </remarks>
[Experimental("NQL0001")]
public class RandomDefaultLM<TCopula, TInitTraits, TUsng> : RandomLM<TCopula, TInitTraits, TUsng>
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
    where TUsng : class, ISeedableRsg<TUsng>
{
    // for time inversion:
    private readonly double accuracy;
    private DefaultLatentModel<TCopula, TInitTraits> model;
    private ImmutableArray<double> recoveries;

    // Default probabilities for each name at the time of the maximun
    //   horizon date. Cached for perf.
    private double[] horizonDefaultPs = [];

    /// <summary>Initializes a new instance of the <see cref="RandomDefaultLM{TCopula, TInitTraits, TUsng}"/> class
    /// on a default latent model and its names' recoveries.</summary>
    /// <param name="model">The default latent model.</param>
    /// <param name="recoveries">The names' recoveries; <see langword="null"/> or empty for 0 each.</param>
    /// <param name="nSims">The number of paths, at least 1.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295]; QuantLib's default is 2863311530.</param>
    /// <param name="accuracy">The default day's solver accuracy.</param>
    /// <param name="maxHorizon">The simulation's horizon in days, QuantLib's 4,050 by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count or the seed lies outside its range.</exception>
    public RandomDefaultLM(
        DefaultLatentModel<TCopula, TInitTraits> model,
        IReadOnlyList<double>? recoveries,
        int nSims,
        long seed,
        double accuracy = 1.0e-6,
        int maxHorizon = 4050)
        : base(nSims, seed, maxHorizon)
    {
        ArgumentNullException.ThrowIfNull(model);
        this.model = model;
        this.recoveries = recoveries is null || recoveries.Count == 0 ? [.. new double[model.Size]] : [.. recoveries];
        this.accuracy = accuracy;
    }

    /// <summary>Initializes a new instance of the <see cref="RandomDefaultLM{TCopula, TInitTraits, TUsng}"/> class
    /// on a constant-loss latent model, its recoveries the model's.</summary>
    /// <param name="model">The constant-loss latent model.</param>
    /// <param name="nSims">The number of paths, at least 1.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295]; QuantLib's default is 2863311530.</param>
    /// <param name="accuracy">The default day's solver accuracy.</param>
    /// <param name="maxHorizon">The simulation's horizon in days, QuantLib's 4,050 by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count or the seed lies outside its range.</exception>
    public RandomDefaultLM(
        ConstantLossLatentModel<TCopula, TInitTraits> model, int nSims, long seed, double accuracy = 1.0e-6, int maxHorizon = 4050)
        : base(nSims, seed, maxHorizon)
    {
        ArgumentNullException.ThrowIfNull(model);
        this.model = model;
        recoveries = model.Recoveries;
        this.accuracy = accuracy;
    }

    /// <summary>A name's recovery, deterministic.</summary>
    /// <param name="d">The date, unread.</param>
    /// <param name="iName">The name's position in the bound basket.</param>
    /// <param name="defaultKey">The key, unread.</param>
    /// <returns>The recovery.</returns>
    public override double ExpectedRecovery(Date d, int iName, DefaultProbKey defaultKey) => recoveries[iName];

    /// <inheritdoc/>
    private protected override TCopula SimulationCopula => model.Copula;

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
            horizonDefaultPs[iName] = Curve(basket, iName).DefaultProbability(maxHorizonDate, true);
        }
    }

    /// <inheritdoc/>
    private protected override void NextSample(ReadOnlySpan<double> values, Date today, List<RandomLMEvent> events)
    {
        Basket basket = BoundBasket;

        // starts with no events
        for (int iName = 0; iName < model.Size; iName++)
        {
            double latentVarSample = model.LatentVarValue(values, iName);
            double simDefaultProb = model.CumulativeY(latentVarSample, iName);

            // If the default simulated lies before the max date:
            if (horizonDefaultPs[iName] >= simDefaultProb)
            {
                // compute and store default time with respect to the
                //  evaluation date:
                int dateSTride = DefaultDay(Curve(basket, iName), today, simDefaultProb, accuracy);
                events.Add(new RandomLMEvent(iName, dateSTride, recoveries[iName]));
            }
        }
    }

    /// <inheritdoc/>
    protected override void ResetModel()
    {
        Basket source = SourceBasket;
        model = model.ResetBasket(source);
        if (recoveries.Length != source.Size)
        {
            throw new NanoQLException("Incompatible basket and recovery sizes.");
        }

        if (source.ModelRows.Length != source.Size)
        {
            recoveries = [.. source.ModelRows.Select(i => recoveries[i])];
        }

        PerformCalculations();
    }

    private static DefaultProbabilityTermStructure Curve(Basket basket, int iName) =>
        basket.Pool.Get(basket.Pool.Names[iName]).DefaultProbability(basket.DefaultKeys[iName]);
}
