// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolutiondescription.hpp ql/models/marketmodels/evolutiondescription.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Models;

/// <summary>
/// A market-model evolution's description: the rate times defining the rates evolved, the evolution times at which
/// the rates need to be known, and which rates are relevant at each step.
/// </summary>
/// <remarks>
/// <para>
/// There are n + 1 rate times, the payment and reset times of n forward rates, and any number of evolution times. The
/// relevant rates of the i-th step run from <c>RelevanceRates[i].First</c> to <c>RelevanceRates[i].Second</c>, by
/// default 0 and n.
/// </para>
/// <para>
/// Immutable. QuantLib's default constructor, which leaves the number of rates uninitialized, isn't ported. Its refusal
/// of a last evolution time past the last fixing time names the time before the one it checks; the text here names the
/// one it checks (<c>Divergences/MarketModelTextTests.cs</c>).
/// </para>
/// </remarks>
public sealed class EvolutionDescription
{
    /// <summary>Initializes a new instance of the <see cref="EvolutionDescription"/> class.</summary>
    /// <param name="rateTimes">The rate times: at least two, strictly increasing, the first greater than zero.</param>
    /// <param name="evolutionTimes">
    /// The evolution times, strictly increasing and positive, the last at most the last fixing time; empty for every
    /// rate time but the last.
    /// </param>
    /// <param name="relevanceRates">The relevant rates of each step; empty for 0 and n at every step.</param>
    /// <exception cref="ArgumentException">
    /// The times aren't strictly increasing positive times, the last evolution time is past the last fixing time, or
    /// the relevant rates aren't one per step.
    /// </exception>
    public EvolutionDescription(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> evolutionTimes = default,
        ReadOnlySpan<(int First, int Second)> relevanceRates = default)
    {
        NumberOfRates = rateTimes.IsEmpty ? 0 : rateTimes.Length - 1;
        ReadOnlySpan<double> evolution = evolutionTimes.IsEmpty && !rateTimes.IsEmpty ? rateTimes[..^1] : evolutionTimes;
        double[] taus = new double[NumberOfRates];
        UtilitiesFunctions.CheckIncreasingTimesAndCalculateTaus(rateTimes, taus);
        UtilitiesFunctions.CheckIncreasingTimes(evolution);
        int numberOfSteps = evolution.Length;

        if (!(evolution[^1] <= rateTimes[^2]))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The last evolution time ({evolution[^1]}) is past the last fixing time ({rateTimes[^2]})"),
                nameof(evolutionTimes));
        }

        if (relevanceRates.IsEmpty)
        {
            var all = new (int First, int Second)[numberOfSteps];
            Array.Fill(all, (0, NumberOfRates));
            RelevanceRates = ImmutableArray.Create(all);
        }
        else
        {
            if (!(relevanceRates.Length == numberOfSteps))
            {
                throw new ArgumentException("relevanceRates / evolutionTimes mismatch", nameof(relevanceRates));
            }

            RelevanceRates = [.. relevanceRates];
        }

        RateTimes = [.. rateTimes];
        RateTaus = ImmutableArray.Create(taus);
        EvolutionTimes = [.. evolution];

        int[] firstAlive = new int[numberOfSteps];
        double currentEvolutionTime = 0.0;
        int firstAliveRate = 0;
        for (int j = 0; j < numberOfSteps; ++j)
        {
            while (rateTimes[firstAliveRate] <= currentEvolutionTime)
            {
                ++firstAliveRate;
            }

            firstAlive[j] = firstAliveRate;
            currentEvolutionTime = evolution[j];
        }

        FirstAliveRate = ImmutableArray.Create(firstAlive);
    }

    /// <summary>Gets the rate times.</summary>
    public ImmutableArray<double> RateTimes { get; }

    /// <summary>Gets the rates' accrual periods.</summary>
    public ImmutableArray<double> RateTaus { get; }

    /// <summary>Gets the evolution times.</summary>
    public ImmutableArray<double> EvolutionTimes { get; }

    /// <summary>Gets each step's first alive rate: the first rate whose time is past the step's start.</summary>
    public ImmutableArray<int> FirstAliveRate { get; }

    /// <summary>Gets each step's relevant rates.</summary>
    public ImmutableArray<(int First, int Second)> RelevanceRates { get; }

    /// <summary>Gets the number of rates, one less than the rate times.</summary>
    public int NumberOfRates { get; }

    /// <summary>Gets the number of steps, the evolution times'.</summary>
    public int NumberOfSteps => EvolutionTimes.Length;
}
