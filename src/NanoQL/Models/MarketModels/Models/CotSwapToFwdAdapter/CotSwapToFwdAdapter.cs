// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/cotswaptofwdadapter.hpp ql/models/marketmodels/models/cotswaptofwdadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>
/// A coterminal swap-rate market model as a forward-rate one, in the frozen-coefficient approximation: its initial
/// rates the forwards of the coterminal model's curve, each step's pseudo-root the inverse of the coterminal Z matrix
/// times the coterminal model's, the expired rates' rows zero.
/// </summary>
/// <remarks>
/// Immutable: its pseudo-roots are computed at construction, and a read returns a copy. Its displacements, evolution
/// and counts are the coterminal model's. A pseudo-root read past the steps, which QuantLib reads unchecked, is
/// refused.
/// </remarks>
public sealed class CotSwapToFwdAdapter : MarketModel
{
    private readonly MarketModel coterminalModel;
    private readonly Matrix[] pseudoRoots;

    /// <summary>Initializes a new instance of the <see cref="CotSwapToFwdAdapter"/> class.</summary>
    /// <param name="ctModel">The coterminal swap-rate model, its displacements equal and every rate time to its last
    /// evolution time an evolution time.</param>
    /// <exception cref="ArgumentException">The displacements differ, or the evolution skips a rate time.</exception>
    public CotSwapToFwdAdapter(MarketModel ctModel)
    {
        ArgumentNullException.ThrowIfNull(ctModel);
        coterminalModel = ctModel;
        int numberOfSteps = ctModel.NumberOfSteps;

        ImmutableArray<double> displacements = ctModel.Displacements;
        for (int i = 1; i < displacements.Length; ++i)
        {
            if (!(displacements[i] == displacements[0]))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{DataFormattersFunctions.Ordinal(i + 1)} displacement ({displacements[i]}) not equal to the previous ones ({displacements[0]})"),
                    nameof(ctModel));
            }
        }

        ImmutableArray<double> rateTimes = ctModel.Evolution.RateTimes;

        // we must ensure we step through all rateTimes
        ImmutableArray<double> evolutionTimes = ctModel.Evolution.EvolutionTimes;
        for (int i = 0; i < rateTimes.Length && rateTimes[i] <= evolutionTimes[^1]; ++i)
        {
            if (!evolutionTimes.Contains(rateTimes[i]))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"skipping {DataFormattersFunctions.Ordinal(i + 1)} rate time"),
                    nameof(ctModel));
            }
        }

        var cs = new CoterminalSwapCurveState(rateTimes.AsSpan());
        cs.SetOnCoterminalSwapRates(ctModel.InitialRates.AsSpan());
        InitialRates = [.. cs.ForwardRates()];

        Matrix zedMatrix = SwapForwardMappings.CoterminalSwapZedMatrix(cs, displacements[0]);
        Matrix invertedZedMatrix = MatrixFunctions.Inverse(zedMatrix);

        ImmutableArray<int> alive = ctModel.Evolution.FirstAliveRate;
        pseudoRoots = new Matrix[numberOfSteps];
        for (int k = 0; k < numberOfSteps; ++k)
        {
            pseudoRoots[k] = invertedZedMatrix * ctModel.PseudoRoot(k);
            for (int i = 0; i < alive[k]; ++i)
            {
                pseudoRoots[k].Row(i).Clear();
            }
        }
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> InitialRates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> Displacements => coterminalModel.Displacements;

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => coterminalModel.Evolution;

    /// <inheritdoc/>
    public override int NumberOfRates => coterminalModel.NumberOfRates;

    /// <inheritdoc/>
    public override int NumberOfFactors => coterminalModel.NumberOfFactors;

    /// <inheritdoc/>
    public override int NumberOfSteps => coterminalModel.NumberOfSteps;

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public override Matrix PseudoRoot(int i)
    {
        // QuantLib reads the pseudo-roots unchecked.
        if (!(i >= 0 && i < pseudoRoots.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                string.Create(
                    CultureInfo.InvariantCulture, $"the index {i} is invalid: it must be less than number of steps ({pseudoRoots.Length})"));
        }

        return pseudoRoots[i].Clone();
    }
}
