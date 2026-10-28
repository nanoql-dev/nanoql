// SPDX-FileCopyrightText: Copyright (C) 2012 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/differentialevolution.hpp ql/math/optimization/differentialevolution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The differential evolution optimization method.</summary>
/// <remarks>
/// <para>
/// The algorithm and strategy names are taken from Price, K., Storn, R., 1997. Differential Evolution - A Simple and
/// Efficient Heuristic for Global Optimization over Continuous Spaces. Journal of Global Optimization, Kluwer Academic
/// Publishers, 1997, Vol. 11, pp. 341 - 359.
/// </para>
/// <para>
/// Immutable: each minimization draws from a new Mersenne Twister built from the seed, so a second minimization
/// repeats the first's draws, where QuantLib's optimizer keeps its generator and continues its stream (the S-core
/// plan's CR2 (c); <c>Divergences/DifferentialEvolutionTests.cs</c>); its working population and adaptive weights live
/// in the call. The best member is moved to the front as MSVC's <c>std::partial_sort(first, first + 1, last)</c>
/// moves it, a scan swapping each member that costs less than the front's into the front (CD5), so the population's
/// order, which the next generation's shuffles read, is QuantLib's.
/// </para>
/// </remarks>
public sealed class DifferentialEvolution : OptimizationMethod
{
    private readonly long seed;

    /// <summary>Initializes a new instance of the <see cref="DifferentialEvolution"/> class.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="seed">The Mersenne Twister's seed, in [1, 4294967295]; QuantLib's 0, the clock, is refused.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public DifferentialEvolution(DifferentialEvolutionConfiguration configuration, long seed)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Seeds.Check(seed);
        Configuration = configuration;
        this.seed = seed;
    }

    /// <summary>Gets the configuration.</summary>
    public DifferentialEvolutionConfiguration Configuration { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// A bound or a member of the initial population has another size than the problem.
    /// </exception>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria) =>
        new Minimization(Configuration, new MersenneTwisterUniformRng(seed)).Minimize(P, endCriteria);

    // MSVC's std::partial_sort(first, first + 1, last, sort_by_cost): its heap of one is the front, and each later
    // member costing less replaces it, the front moving to that member's place.
    private static void PartialSortFront(List<Candidate> population)
    {
        for (int next = 1; next < population.Count; ++next)
        {
            if (population[next].Cost < population[0].Cost)
            {
                (population[0], population[next]) = (population[next], population[0]);
            }
        }
    }

    // A member: its values and its cost. Values are never changed in place once shared, so a copy of the struct is a
    // copy of QuantLib's Candidate.
    private struct Candidate
    {
        public Vector Values;
        public double Cost;

        public Candidate(Vector values, double cost)
        {
            Values = values;
            Cost = cost;
        }
    }

    // One minimization's state, QuantLib's mutable members.
    private sealed class Minimization
    {
        private readonly DifferentialEvolutionConfiguration configuration;
        private readonly MersenneTwisterUniformRng rng;
        private Vector upperBound = new(0), lowerBound = new(0);
        private Vector currGenSizeWeights = new(0), currGenCrossover = new(0);
        private Candidate bestMemberEver;

        public Minimization(DifferentialEvolutionConfiguration configuration, MersenneTwisterUniformRng rng)
        {
            this.configuration = configuration;
            this.rng = rng;
        }

        public EndCriteriaType Minimize(ProblemState p, EndCriteria endCriteria)
        {
            EndCriteriaType ecType;
            p.Reset();

            if (configuration.UpperBound.IsEmpty)
            {
                upperBound = p.Constraint.UpperBound(p.CurrentValue);
            }
            else
            {
                if (!(configuration.UpperBound.Length == p.CurrentValue.Size))
                {
                    throw new NanoQLException("wrong upper bound size in differential evolution configuration");
                }

                upperBound = new Vector(configuration.UpperBound.AsSpan());
            }

            if (configuration.LowerBound.IsEmpty)
            {
                lowerBound = p.Constraint.LowerBound(p.CurrentValue);
            }
            else
            {
                if (!(configuration.LowerBound.Length == p.CurrentValue.Size))
                {
                    throw new NanoQLException("wrong lower bound size in differential evolution configuration");
                }

                lowerBound = new Vector(configuration.LowerBound.AsSpan());
            }

            currGenSizeWeights = new Vector(configuration.PopulationMembers, configuration.StepsizeWeight);
            currGenCrossover = new Vector(configuration.PopulationMembers, configuration.CrossoverProbability);

            var population = new List<Candidate>();
            if (!configuration.InitialPopulation.IsEmpty)
            {
                foreach (System.Collections.Immutable.ImmutableArray<double> member in configuration.InitialPopulation)
                {
                    var values = new Vector(member.AsSpan());
                    if (!(values.Size == p.CurrentValue.Size))
                    {
                        throw new NanoQLException("wrong values size in initial population");
                    }

                    population.Add(new Candidate(values, p.CostFunction.Value(values)));
                }
            }
            else
            {
                for (int i = 0; i < configuration.PopulationMembers; ++i)
                {
                    population.Add(new Candidate(new Vector(p.CurrentValue.Size, 0.0), 0.0));
                }

                FillInitialPopulation(population, p);
            }

            PartialSortFront(population);
            bestMemberEver = population[0];
            double fxOld = population[0].Cost;
            int iteration = 0, stationaryPointIteration = 0;

            // main loop - calculate consecutive emerging populations
            while (true)
            {
                if (endCriteria.CheckMaxIterations(iteration++))
                {
                    ecType = EndCriteriaType.MaxIterations;
                    break;
                }

                CalculateNextGeneration(population, p);
                PartialSortFront(population);
                if (population[0].Cost < bestMemberEver.Cost)
                {
                    bestMemberEver = population[0];
                }

                double fxNew = population[0].Cost;
                EndCriteriaCheck stationary =
                    endCriteria.CheckStationaryFunctionValue(fxOld, fxNew, stationaryPointIteration);
                stationaryPointIteration = stationary.StationaryStateIterations;
                if (stationary.Stop)
                {
                    ecType = stationary.Type;
                    break;
                }

                fxOld = fxNew;
            }

            p.CurrentValue = bestMemberEver.Values.Clone();
            p.FunctionValue = bestMemberEver.Cost;
            return ecType;
        }

        // QuantLib's randomize: a Fisher-Yates shuffle on the generator's 32-bit integers.
        private void Randomize<T>(IList<T> items)
        {
            int n = items.Count;
            for (int i = n - 1; i > 0; --i)
            {
                int j = (int)(rng.NextInt32() % (i + 1));
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private void CalculateNextGeneration(List<Candidate> population, ProblemState p)
        {
            List<Candidate> mirrorPopulation;
            List<Candidate> oldPopulation = [.. population];
            double stepsizeWeight = configuration.StepsizeWeight;

            switch (configuration.Strategy)
            {
                case DifferentialEvolutionStrategy.Rand1Standard:
                    {
                        Randomize(population);
                        List<Candidate> shuffledPop1 = [.. population];
                        Randomize(population);
                        List<Candidate> shuffledPop2 = [.. population];
                        Randomize(population);
                        mirrorPopulation = shuffledPop1;

                        for (int popIter = 0; popIter < population.Count; popIter++)
                        {
                            population[popIter] = population[popIter] with
                            {
                                Values = population[popIter].Values
                                    + (stepsizeWeight * (shuffledPop1[popIter].Values - shuffledPop2[popIter].Values)),
                            };
                        }
                    }

                    break;

                case DifferentialEvolutionStrategy.BestMemberWithJitter:
                    {
                        Randomize(population);
                        List<Candidate> shuffledPop1 = [.. population];
                        Randomize(population);
                        var jitter = new Vector(population[0].Values.Size, 0.0);

                        for (int popIter = 0; popIter < population.Count; popIter++)
                        {
                            for (int k = 0; k < jitter.Size; k++)
                            {
                                jitter[k] = rng.NextReal();
                            }

                            population[popIter] = population[popIter] with
                            {
                                Values = bestMemberEver.Values
                                    + ((shuffledPop1[popIter].Values - population[popIter].Values)
                                        * ((0.0001 * jitter) + stepsizeWeight)),
                            };
                        }

                        mirrorPopulation = [.. Enumerable.Repeat(bestMemberEver, population.Count)];
                    }

                    break;

                case DifferentialEvolutionStrategy.CurrentToBest2Diffs:
                    {
                        Randomize(population);
                        List<Candidate> shuffledPop1 = [.. population];
                        Randomize(population);

                        for (int popIter = 0; popIter < population.Count; popIter++)
                        {
                            population[popIter] = population[popIter] with
                            {
                                Values = oldPopulation[popIter].Values
                                    + (stepsizeWeight * (bestMemberEver.Values - oldPopulation[popIter].Values))
                                    + (stepsizeWeight * (population[popIter].Values - shuffledPop1[popIter].Values)),
                            };
                        }

                        mirrorPopulation = shuffledPop1;
                    }

                    break;

                case DifferentialEvolutionStrategy.Rand1DiffWithPerVectorDither:
                    {
                        Randomize(population);
                        List<Candidate> shuffledPop1 = [.. population];
                        Randomize(population);
                        List<Candidate> shuffledPop2 = [.. population];
                        Randomize(population);
                        mirrorPopulation = shuffledPop1;
                        var fWeight = new Vector(population[0].Values.Size, 0.0);
                        for (int k = 0; k < fWeight.Size; k++)
                        {
                            fWeight[k] = ((1.0 - stepsizeWeight) * rng.NextReal()) + stepsizeWeight;
                        }

                        for (int popIter = 0; popIter < population.Count; popIter++)
                        {
                            population[popIter] = population[popIter] with
                            {
                                Values = population[popIter].Values
                                    + (fWeight * (shuffledPop1[popIter].Values - shuffledPop2[popIter].Values)),
                            };
                        }
                    }

                    break;

                case DifferentialEvolutionStrategy.Rand1DiffWithDither:
                    {
                        Randomize(population);
                        List<Candidate> shuffledPop1 = [.. population];
                        Randomize(population);
                        List<Candidate> shuffledPop2 = [.. population];
                        Randomize(population);
                        mirrorPopulation = shuffledPop1;
                        double fWeight = ((1.0 - stepsizeWeight) * rng.NextReal()) + stepsizeWeight;
                        for (int popIter = 0; popIter < population.Count; popIter++)
                        {
                            population[popIter] = population[popIter] with
                            {
                                Values = population[popIter].Values
                                    + (fWeight * (shuffledPop1[popIter].Values - shuffledPop2[popIter].Values)),
                            };
                        }
                    }

                    break;

                case DifferentialEvolutionStrategy.EitherOrWithOptimalRecombination:
                    {
                        Randomize(population);
                        List<Candidate> shuffledPop1 = [.. population];
                        Randomize(population);
                        List<Candidate> shuffledPop2 = [.. population];
                        Randomize(population);
                        mirrorPopulation = shuffledPop1;
                        double probFWeight = 0.5;
                        if (rng.NextReal() < probFWeight)
                        {
                            for (int popIter = 0; popIter < population.Count; popIter++)
                            {
                                population[popIter] = population[popIter] with
                                {
                                    Values = oldPopulation[popIter].Values
                                        + (stepsizeWeight * (shuffledPop1[popIter].Values - shuffledPop2[popIter].Values)),
                                };
                            }
                        }
                        else
                        {
                            double K = 0.5 * (stepsizeWeight + 1); // invariant with respect to probFWeight used
                            for (int popIter = 0; popIter < population.Count; popIter++)
                            {
                                population[popIter] = population[popIter] with
                                {
                                    Values = oldPopulation[popIter].Values
                                        + (K * (shuffledPop1[popIter].Values - shuffledPop2[popIter].Values
                                            - (2.0 * population[popIter].Values))),
                                };
                            }
                        }
                    }

                    break;

                case DifferentialEvolutionStrategy.Rand1SelfadaptiveWithRotation:
                    {
                        Randomize(population);
                        List<Candidate> shuffledPop1 = [.. population];
                        Randomize(population);
                        List<Candidate> shuffledPop2 = [.. population];
                        Randomize(population);
                        mirrorPopulation = shuffledPop1;

                        AdaptSizeWeights();

                        for (int popIter = 0; popIter < population.Count; popIter++)
                        {
                            if (rng.NextReal() < 0.1)
                            {
                                population[popIter] = population[popIter] with
                                {
                                    Values = RotateArray(bestMemberEver.Values),
                                };
                            }
                            else
                            {
                                population[popIter] = population[popIter] with
                                {
                                    Values = bestMemberEver.Values
                                        + (currGenSizeWeights[popIter]
                                            * (shuffledPop1[popIter].Values - shuffledPop2[popIter].Values)),
                                };
                            }
                        }
                    }

                    break;

                default:
                    throw new NanoQLException($"Unknown strategy ({(int)configuration.Strategy})");
            }

            // in order to avoid unnecessary copying we use the same population object for mutants
            Crossover(oldPopulation, population, population, mirrorPopulation, p);
        }

        private void Crossover(
            List<Candidate> oldPopulation,
            List<Candidate> population,
            List<Candidate> mutantPopulation,
            List<Candidate> mirrorPopulation,
            ProblemState p)
        {
            if (configuration.CrossoverIsAdaptive)
            {
                AdaptCrossover();
            }

            Vector mutationProbabilities = GetMutationProbabilities(population);

            var crossoverMask = new List<Vector>();
            var invCrossoverMask = new List<Vector>();
            for (int i = 0; i < population.Count; i++)
            {
                crossoverMask.Add(new Vector(population[0].Values.Size, 1.0));
                invCrossoverMask.Add(new Vector(population[0].Values.Size, 1.0));
            }

            GetCrossoverMask(crossoverMask, invCrossoverMask, mutationProbabilities);

            // crossover of the old and mutant population
            for (int popIter = 0; popIter < population.Count; popIter++)
            {
                Vector values = (oldPopulation[popIter].Values * invCrossoverMask[popIter])
                    + (mutantPopulation[popIter].Values * crossoverMask[popIter]);

                // immediately apply bounds if specified
                if (configuration.ApplyBounds)
                {
                    for (int memIter = 0; memIter < values.Size; memIter++)
                    {
                        if (values[memIter] > upperBound[memIter])
                        {
                            values[memIter] = upperBound[memIter]
                                + (rng.NextReal() * (mirrorPopulation[popIter].Values[memIter] - upperBound[memIter]));
                        }

                        if (values[memIter] < lowerBound[memIter])
                        {
                            values[memIter] = lowerBound[memIter]
                                + (rng.NextReal() * (mirrorPopulation[popIter].Values[memIter] - lowerBound[memIter]));
                        }
                    }
                }

                // evaluate objective function as soon as possible to avoid unnecessary loops
                double cost;
                try
                {
                    cost = p.Value(values);
                }
                catch (Exception e) when (e is NanoQLException or ArgumentException)
                {
                    cost = NumericLimits.QL_MAX_REAL;
                }

                if (!double.IsFinite(cost))
                {
                    cost = NumericLimits.QL_MAX_REAL;
                }

                population[popIter] = new Candidate(values, cost);
            }
        }

        private void GetCrossoverMask(
            List<Vector> crossoverMask, List<Vector> invCrossoverMask, Vector mutationProbabilities)
        {
            for (int cmIter = 0; cmIter < crossoverMask.Count; cmIter++)
            {
                for (int memIter = 0; memIter < crossoverMask[cmIter].Size; memIter++)
                {
                    if (rng.NextReal() < mutationProbabilities[cmIter])
                    {
                        invCrossoverMask[cmIter][memIter] = 0.0;
                    }
                    else
                    {
                        crossoverMask[cmIter][memIter] = 0.0;
                    }
                }
            }
        }

        private Vector GetMutationProbabilities(List<Candidate> population)
        {
            Vector mutationProbabilities = currGenCrossover.Clone();
            int size = population[0].Values.Size;
            switch (configuration.CrossoverType)
            {
                case DifferentialEvolutionCrossoverType.Normal:
                    break;
                case DifferentialEvolutionCrossoverType.Binomial:
                    mutationProbabilities = (currGenCrossover * (1.0 - (1.0 / size))) + (1.0 / size);
                    break;
                case DifferentialEvolutionCrossoverType.Exponential:
                    for (int coIter = 0; coIter < currGenCrossover.Size; coIter++)
                    {
                        mutationProbabilities[coIter] =
                            (1.0 - Math.Pow(currGenCrossover[coIter], size))
                            / (size * (1.0 - currGenCrossover[coIter]));
                    }

                    break;
                default:
                    throw new NanoQLException($"Unknown crossover type ({(int)configuration.CrossoverType})");
            }

            return mutationProbabilities;
        }

        // QuantLib's rotateArray, which shuffles a copy.
        private Vector RotateArray(Vector inputArray)
        {
            double[] a = inputArray.AsSpan().ToArray();
            Randomize(a);
            return new Vector(a);
        }

        private void AdaptSizeWeights()
        {
            // [=Fl & =Fu] respectively see Brest, J. et al., 2006,
            // "Self-Adapting Control Parameters in Differential
            // Evolution"
            double sizeWeightLowerBound = 0.1, sizeWeightUpperBound = 0.9;

            // [=tau1] A Comparative Study on Numerical Benchmark
            // Problems." page 649 for reference
            double sizeWeightChangeProb = 0.1;
            for (int i = 0; i < currGenSizeWeights.Size; i++)
            {
                if (rng.NextReal() < sizeWeightChangeProb)
                {
                    currGenSizeWeights[i] = sizeWeightLowerBound + (rng.NextReal() * sizeWeightUpperBound);
                }
            }
        }

        private void AdaptCrossover()
        {
            double crossoverChangeProb = 0.1; // [=tau2]
            for (int i = 0; i < currGenCrossover.Size; i++)
            {
                if (rng.NextReal() < crossoverChangeProb)
                {
                    currGenCrossover[i] = rng.NextReal();
                }
            }
        }

        private void FillInitialPopulation(List<Candidate> population, ProblemState p)
        {
            // use initial values provided by the user
            Vector first = p.CurrentValue.Clone();
            population[0] = new Candidate(first, p.CostFunction.Value(first));

            // rest of the initial population is random
            for (int j = 1; j < population.Count; ++j)
            {
                Vector values = population[j].Values;
                for (int i = 0; i < p.CurrentValue.Size; ++i)
                {
                    double l = lowerBound[i], u = upperBound[i];
                    values[i] = l + ((u - l) * rng.NextReal());
                }

                double cost = p.CostFunction.Value(values);
                if (!double.IsFinite(cost))
                {
                    cost = NumericLimits.QL_MAX_REAL;
                }

                population[j] = new Candidate(values, cost);
            }
        }
    }
}
