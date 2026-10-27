// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/fwdperiodadapter.hpp ql/models/marketmodels/models/fwdperiodadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A forward-rate market model on short forwards as one on long forwards, each spanning a period of the short ones
/// from an offset: its curve the short model's restricted to the long rate times, its evolution the short model's to
/// the last long reset, each step's pseudo-root the Y matrix times the short model's, the expired rates' rows zero.
/// </summary>
/// <remarks>
/// <para>Immutable (the S-mm plan's MR1 (i)): its pseudo-roots are computed at construction, and a read returns a
/// copy.</para>
/// <para>
/// Two departures from QuantLib (<c>Divergences/FwdPeriodAdapterTests.cs</c>): with no displacements given, each long
/// rate's is the mean of its own period's short displacements, where QuantLib's running sum is never reset and starts
/// at the first rate, not the offset (MR3 (ii); <c>fwdperiodadapter.cpp:54-64</c>); and the Y matrix is
/// <see cref="ForwardForwardMappings.YMatrix"/>'s, the long forwards' true elasticities (MR3 (i)). A short model whose
/// evolution holds no time to the last long reset, which leaves QuantLib's adapter with steps it never computes, no
/// long forward fitting the short ones, and a pseudo-root read past the steps, which QuantLib reads unchecked, are
/// refused (MR3 (xx)).
/// </para>
/// </remarks>
public sealed class FwdPeriodAdapter : MarketModel
{
    private readonly int numberOfFactors;
    private readonly int numberOfRates;
    private readonly int numberOfSteps;
    private readonly Matrix[] pseudoRoots;

    /// <summary>Initializes a new instance of the <see cref="FwdPeriodAdapter"/> class.</summary>
    /// <param name="largeModel">The short forwards' model.</param>
    /// <param name="period">The short forwards each long one spans, at least 1.</param>
    /// <param name="offset">The first long forward's first short one, below the period.</param>
    /// <param name="newDisplacements">
    /// The long forwards' displacements: none for each period's mean, one for all, or one per long forward.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The period isn't above the offset, the displacements aren't none, one or one per long forward, or a long rate
    /// time but the last isn't an evolution time.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period"/> is below 1 or <paramref name="offset"/>
    /// negative.</exception>
    public FwdPeriodAdapter(MarketModel largeModel, int period, int offset, ReadOnlySpan<double> newDisplacements)
    {
        ArgumentNullException.ThrowIfNull(largeModel);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (!(period > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "period must  be greater than zero in fwdperiodadapter");
        }

        if (!(period > offset))
        {
            throw new ArgumentException("period must be greater than offset in fwdperiodadapter", nameof(offset));
        }

        numberOfFactors = largeModel.NumberOfFactors;
        numberOfRates = (largeModel.NumberOfRates - offset) / period;

        // QuantLib sizes its long rates from the short ones less the offset, unsigned, a wrap past them (MR3 (xx))
        if (!(offset <= largeModel.NumberOfRates && numberOfRates >= 1))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no long forward of period {period} fits the {largeModel.NumberOfRates} rates from offset {offset} in fwdperiodadapter"),
                nameof(period));
        }

        ImmutableArray<double> largeDisplacements = largeModel.Displacements;
        double[] displacements;
        if (newDisplacements.Length == 1)
        {
            displacements = [.. Enumerable.Repeat(newDisplacements[0], numberOfRates)];
        }
        else if (newDisplacements.IsEmpty)
        {
            // if not specified use average across rate: each period's own short displacements, from the offset, where
            // QuantLib's sum runs on from the first rate (MR3 (ii))
            displacements = new double[numberOfRates];
            for (int k = 0; k < displacements.Length; ++k)
            {
                double sum = 0.0;
                for (int l = 0; l < period; ++l)
                {
                    sum += largeDisplacements[offset + (k * period) + l];
                }

                displacements[k] = sum / period;
            }
        }
        else
        {
            displacements = newDisplacements.ToArray();
        }

        if (!(displacements.Length == numberOfRates))
        {
            throw new ArgumentException(
                "newDisplacements should be empty,1, or number of new rates in fwdperiodadapter", nameof(newDisplacements));
        }

        var largeCS = new LMMCurveState(largeModel.Evolution.RateTimes.AsSpan());
        largeCS.SetOnForwardRates(largeModel.InitialRates.AsSpan());

        LMMCurveState smallCS = ForwardForwardMappings.RestrictCurveState(largeCS, period, offset);

        InitialRates = [.. smallCS.ForwardRates()];

        double finalReset = smallCS.RateTimes[smallCS.NumberOfRates - 1];
        ImmutableArray<double> oldEvolutionTimes = largeModel.Evolution.EvolutionTimes;
        var newEvolutionTimes = new List<double>();
        for (int i = 0; i < oldEvolutionTimes.Length && oldEvolutionTimes[i] <= finalReset; ++i)
        {
            newEvolutionTimes.Add(oldEvolutionTimes[i]);
        }

        // QuantLib's evolution then takes the rate times' default while its steps stay none (MR3 (xx))
        if (!(newEvolutionTimes.Count > 0))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the short model's evolution has no time at or before the last long reset ({finalReset}) in fwdperiodadapter"),
                nameof(largeModel));
        }

        Evolution = new EvolutionDescription(smallCS.RateTimes.AsSpan(), [.. newEvolutionTimes]);
        numberOfSteps = newEvolutionTimes.Count;

        ImmutableArray<double> rateTimes = smallCS.RateTimes;

        // we must ensure we step through all rateTimes
        var setTimes = new HashSet<double>(Evolution.EvolutionTimes);
        for (int i = 0; i < rateTimes.Length - 1; ++i)
        {
            if (!setTimes.Contains(rateTimes[i]))
            {
                throw new ArgumentException(
                    "every new rate time except last must be an evolution time in fwdperiod adapter", nameof(largeModel));
            }
        }

        Matrix yMatrix = ForwardForwardMappings.YMatrix(largeCS, largeDisplacements.AsSpan(), displacements, period, offset);
        Displacements = ImmutableArray.Create(displacements);

        ImmutableArray<int> alive = Evolution.FirstAliveRate;
        pseudoRoots = new Matrix[numberOfSteps];
        for (int k = 0; k < numberOfSteps; ++k)
        {
            pseudoRoots[k] = yMatrix * largeModel.PseudoRoot(k);
            for (int i = 0; i < alive[k]; ++i)
            {
                pseudoRoots[k].Row(i).Clear();
            }
        }
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> InitialRates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> Displacements { get; }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution { get; }

    /// <inheritdoc/>
    public override int NumberOfRates => numberOfRates;

    /// <inheritdoc/>
    public override int NumberOfFactors => numberOfFactors;

    /// <inheritdoc/>
    public override int NumberOfSteps => numberOfSteps;

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public override Matrix PseudoRoot(int i)
    {
        // QuantLib reads the pseudo-roots unchecked (MR3 (xx)).
        if (!(i >= 0 && i < numberOfSteps))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                string.Create(
                    CultureInfo.InvariantCulture, $"the index {i} is invalid: it must be less than number of steps ({numberOfSteps})"));
        }

        return pseudoRoots[i].Clone();
    }
}
