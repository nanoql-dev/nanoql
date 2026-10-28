// SPDX-FileCopyrightText: Copyright (C) 2012 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/differentialevolution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The differential evolution's configuration, QuantLib's <c>DifferentialEvolution::Configuration</c>.</summary>
/// <remarks>
/// <para>
/// There are seven basic strategies for creating mutant population currently implemented. Three basic crossover types
/// are also available.
/// </para>
/// <para>
/// Immutable, with QuantLib's defaults: each <c>With…</c> method returns a modified copy, where QuantLib's setters
/// change the configuration and return it (the S-core plan's CD12). QuantLib's <c>seed</c> isn't here: the seed is
/// the optimizer's own argument, required (<see cref="DifferentialEvolution"/>). Its vectors compare by reference, so
/// two configurations are equal only when they share them.
/// </para>
/// </remarks>
public sealed record DifferentialEvolutionConfiguration
{
    /// <summary>Gets the strategy for creating the mutant population; QuantLib's default, the best member with
    /// jitter.</summary>
    public DifferentialEvolutionStrategy Strategy { get; private init; } = DifferentialEvolutionStrategy.BestMemberWithJitter;

    /// <summary>Gets the crossover type; normal by default.</summary>
    public DifferentialEvolutionCrossoverType CrossoverType { get; private init; } = DifferentialEvolutionCrossoverType.Normal;

    /// <summary>Gets the number of population members; 100 by default.</summary>
    public int PopulationMembers { get; private init; } = 100;

    /// <summary>Gets the step size weight; 0.2 by default.</summary>
    public double StepsizeWeight { get; private init; } = 0.2;

    /// <summary>Gets the crossover probability; 0.9 by default.</summary>
    public double CrossoverProbability { get; private init; } = 0.9;

    /// <summary>Gets a value indicating whether a mutant outside the bounds is pulled back inside them; by default it
    /// is.</summary>
    public bool ApplyBounds { get; private init; } = true;

    /// <summary>Gets a value indicating whether the crossover probabilities adapt; by default they don't.</summary>
    public bool CrossoverIsAdaptive { get; private init; }

    /// <summary>Gets the initial population; empty by default, for one drawn between the bounds.</summary>
    public ImmutableArray<ImmutableArray<double>> InitialPopulation { get; private init; } = [];

    /// <summary>Gets the upper bound; empty by default, for the constraint's.</summary>
    public ImmutableArray<double> UpperBound { get; private init; } = [];

    /// <summary>Gets the lower bound; empty by default, for the constraint's.</summary>
    public ImmutableArray<double> LowerBound { get; private init; } = [];

    /// <summary>Returns a copy that applies the bounds, or not.</summary>
    /// <param name="b">Whether to apply them.</param>
    /// <returns>The copy.</returns>
    public DifferentialEvolutionConfiguration WithBounds(bool b = true) => this with { ApplyBounds = b };

    /// <summary>Returns a copy with the crossover probability.</summary>
    /// <param name="p">The probability, in [0, 1].</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException"><paramref name="p"/> is outside [0, 1].</exception>
    public DifferentialEvolutionConfiguration WithCrossoverProbability(double p)
    {
        if (!(p >= 0.0 && p <= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Crossover probability ({p}) must be in [0,1] range"),
                nameof(p));
        }

        return this with { CrossoverProbability = p };
    }

    /// <summary>Returns a copy with the number of population members and no initial population.</summary>
    /// <param name="n">The number, positive.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException"><paramref name="n"/> isn't positive.</exception>
    public DifferentialEvolutionConfiguration WithPopulationMembers(int n)
    {
        if (!(n > 0))
        {
            throw new ArgumentException("Positive number of population members required", nameof(n));
        }

        return this with { PopulationMembers = n, InitialPopulation = [] };
    }

    /// <summary>Returns a copy with the initial population, whose size becomes the number of population members.</summary>
    /// <param name="c">The members, which it copies; at least one.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="c"/> is empty, which QuantLib accepts and whose minimization then reads the front of an empty
    /// population (the S-core plan's CR7 (xxxvi)).
    /// </exception>
    public DifferentialEvolutionConfiguration WithInitialPopulation(IEnumerable<Vector> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        ImmutableArray<ImmutableArray<double>> population = [.. c.Select(v => ImmutableArray.Create(v.AsSpan()))];
        if (population.IsEmpty)
        {
            throw new ArgumentException("Positive number of population members required", nameof(c));
        }

        return this with { InitialPopulation = population, PopulationMembers = population.Length };
    }

    /// <summary>Returns a copy with the upper bound, which overrides the constraint's.</summary>
    /// <param name="u">The bound, which it copies.</param>
    /// <returns>The copy.</returns>
    public DifferentialEvolutionConfiguration WithUpperBound(ReadOnlySpan<double> u) => this with { UpperBound = [.. u] };

    /// <summary>Returns a copy with the lower bound, which overrides the constraint's.</summary>
    /// <param name="l">The bound, which it copies.</param>
    /// <returns>The copy.</returns>
    public DifferentialEvolutionConfiguration WithLowerBound(ReadOnlySpan<double> l) => this with { LowerBound = [.. l] };

    /// <summary>Returns a copy whose crossover probabilities adapt, or not.</summary>
    /// <param name="b">Whether they adapt.</param>
    /// <returns>The copy.</returns>
    public DifferentialEvolutionConfiguration WithAdaptiveCrossover(bool b = true) => this with { CrossoverIsAdaptive = b };

    /// <summary>Returns a copy with the step size weight.</summary>
    /// <param name="w">The weight, in [0, 2].</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException"><paramref name="w"/> is outside [0, 2].</exception>
    public DifferentialEvolutionConfiguration WithStepsizeWeight(double w)
    {
        if (!(w >= 0 && w <= 2.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Step size weight ({w}) must be in [0,2] range"),
                nameof(w));
        }

        return this with { StepsizeWeight = w };
    }

    /// <summary>Returns a copy with the crossover type.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="t"/> isn't one of the type's members.</exception>
    public DifferentialEvolutionConfiguration WithCrossoverType(DifferentialEvolutionCrossoverType t)
    {
        if (!Enum.IsDefined(t))
        {
            throw new ArgumentOutOfRangeException(
                nameof(t), t, string.Create(CultureInfo.InvariantCulture, $"Unknown crossover type ({(int)t})"));
        }

        return this with { CrossoverType = t };
    }

    /// <summary>Returns a copy with the strategy.</summary>
    /// <param name="s">The strategy.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="s"/> isn't one of the type's members.</exception>
    public DifferentialEvolutionConfiguration WithStrategy(DifferentialEvolutionStrategy s)
    {
        if (!Enum.IsDefined(s))
        {
            throw new ArgumentOutOfRangeException(
                nameof(s), s, string.Create(CultureInfo.InvariantCulture, $"Unknown strategy ({(int)s})"));
        }

        return this with { Strategy = s };
    }
}
