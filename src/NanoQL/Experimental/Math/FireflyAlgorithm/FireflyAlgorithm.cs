// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp ql/experimental/math/fireflyalgorithm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The firefly algorithm, with a differential evolution subpopulation: QuantLib's <c>FireflyAlgorithm</c>.
/// </summary>
/// <remarks>
/// <para>
/// M fireflies explore the N-dimensional parameter space, each moved by its attraction to the brighter ones (the
/// intensity) and a random walk: X_{i,j}^{k+1} = X_{i,j}^k + I(X^k)_{i,j} + RandomWalk_{i,j}^k, the intensity monotonic.
/// The population is sorted each iteration and split: the M − M_de brightest move as fireflies, the M_de others by the
/// differential evolution operator Y^{k+1} = X_best^k + F (X_{r1}^k − X_{r2}^k), each coordinate crossed over with
/// probability C (one always), and accepted where it improves. With M_de = M it is a differential evolution optimizer
/// whose base is a random member. The algorithm stops at its iterations or after its stationary iterations. Reference:
/// A. Abdullah et al., "A New Hybrid Firefly Algorithm for Complex and Nonlinear Problem", Advances in Intelligent and
/// Soft Computing 151, pp. 673-680, 2012.
/// </para>
/// <para>
/// The population starts on a Sobol sequence within the bounds, its seed the algorithm's: QuantLib's is 0, the clock,
/// read only past the 32 dimensions of Jaeckel's direction integers, so the bits are QuantLib's up to there. A
/// problem's constraint is never empty, so QuantLib's refusal of one doesn't arise. The differential evolution's integers are
/// <c>std::uniform_int_distribution</c>'s on a <c>std::mt19937</c>, drawn as MSVC draws them, and its crossover's
/// uniforms a Mersenne Twister's, both on the seed, which is required: QuantLib's default seeds from the clock.
/// Immutable: each minimization copies the intensity and the walk and starts the generators from the seed, where
/// QuantLib's continues them.
/// </para>
/// <para>
/// Fixes (<c>Divergences/FireflyAlgorithmTests.cs</c>): each moved firefly's new value goes to its own place in the
/// sorted population, where QuantLib's writes it at the place its index names, another firefly's, so the population's
/// values and positions part after the first move; pure differential evolution takes a copy of its random base, where
/// QuantLib's assigns through a reference and overwrites the best firefly with it; and fewer than three differential
/// evolution members are refused, since three distinct members can't be drawn from fewer, where QuantLib's draws loop
/// forever.
/// </para>
/// <para>
/// The differential evolution's donors, and pure differential evolution's base, are drawn in [Mfa, M − 1] and read as
/// fireflies' labels, the order of the Sobol start, not as places in the sorted population, as QuantLib reads them; its
/// header doesn't say which members it means. So in a hybrid the donors are the fireflies labelled Mfa to M − 1, which
/// may be moving as fireflies, and a target may be its own donor; in pure differential evolution the two readings
/// agree. The three-member refusal follows the label reading.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class FireflyAlgorithm : OptimizationMethod
{
    private readonly double mutation, crossover;
    private readonly int M, Mde, Mfa;
    private readonly FireflyAlgorithmIntensity intensity;
    private readonly FireflyAlgorithmRandomWalk randomWalk;
    private readonly long seed;

    /// <summary>Initializes a new instance of the <see cref="FireflyAlgorithm"/> class.</summary>
    /// <param name="M">The population's size.</param>
    /// <param name="intensity">The intensity, which each minimization copies.</param>
    /// <param name="randomWalk">The random walk, which each minimization copies.</param>
    /// <param name="Mde">The differential evolution subpopulation's size, 0 or at least 3; QuantLib's default 0.</param>
    /// <param name="mutationFactor">The mutation factor F, QuantLib's default 1.0.</param>
    /// <param name="crossoverFactor">The crossover constant C, QuantLib's default 0.5.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="Mde"/> exceeds <paramref name="M"/>, with QuantLib's text, or is 1 or 2.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public FireflyAlgorithm(
        int M,
        FireflyAlgorithmIntensity intensity,
        FireflyAlgorithmRandomWalk randomWalk,
        int Mde,
        double mutationFactor,
        double crossoverFactor,
        long seed)
    {
        ArgumentNullException.ThrowIfNull(intensity);
        ArgumentNullException.ThrowIfNull(randomWalk);
        ArgumentOutOfRangeException.ThrowIfNegative(M);
        ArgumentOutOfRangeException.ThrowIfNegative(Mde);
        Seeds.Check(seed);
        if (!(M >= Mde))
        {
            throw new ArgumentException(
                "Differential Evolution subpopulation cannot be larger than total population", nameof(Mde));
        }

        if (Mde is 1 or 2)
        {
            throw new ArgumentException(
                $"Differential Evolution subpopulation of {Mde}: its operator needs three distinct members", nameof(Mde));
        }

        mutation = mutationFactor;
        crossover = crossoverFactor;
        this.M = M;
        this.Mde = Mde;
        Mfa = M - Mde;
        this.intensity = intensity.Clone();
        this.randomWalk = randomWalk.Clone();
        this.seed = seed;
    }

    /// <inheritdoc/>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(P);
        ArgumentNullException.ThrowIfNull(endCriteria);
        return new Minimization(this).Minimize(P, endCriteria);
    }

    // One minimization's population, which the intensity and the walk read: QuantLib's protected members.
    internal sealed class Population
    {
        public int Mfa;
        public int N;
        public Vector[] X = [];
        public Vector[] XI = [];
        public Vector[] XRW = [];
        public (double Value, int Index)[] Values = [];
        public Vector LowerBound = new();
        public Vector UpperBound = new();
    }

    // One minimization's state.
    private sealed class Minimization
    {
        private readonly FireflyAlgorithm method;
        private readonly Population pop = new();
        private readonly FireflyAlgorithmIntensity intensity;
        private readonly FireflyAlgorithmRandomWalk randomWalk;
        private readonly MersenneTwisterUniformRng generator;
        private readonly MersenneTwisterUniformRng rng;

        public Minimization(FireflyAlgorithm method)
        {
            this.method = method;
            intensity = method.intensity.Clone();
            randomWalk = method.randomWalk.Clone();
            generator = new MersenneTwisterUniformRng(method.seed);
            rng = new MersenneTwisterUniformRng(method.seed);
        }

        public EndCriteriaType Minimize(ProblemState P, EndCriteria endCriteria)
        {
            int M = method.M, Mfa = method.Mfa;
            EndCriteriaType ecType;
            P.Reset();
            int iteration = 0;
            int iterationStat = 0;
            int maxIteration = endCriteria.MaxIterations;
            int maxIStationary = endCriteria.MaxStationaryStateIterations;
            StartState(P);
            int N = pop.N;
            bool isFA = Mfa > 0;

            // variables for DE
            var z = new Vector(N, 0.0);
            int indexR1, indexR2;

            // the DE's indexes, [Mfa, M - 1]
            long lowest = Mfa, highest = method.Mde > 0 ? M - 1 : M;

            // set best value & position
            double bestValue = pop.Values[0].Value;
            int bestPosition = 0;
            for (int i = 1; i < M; i++)
            {
                if (pop.Values[i].Value < bestValue)
                {
                    bestPosition = i;
                    bestValue = pop.Values[i].Value;
                }
            }

            Vector bestX = pop.X[bestPosition].Clone();

            // run optimization
            while (true)
            {
                iteration++;
                iterationStat++;

                // check if stopping criteria is met
                if (iteration > maxIteration || iterationStat > maxIStationary)
                {
                    break;
                }

                // divide into two subpopulations: first sort values
                Array.Sort(pop.Values, (a, b) => a.Value < b.Value ? -1 : b.Value < a.Value ? 1 : a.Index.CompareTo(b.Index));

                // differential evolution
                if (Mfa < M)
                {
                    int indexBest = pop.Values[0].Index;
                    Vector xBest = pop.X[indexBest];
                    for (int i = Mfa; i < M; i++)
                    {
                        if (!isFA)
                        {
                            // pure DE requires a random index; its base a copy
                            indexBest = (int)MsvcRandom.UniformInt(generator, lowest, highest);
                            xBest = pop.X[indexBest].Clone();
                        }

                        do
                        {
                            indexR1 = (int)MsvcRandom.UniformInt(generator, lowest, highest);
                        }
                        while (indexR1 == indexBest);

                        do
                        {
                            indexR2 = (int)MsvcRandom.UniformInt(generator, lowest, highest);
                        }
                        while (indexR2 == indexBest || indexR2 == indexR1);

                        int index = pop.Values[i].Index;
                        Vector x = pop.X[index];
                        Vector xR1 = pop.X[indexR1];
                        Vector xR2 = pop.X[indexR2];
                        int rIndex = (int)MsvcRandom.UniformInt(generator, 0, N - 1);
                        for (int j = 0; j < N; j++)
                        {
                            if (j == rIndex || rng.NextReal() <= method.crossover)
                            {
                                // change x[j] according to crossover
                                z[j] = xBest[j] + (method.mutation * (xR1[j] - xR2[j]));
                            }
                            else
                            {
                                z[j] = x[j];
                            }

                            // enforce bounds on positions
                            if (z[j] < pop.LowerBound[j])
                            {
                                z[j] = pop.LowerBound[j];
                            }
                            else if (z[j] > pop.UpperBound[j])
                            {
                                z[j] = pop.UpperBound[j];
                            }
                        }

                        double val = P.Value(z);

                        // the firefly's own place in the sorted population
                        if (val < pop.Values[i].Value)
                        {
                            // accept new point
                            z.AsSpan().CopyTo(x.AsSpan());
                            pop.Values[i].Value = val;

                            // mark best
                            if (val < bestValue)
                            {
                                bestValue = val;
                                bestX = x.Clone();
                                iterationStat = 0;
                            }
                        }
                    }
                }

                // firefly algorithm
                if (isFA)
                {
                    // according to the intensity, determine best global position
                    intensity.FindBrightest();

                    // prepare random walk
                    randomWalk.Walk();

                    // loop over particles
                    for (int i = 0; i < Mfa; i++)
                    {
                        int index = pop.Values[i].Index;
                        Vector x = pop.X[index];
                        Vector xI = pop.XI[index];
                        Vector xRW = pop.XRW[index];

                        // loop over dimensions
                        for (int j = 0; j < N; j++)
                        {
                            // update position
                            z[j] = x[j] + xI[j] + xRW[j];

                            // enforce bounds on positions
                            if (z[j] < pop.LowerBound[j])
                            {
                                z[j] = pop.LowerBound[j];
                            }
                            else if (z[j] > pop.UpperBound[j])
                            {
                                z[j] = pop.UpperBound[j];
                            }
                        }

                        double val = P.Value(z);
                        if (!double.IsNaN(val))
                        {
                            // accept new point, at the firefly's own place in the sorted population
                            z.AsSpan().CopyTo(x.AsSpan());
                            pop.Values[i].Value = val;

                            // mark best
                            if (val < bestValue)
                            {
                                bestValue = val;
                                bestX = x.Clone();
                                iterationStat = 0;
                            }
                        }
                    }
                }
            }

            if (iteration > maxIteration)
            {
                ecType = EndCriteriaType.MaxIterations;
            }
            else
            {
                ecType = EndCriteriaType.StationaryPoint;
            }

            // set result to best point
            P.CurrentValue = bestX;
            P.FunctionValue = bestValue;
            return ecType;
        }

        // QuantLib's startState: the population on a Sobol sequence within the bounds.
        private void StartState(ProblemState P)
        {
            int M = method.M;
            int N = P.CurrentValue.Size;
            pop.Mfa = method.Mfa;
            pop.N = N;
            pop.UpperBound = P.Constraint.UpperBound(P.CurrentValue);
            pop.LowerBound = P.Constraint.LowerBound(P.CurrentValue);
            Vector bounds = pop.UpperBound - pop.LowerBound;

            // random initialization is done by Sobol sequence
            var sobol = new SobolRsg(N, method.seed);

            // prepare containers
            pop.X = new Vector[M];
            pop.XI = new Vector[M];
            pop.XRW = new Vector[M];
            pop.Values = new (double, int)[M];
            for (int i = 0; i < M; i++)
            {
                ReadOnlySpan<double> sample = sobol.NextSequence().Value.Span;
                var x = new Vector(N, 0.0);
                pop.X[i] = x;
                pop.XI[i] = new Vector(N, 0.0);
                pop.XRW[i] = new Vector(N, 0.0);
                for (int j = 0; j < N; j++)
                {
                    // assign X = lb + (ub - lb) * random
                    x[j] = pop.LowerBound[j] + (bounds[j] * sample[j]);
                }

                // evaluate point
                pop.Values[i] = (P.Value(x), i);
            }

            // init intensity & random walk
            intensity.Init(pop);
            randomWalk.Bind(pop);
        }
    }
}
