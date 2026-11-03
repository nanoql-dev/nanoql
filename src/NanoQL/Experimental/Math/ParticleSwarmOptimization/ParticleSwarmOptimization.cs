// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp ql/experimental/math/particleswarmoptimization.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>Particle swarm optimization: QuantLib's <c>ParticleSwarmOptimization</c>.</summary>
/// <remarks>
/// <para>
/// M particles explore the N-dimensional parameter space. Each position moves by its velocity,
/// X_{i,j}^{k+1} = X_{i,j}^k + V_{i,j}^{k+1}, the velocity
/// V_{i,j}^{k+1} = χ(V_{i,j}^k + c₁ r_{i,j}^k (P_{i,j}^k − X_{i,j}^k) + c₂ R_{i,j}^k (G_{i,j}^k − X_{i,j}^k)), with r and R
/// uniform in [0, 1], P the particle's personal best and G its social best, which the topology sets: the PSO with
/// constriction factor (PSO-Co). The PSO with inertia factor (PSO-In) takes ω, ĉ₁ and ĉ₂ in place of χ, χc₁ and χc₂. χ
/// is 2/|2 − φ − √(φ² − 4φ)| with φ = c₁ + c₂; stable convergence needs φ ≥ 4, and Clerc and Kennedy recommend
/// c₁ = c₂ = 2.05. The inertia scales the previous velocity, a constant in the canonical PSO. The optimization stops at
/// its iterations or after its stationary iterations; positions are clamped to the bounds, the velocity zeroed there.
/// </para>
/// <para>
/// The swarm starts on a Sobol sequence within the bounds, its seed the optimizer's: QuantLib's is 0, the clock, read
/// only past the 32 dimensions of Jaeckel's direction integers, so the bits are QuantLib's up to 16 parameters. The
/// velocities' uniforms are a Mersenne Twister's on the seed, which is required: QuantLib's default seeds from the
/// clock, and its two constructors are ambiguous for an integer literal there. A problem's constraint is never empty,
/// so QuantLib's refusal of one doesn't arise. Immutable: each minimization copies the topology and the inertia and
/// starts the generators from the seed, where QuantLib's continues them and appends a new swarm to the last.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ParticleSwarmOptimization : OptimizationMethod
{
    private readonly int M;
    private readonly double c0, c1, c2;
    private readonly long seed;
    private readonly ParticleSwarmOptimizationTopology topology;
    private readonly ParticleSwarmOptimizationInertia inertia;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParticleSwarmOptimization"/> class, the PSO with constriction factor.
    /// </summary>
    /// <param name="M">The swarm's size.</param>
    /// <param name="topology">The topology, which each minimization copies.</param>
    /// <param name="inertia">The inertia, which each minimization copies.</param>
    /// <param name="c1">The self-recognition coefficient c₁, QuantLib's default 2.05.</param>
    /// <param name="c2">The social-recognition coefficient c₂, QuantLib's default 2.05.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="NanoQLException">φ² − 4φ is 0, with QuantLib's text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public ParticleSwarmOptimization(
        int M,
        ParticleSwarmOptimizationTopology topology,
        ParticleSwarmOptimizationInertia inertia,
        double c1,
        double c2,
        long seed)
        : this(M, topology, inertia, seed)
    {
        double phi = c1 + c2;
        if (!((phi * phi) - (4 * phi) != 0.0))
        {
            throw new NanoQLException("Invalid phi");
        }

        c0 = 2.0 / Math.Abs(2.0 - phi - Math.Sqrt((phi * phi) - (4 * phi)));
        this.c1 = c0 * c1;
        this.c2 = c0 * c2;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParticleSwarmOptimization"/> class, the PSO with inertia factor.
    /// </summary>
    /// <param name="M">The swarm's size.</param>
    /// <param name="topology">The topology, which each minimization copies.</param>
    /// <param name="inertia">The inertia, which each minimization copies.</param>
    /// <param name="omega">The inertia factor ω.</param>
    /// <param name="c1">The self-recognition coefficient ĉ₁.</param>
    /// <param name="c2">The social-recognition coefficient ĉ₂.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public ParticleSwarmOptimization(
        int M,
        ParticleSwarmOptimizationTopology topology,
        ParticleSwarmOptimizationInertia inertia,
        double omega,
        double c1,
        double c2,
        long seed)
        : this(M, topology, inertia, seed)
    {
        c0 = omega;
        this.c1 = c1;
        this.c2 = c2;
    }

    private ParticleSwarmOptimization(
        int M, ParticleSwarmOptimizationTopology topology, ParticleSwarmOptimizationInertia inertia, long seed)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(inertia);
        ArgumentOutOfRangeException.ThrowIfNegative(M);
        Seeds.Check(seed);
        this.M = M;
        this.topology = topology.Clone();
        this.inertia = inertia.Clone();
        this.seed = seed;
    }

    /// <inheritdoc/>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(P);
        ArgumentNullException.ThrowIfNull(endCriteria);
        return new Minimization(this).Minimize(P, endCriteria);
    }

    // One minimization's swarm, which the topology and the inertia read: QuantLib's protected members.
    internal sealed class Swarm
    {
        public Vector[] X = [];
        public Vector[] V = [];
        public Vector[] PersonalBestX = [];
        public Vector[] GlobalBestX = [];
        public Vector PersonalBestF = new();
        public Vector GlobalBestF = new();
        public Vector LowerBound = new();
        public Vector UpperBound = new();
    }

    // One minimization's state.
    private sealed class Minimization
    {
        private readonly ParticleSwarmOptimization method;
        private readonly Swarm swarm = new();
        private readonly ParticleSwarmOptimizationTopology topology;
        private readonly ParticleSwarmOptimizationInertia inertia;
        private readonly MersenneTwisterUniformRng rng;
        private int N;

        public Minimization(ParticleSwarmOptimization method)
        {
            this.method = method;
            topology = method.topology.Clone();
            inertia = method.inertia.Clone();
            rng = new MersenneTwisterUniformRng(method.seed);
        }

        public EndCriteriaType Minimize(ProblemState P, EndCriteria endCriteria)
        {
            int M = method.M;
            EndCriteriaType ecType;
            P.Reset();
            int iteration = 0;
            int iterationStat = 0;
            int maxIteration = endCriteria.MaxIterations;
            int maxIStationary = endCriteria.MaxStationaryStateIterations;
            double bestValue = NumericLimits.QL_MAX_REAL;
            int bestPosition = 0;

            StartState(P, endCriteria);

            // set best value & position
            for (int i = 0; i < M; i++)
            {
                if (swarm.PersonalBestF[i] < bestValue)
                {
                    bestValue = swarm.PersonalBestF[i];
                    bestPosition = i;
                }
            }

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

                // according to the topology, determine best global position
                topology.FindSocialBest();

                // call inertia to change internal state
                inertia.SetValues();

                // loop over particles
                for (int i = 0; i < M; i++)
                {
                    Vector x = swarm.X[i];
                    Vector pB = swarm.PersonalBestX[i];
                    Vector gB = swarm.GlobalBestX[i];
                    Vector v = swarm.V[i];

                    // loop over dimensions
                    for (int j = 0; j < N; j++)
                    {
                        // update velocity, the second uniform drawn first, as MSVC evaluates QuantLib's expression
                        double r2 = rng.NextReal();
                        double r1 = rng.NextReal();
                        v[j] += (method.c1 * r1 * (pB[j] - x[j])) + (method.c2 * r2 * (gB[j] - x[j]));

                        // update position
                        x[j] += v[j];

                        // enforce bounds on positions
                        if (x[j] < swarm.LowerBound[j])
                        {
                            x[j] = swarm.LowerBound[j];
                            v[j] = 0.0;
                        }
                        else if (x[j] > swarm.UpperBound[j])
                        {
                            x[j] = swarm.UpperBound[j];
                            v[j] = 0.0;
                        }
                    }

                    // evaluate x
                    double f = P.Value(x);
                    if (f < swarm.PersonalBestF[i])
                    {
                        // update personal best
                        swarm.PersonalBestF[i] = f;
                        x.AsSpan().CopyTo(pB.AsSpan());

                        // check stationary condition
                        if (f < bestValue)
                        {
                            bestValue = f;
                            bestPosition = i;
                            iterationStat = 0;
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
            P.CurrentValue = swarm.PersonalBestX[bestPosition];
            P.FunctionValue = bestValue;
            return ecType;
        }

        // QuantLib's startState: the swarm on a Sobol sequence within the bounds, each velocity within ± the bounds' width.
        private void StartState(ProblemState P, EndCriteria endCriteria)
        {
            int M = method.M;
            N = P.CurrentValue.Size;
            topology.SetSize(M);
            inertia.SetSize(M, N, method.c0, endCriteria);
            swarm.PersonalBestF = new Vector(M, 0.0);
            swarm.GlobalBestF = new Vector(M, 0.0);
            swarm.UpperBound = P.Constraint.UpperBound(P.CurrentValue);
            swarm.LowerBound = P.Constraint.LowerBound(P.CurrentValue);
            Vector bounds = swarm.UpperBound - swarm.LowerBound;

            // random initialization is done by Sobol sequence
            var sobol = new SobolRsg(N * 2, method.seed);

            // prepare containers
            swarm.X = new Vector[M];
            swarm.V = new Vector[M];
            swarm.PersonalBestX = new Vector[M];
            swarm.GlobalBestX = new Vector[M];
            for (int i = 0; i < M; i++)
            {
                ReadOnlySpan<double> sample = sobol.NextSequence().Value.Span;
                var x = new Vector(N, 0.0);
                var v = new Vector(N, 0.0);
                swarm.X[i] = x;
                swarm.V[i] = v;
                swarm.GlobalBestX[i] = new Vector(N, 0.0);
                for (int j = 0; j < N; j++)
                {
                    // assign X = lb + (ub - lb) * random
                    x[j] = swarm.LowerBound[j] + (bounds[j] * sample[2 * j]);

                    // assign V = (ub - lb) * 2 * random - (ub - lb), between (lb - ub) and (ub - lb)
                    v[j] = bounds[j] * ((2.0 * sample[(2 * j) + 1]) - 1.0);
                }

                // evaluate X and assign as personal best
                swarm.PersonalBestX[i] = x.Clone();
                swarm.PersonalBestF[i] = P.Value(x);
            }

            // init topology & inertia
            topology.Bind(swarm);
            inertia.Bind(swarm);
        }
    }
}
