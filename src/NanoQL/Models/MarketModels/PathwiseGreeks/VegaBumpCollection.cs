// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/vegabumpcluster.hpp ql/models/marketmodels/pathwisegreeks/vegabumpcluster.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The clusters a model's alive pseudo-root elements are bumped in, too many to bump one by one: by default each alive
/// element alone, or each alive rate's factors together.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: its checks are computed at each call, where QuantLib caches them in <c>mutable</c> flags.
/// </para>
/// <para>
/// <b>The checks</b> (a documented divergence; <c>Divergences/VegaBumpCollectionCheckTests.cs</c>):
/// <see cref="IsFull"/> and <see cref="IsNonOverlapping"/> are true where no element fails; QuantLib's are true where
/// one fails (<c>vegabumpcluster.cpp:179</c>, <c>:218</c>), so its own default collection given as its clusters reports
/// not full, overlapping and not sensible. The default collection's flags are QuantLib's, true.
/// </para>
/// </remarks>
public sealed class VegaBumpCollection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VegaBumpCollection"/> class: each alive pseudo-root element of each
    /// step its own cluster, or each alive rate's factors one cluster.
    /// </summary>
    /// <param name="volStructure">The model.</param>
    /// <param name="allowFactorwiseBumping">Whether each factor is its own cluster.</param>
    public VegaBumpCollection(MarketModel volStructure, bool allowFactorwiseBumping = true)
    {
        ArgumentNullException.ThrowIfNull(volStructure);
        AssociatedModel = volStructure;
        int steps = volStructure.NumberOfSteps;
        int rates = volStructure.NumberOfRates;
        int factors = volStructure.NumberOfFactors;
        ImmutableArray<int> alive = volStructure.Evolution.FirstAliveRate;
        ImmutableArray<VegaBumpCluster>.Builder bumps = ImmutableArray.CreateBuilder<VegaBumpCluster>();

        for (int s = 0; s < steps; ++s)
        {
            for (int r = alive[s]; r < rates; ++r)
            {
                if (allowFactorwiseBumping)
                {
                    for (int f = 0; f < factors; ++f)
                    {
                        bumps.Add(new VegaBumpCluster(f, f + 1, r, r + 1, s, s + 1));
                    }
                }
                else
                {
                    bumps.Add(new VegaBumpCluster(0, factors, r, r + 1, s, s + 1));
                }
            }
        }

        AllBumps = bumps.ToImmutable();
    }

    /// <summary>Initializes a new instance of the <see cref="VegaBumpCollection"/> class: given clusters.</summary>
    /// <param name="allBumps">The clusters, each compatible with the model.</param>
    /// <param name="volStructure">The model.</param>
    /// <exception cref="ArgumentException">A cluster isn't compatible with the model.</exception>
    public VegaBumpCollection(IEnumerable<VegaBumpCluster> allBumps, MarketModel volStructure)
    {
        ArgumentNullException.ThrowIfNull(allBumps);
        ArgumentNullException.ThrowIfNull(volStructure);
        AllBumps = [.. allBumps];
        AssociatedModel = volStructure;
        foreach (VegaBumpCluster bump in AllBumps)
        {
            ArgumentNullException.ThrowIfNull(bump, nameof(allBumps));
            if (!bump.IsCompatible(volStructure))
            {
                throw new ArgumentException("incompatible bumps passed to VegaBumpCollection", nameof(allBumps));
            }
        }
    }

    /// <summary>Gets the number of clusters.</summary>
    public int NumberBumps => AllBumps.Length;

    /// <summary>Gets the model.</summary>
    public MarketModel AssociatedModel { get; }

    /// <summary>Gets the clusters.</summary>
    public ImmutableArray<VegaBumpCluster> AllBumps { get; }

    /// <summary>Whether every alive pseudo-root element is bumped at least once.</summary>
    /// <returns>Whether the collection is full.</returns>
    public bool IsFull()
    {
        bool[,,] v = Marks(out _);
        int numberFailures = 0;
        int steps = AssociatedModel.NumberOfSteps;
        int factors = AssociatedModel.NumberOfFactors;
        int rates = AssociatedModel.NumberOfRates;
        ImmutableArray<int> alive = AssociatedModel.Evolution.FirstAliveRate;
        for (int s = 0; s < steps; ++s)
        {
            for (int f = 0; f < factors; ++f)
            {
                for (int r = alive[s]; r < rates; ++r)
                {
                    if (!v[s, r, f])
                    {
                        ++numberFailures;
                    }
                }
            }
        }

        // no element fails
        return numberFailures == 0;
    }

    /// <summary>Whether every alive pseudo-root element is bumped at most once.</summary>
    /// <returns>Whether the clusters don't overlap.</returns>
    public bool IsNonOverlapping()
    {
        _ = Marks(out int numberFailures);

        // no element fails
        return numberFailures == 0;
    }

    /// <summary>Whether every alive pseudo-root element is bumped precisely once.</summary>
    /// <returns>Whether the collection is full and doesn't overlap.</returns>
    public bool IsSensible() => IsNonOverlapping() && IsFull();

    // Each element the clusters bump, and the number of times one is bumped again.
    private bool[,,] Marks(out int numberFailures)
    {
        var v = new bool[AssociatedModel.NumberOfSteps, AssociatedModel.NumberOfRates, AssociatedModel.NumberOfFactors];
        numberFailures = 0;
        foreach (VegaBumpCluster bump in AllBumps)
        {
            for (int f = bump.FactorBegin; f < bump.FactorEnd; ++f)
            {
                for (int r = bump.RateBegin; r < bump.RateEnd; ++r)
                {
                    for (int s = bump.StepBegin; s < bump.StepEnd; ++s)
                    {
                        if (v[s, r, f])
                        {
                            ++numberFailures;
                        }

                        v[s, r, f] = true;
                    }
                }
            }
        }

        return v;
    }
}
