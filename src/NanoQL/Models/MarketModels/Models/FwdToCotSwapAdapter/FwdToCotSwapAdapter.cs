// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/fwdtocotswapadapter.hpp ql/models/marketmodels/models/fwdtocotswapadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>
/// A forward-rate market model as a coterminal swap-rate one, in the frozen-coefficient approximation: its initial
/// rates the coterminal swap rates of the forward model's curve, each step's pseudo-root the coterminal Z matrix times
/// the forward model's, the expired rates' rows zero.
/// </summary>
/// <remarks>
/// Immutable (the S-mm plan's MR1 (i)): its pseudo-roots are computed at construction, and a read returns a copy. Its
/// displacements, evolution and counts are the forward model's. A pseudo-root read past the steps, which QuantLib reads
/// unchecked, is refused (MR3 (xx)).
/// </remarks>
public sealed class FwdToCotSwapAdapter : MarketModel
{
    private readonly MarketModel fwdModel;
    private readonly Matrix[] pseudoRoots;

    /// <summary>Initializes a new instance of the <see cref="FwdToCotSwapAdapter"/> class.</summary>
    /// <param name="fwdModel">The forward-rate model, its displacements equal and every rate time to its last evolution
    /// time an evolution time.</param>
    /// <exception cref="ArgumentException">The displacements differ, or the evolution skips a rate time.</exception>
    public FwdToCotSwapAdapter(MarketModel fwdModel)
    {
        ArgumentNullException.ThrowIfNull(fwdModel);
        this.fwdModel = fwdModel;
        int numberOfSteps = fwdModel.NumberOfSteps;

        ImmutableArray<double> displacements = fwdModel.Displacements;
        for (int i = 1; i < displacements.Length; ++i)
        {
            if (!(displacements[i] == displacements[0]))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{DataFormattersFunctions.Ordinal(i + 1)} displacement ({displacements[i]}) not equal to the previous ones ({displacements[0]})"),
                    nameof(fwdModel));
            }
        }

        ImmutableArray<double> rateTimes = fwdModel.Evolution.RateTimes;

        // we must ensure we step through all rateTimes
        ImmutableArray<double> evolutionTimes = fwdModel.Evolution.EvolutionTimes;
        for (int i = 0; i < rateTimes.Length && rateTimes[i] <= evolutionTimes[^1]; ++i)
        {
            if (!evolutionTimes.Contains(rateTimes[i]))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"skipping {DataFormattersFunctions.Ordinal(i + 1)} rate time"),
                    nameof(fwdModel));
            }
        }

        var cs = new LMMCurveState(rateTimes.AsSpan());
        cs.SetOnForwardRates(fwdModel.InitialRates.AsSpan());
        InitialRates = [.. cs.CoterminalSwapRates()];

        Matrix zedMatrix = SwapForwardMappings.CoterminalSwapZedMatrix(cs, displacements[0]);

        ImmutableArray<int> alive = fwdModel.Evolution.FirstAliveRate;
        pseudoRoots = new Matrix[numberOfSteps];
        for (int k = 0; k < numberOfSteps; ++k)
        {
            pseudoRoots[k] = zedMatrix * fwdModel.PseudoRoot(k);
            for (int i = 0; i < alive[k]; ++i)
            {
                pseudoRoots[k].Row(i).Clear();
            }
        }
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> InitialRates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> Displacements => fwdModel.Displacements;

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => fwdModel.Evolution;

    /// <inheritdoc/>
    public override int NumberOfRates => fwdModel.NumberOfRates;

    /// <inheritdoc/>
    public override int NumberOfFactors => fwdModel.NumberOfFactors;

    /// <inheritdoc/>
    public override int NumberOfSteps => fwdModel.NumberOfSteps;

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public override Matrix PseudoRoot(int i)
    {
        // QuantLib reads the pseudo-roots unchecked (MR3 (xx)).
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
